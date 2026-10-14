using System.ComponentModel.Composition;
using System.IO;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Tagging;
using Microsoft.VisualStudio.Utilities;

namespace Vs2010Guard
{
    /// <summary>
    /// 只为 C 源文件（.c / .h）提供错误标记。
    /// 同时挂 "C/C++" 和 "code" 两个内容类型：只要其中一个能命中就能工作，
    /// 内容类型不匹配时 IsCTarget() 直接返回 false，不会误伤别的语言。
    /// 是否真的检查由 Vs2010GuardTagger 按 vs2010check.on 标记决定（默认关闭）。
    /// </summary>
    [Export(typeof(ITaggerProvider))]
    [ContentType("C/C++")]
    [ContentType("code")]
    [TagType(typeof(ErrorTag))]
    public sealed class Vs2010GuardTaggerProvider : ITaggerProvider
    {
        public ITagger<T> CreateTagger<T>(ITextBuffer buffer) where T : ITag
        {
            if (buffer == null) return null;

            // VS 的错误聚合器请求 IErrorTag，有的调用方直接要 ErrorTag —— 两个都收。
            // ITagger<out T> 是协变的，所以返回 ITagger<ErrorTag> 也能当 ITagger<IErrorTag> 用。
            if (typeof(T) != typeof(ErrorTag) && typeof(T) != typeof(IErrorTag)) return null;

            if (!IsCTarget(buffer)) return null;

            return buffer.Properties.GetOrCreateSingletonProperty(
                delegate { return new Vs2010GuardTagger(buffer); }) as ITagger<T>;
        }

        /// <summary>只有 .c / .h 才检查；.cpp / .cs 等一律放过。</summary>
        public static bool IsCTarget(ITextBuffer buffer)
        {
            ITextDocument document;
            if (buffer.Properties.TryGetProperty<ITextDocument>(typeof(ITextDocument), out document) && document != null)
            {
                string path = document.FilePath;
                if (!string.IsNullOrEmpty(path))
                {
                    string ext = Path.GetExtension(path).ToLowerInvariant();
                    return ext == ".c" || ext == ".h";
                }
            }
            return false;   // 没保存的新文件不检查，免得写别的语言时也冒红线
        }
    }
}
