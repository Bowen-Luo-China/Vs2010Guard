using System;
using System.Collections.Generic;
using System.IO;

namespace Vs2010Guard
{
    /// <summary>把插件用的同一套规则跑在文件上，输出 "文件|行号|E或W|类型|说明"，供对比脚本解析。</summary>
    internal static class RuleCheck
    {
        private static void Main(string[] args)
        {
            foreach (string path in args)
            {
                string text = File.ReadAllText(path);
                List<Finding> found = Vs2010Rules.Scan(text);
                Vs2010Rules.ScanEncoding(path, found);
                foreach (Finding f in found)
                {
                    int line = 1;
                    for (int i = 0; i < f.Start && i < text.Length; i++) if (text[i] == '\n') line++;
                    string msg = f.Message.Replace("|", "/").Replace("\r", " ").Replace("\n", " ");
                    Console.WriteLine(Path.GetFileName(path) + "|" + line + "|" + (f.IsError ? "E" : "W") + "|" + f.Kind + "|" + msg);
                }
            }
        }
    }
}
