using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Adornments;
using Microsoft.VisualStudio.Text.Tagging;

namespace Vs2010Guard
{
    /// <summary>
    /// 把“VS2010 编不过”的位置做成红线（硬错误）或绿线（能编过但结果不对）。
    ///
    /// 按项目开关：只有在被检查的文件（或它的任意上级目录）里放了
    /// <c>vs2010check.on</c> 这个空文件时才会检查，默认对所有项目完全静默。
    ///
    /// 开关变化的两条路径都会刷新标记：
    ///   1) VS 菜单里切换 → Vs2010GuardSwitch.Changed 事件，立刻刷新；
    ///   2) 双击 .bat / 外部工具改了开关文件（插件不知道）→ 2 秒轮询发现，自己刷新。
    /// </summary>
    public sealed class Vs2010GuardTagger : ITagger<ErrorTag>
    {
        private readonly ITextBuffer _buffer;
        private readonly object _lock = new object();
        private List<Finding> _cache;
        private int _cacheVersion = -1;
        private bool _cacheEnabled;
        private volatile bool _lastEnabled;

        // 所有活着的 tagger + 一个轮询，用来发现“外部进程改了开关文件”
        private static readonly object LiveLock = new object();
        private static readonly List<WeakReference> Live = new List<WeakReference>();
        private static Timer _poller;

        public Vs2010GuardTagger(ITextBuffer buffer)
        {
            _buffer = buffer;
            _buffer.Changed += OnBufferChanged;
            Vs2010GuardSwitch.Changed += OnSwitchChanged;
            _lastEnabled = IsEnabledForBuffer();

            lock (LiveLock)
            {
                Live.Add(new WeakReference(this));
                if (_poller == null)
                {
                    // 2 秒一次，代价可以忽略（就是几次 File.Exists）
                    _poller = new Timer(PollTick, null, 2000, 2000);
                }
            }
        }

        private static void PollTick(object state)
        {
            List<Vs2010GuardTagger> alive = new List<Vs2010GuardTagger>();
            lock (LiveLock)
            {
                for (int i = Live.Count - 1; i >= 0; i--)
                {
                    Vs2010GuardTagger t = Live[i].Target as Vs2010GuardTagger;
                    if (t == null) Live.RemoveAt(i);
                    else alive.Add(t);
                }
            }

            foreach (Vs2010GuardTagger t in alive)
            {
                try { t.CheckSwitchChanged(); }
                catch (Exception) { }
            }
        }

        private void CheckSwitchChanged()
        {
            bool now = IsEnabledForBuffer();
            if (now == _lastEnabled) return;

            _lastEnabled = now;
            lock (_lock)
            {
                _cache = null;
                _cacheVersion = -1;
            }
            Vs2010GuardDiagnostics.Log("发现开关被外部改动：enabled=" + now + "  " + DescribeBuffer());
            RefreshAll();
        }

        private void OnSwitchChanged(object sender, EventArgs e)
        {
            _lastEnabled = IsEnabledForBuffer();
            lock (_lock)
            {
                _cache = null;
                _cacheVersion = -1;
            }
            RefreshAll();
        }

        private void RefreshAll()
        {
            EventHandler<SnapshotSpanEventArgs> handler = TagsChanged;
            if (handler == null) return;
            ITextSnapshot snapshot = _buffer.CurrentSnapshot;
            handler(this, new SnapshotSpanEventArgs(new SnapshotSpan(snapshot, 0, snapshot.Length)));
        }

        public event EventHandler<SnapshotSpanEventArgs> TagsChanged;

        private void OnBufferChanged(object sender, TextContentChangedEventArgs e)
        {
            lock (_lock)
            {
                _cache = null;
                _cacheVersion = -1;
            }
            EventHandler<SnapshotSpanEventArgs> handler = TagsChanged;
            if (handler != null)
            {
                handler(this, new SnapshotSpanEventArgs(new SnapshotSpan(e.After, 0, e.After.Length)));
            }
        }

        public IEnumerable<ITagSpan<ErrorTag>> GetTags(NormalizedSnapshotSpanCollection spans)
        {
            if (spans == null || spans.Count == 0) yield break;

            ITextSnapshot snapshot = spans[0].Snapshot;
            List<Finding> findings = GetFindings(snapshot);
            if (findings.Count == 0) yield break;

            foreach (Finding f in findings)
            {
                if (f.Start < 0 || f.Length <= 0 || f.Start + f.Length > snapshot.Length) continue;
                SnapshotSpan span = new SnapshotSpan(snapshot, f.Start, f.Length);
                if (!spans.IntersectsWith(new NormalizedSnapshotSpanCollection(span))) continue;
                string prefix = f.IsError ? "VS2010 会报错：" : "VS2010 上要注意：";
                string errorType = f.IsError
                    ? PredefinedErrorTypeNames.SyntaxError
                    : PredefinedErrorTypeNames.Warning;
                yield return new TagSpan<ErrorTag>(span, new ErrorTag(errorType, prefix + f.Message));
            }
        }

        private List<Finding> GetFindings(ITextSnapshot snapshot)
        {
            bool enabled = IsEnabledForBuffer();
            _lastEnabled = enabled;

            lock (_lock)
            {
                if (_cache != null && _cacheVersion == snapshot.Version.VersionNumber && _cacheEnabled == enabled)
                    return _cache;
            }

            List<Finding> result;
            if (!enabled)
            {
                Vs2010GuardDiagnostics.Log("已跳过（本目录没有 " + Vs2010GuardSwitch.MarkerFileName + "）: " + DescribeBuffer());
                result = new List<Finding>();
            }
            else
            {
                try
                {
                    string text = snapshot.GetText();
                    result = Vs2010Rules.Scan(text);
                    Vs2010Rules.ScanEncoding(DescribeBuffer(), result);
                    Vs2010GuardDiagnostics.Log("GetTags: " + DescribeBuffer() + " 长度 " + text.Length +
                        "，发现 " + result.Count + " 处问题");
                }
                catch (Exception ex)
                {
                    Vs2010GuardDiagnostics.Log("Scan 抛异常：" + ex.Message);
                    result = new List<Finding>();
                }
            }

            lock (_lock)
            {
                _cache = result;
                _cacheVersion = snapshot.Version.VersionNumber;
                _cacheEnabled = enabled;
            }
            return result;
        }

        private bool IsEnabledForBuffer()
        {
            if (Environment.GetEnvironmentVariable("VS2010GUARD_FORCE") == "1") return true;

            string path = DescribeBuffer();
            if (string.IsNullOrEmpty(path) || path.StartsWith("(")) return false;
            return Vs2010GuardSwitch.IsEnabledForFile(path);
        }

        private string DescribeBuffer()
        {
            ITextDocument document;
            if (_buffer.Properties.TryGetProperty<ITextDocument>(typeof(ITextDocument), out document) && document != null)
                return document.FilePath;
            return "(未保存的缓冲区)";
        }
    }
}
