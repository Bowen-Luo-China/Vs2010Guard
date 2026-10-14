using System;
using System.IO;

namespace Vs2010Guard
{
    /// <summary>
    /// 排查用日志：只有设置了环境变量 VS2010GUARD_LOG（指向一个 .log 文件）时才写。
    /// 平时完全静默，不影响编辑器性能。
    /// </summary>
    internal static class Vs2010GuardDiagnostics
    {
        private static readonly string LogPath = Environment.GetEnvironmentVariable("VS2010GUARD_LOG");
        private static readonly object Gate = new object();

        public static void Log(string message)
        {
            if (string.IsNullOrEmpty(LogPath)) return;
            try
            {
                lock (Gate)
                {
                    File.AppendAllText(LogPath,
                        DateTime.Now.ToString("HH:mm:ss.fff") + "  " + message + Environment.NewLine);
                }
            }
            catch (Exception)
            {
                // 日志失败绝不影响编辑器
            }
        }
    }
}
