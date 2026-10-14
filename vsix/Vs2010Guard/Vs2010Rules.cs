using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace Vs2010Guard
{
    /// <summary>一条发现。Start/Length 是相对整个缓冲区的字符偏移。</summary>
    internal sealed class Finding
    {
        public readonly int Start;
        public readonly int Length;
        public readonly string Kind;
        public readonly string Message;
        public readonly bool IsError;   // true = VS2010 直接编不过；false = 能编过但结果不对/链接失败

        public Finding(int start, int length, string kind, string message, bool isError)
        {
            Start = start;
            Length = length;
            Kind = kind;
            Message = message;
            IsError = isError;
        }
    }

    /// <summary>
    /// 规则表。结论都用真实 VS2010 (VC10, cl.exe 16.00.30319.01) 编译验证过，
    /// 见 vs2010-kit/README.md 的“实测对照表”。改这里记得同步改 vs2010-c-scan.py。
    /// </summary>
    internal static class Vs2010Rules
    {
        /// <summary>VC10 真正没有的头文件 → fatal error C1083。</summary>
        internal static readonly Dictionary<string, string> MissingHeaders =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "stdbool.h",    "VC10 没有 <stdbool.h> → 用 int，或 Windows 的 BOOL/TRUE/FALSE（实测 fatal error C1083）" },
            { "inttypes.h",   "VC10 没有 <inttypes.h> → 打印 64 位整数用 %I64d（实测 fatal error C1083）" },
            { "complex.h",    "VC10 不支持复数" },
            { "tgmath.h",     "VC10 不支持" },
            { "stdalign.h",   "C11 头文件，VC10 没有" },
            { "stdnoreturn.h","C11 头文件，VC10 没有" },
            { "threads.h",    "C11 头文件，VC10 没有" },
            { "uchar.h",      "C11 头文件，VC10 没有" },
        };

        /// <summary>VC10 上编译能过（最多 C4013 警告）、但链接必定失败 LNK2019 的函数。</summary>
        internal static readonly Dictionary<string, string> MissingFunctions =
            new Dictionary<string, string>
        {
            { "snprintf",   "_snprintf（注意不保证补 '\\0'）或 sprintf_s" },
            { "vsnprintf",  "_vsnprintf 或 vsprintf_s" },
            { "strndup",    "没有；自己 malloc + memcpy" },
            { "strtok_r",   "strtok_s" },
            { "strerror_r", "strerror_s" },
            { "getline",    "没有；用 fgets" },
            { "getdelim",   "没有；用 fgets" },
            { "round",      "没有；自己写 (int)(x + 0.5)" },
            { "roundf",     "没有；自己写 (int)(x + 0.5f)" },
            { "trunc",      "没有；用 (int) 强制转换" },
            { "truncf",     "没有；用 (int) 强制转换" },
            { "fmax",       "没有；用 (a > b ? a : b)" },
            { "fmin",       "没有；用 (a < b ? a : b)" },
            { "isnan",      "用 _isnan()（在 <float.h> 里）" },
            { "isinf",      "没有；用 !_finite(x) && !_isnan(x)" },
            { "signbit",    "没有；用 x < 0 判断" },
            { "copysign",   "_copysign" },
            { "log2",       "没有；用 log(x) / log(2.0)" },
            { "exp2",       "没有；用 pow(2.0, x)" },
            { "cbrt",       "没有；用 pow(x, 1.0 / 3.0)" },
            { "hypot",      "_hypot" },
            { "fma",        "没有" },
            { "nearbyint",  "没有" },
            { "rint",       "没有" },
            { "remainder",  "没有" },
            { "lgamma",     "没有" },
            { "tgamma",     "没有" },
            { "erf",        "没有" },
            { "erfc",       "没有" },
            { "nextafter",  "_nextafter" },
            { "scalbn",     "_scalbn" },
            { "ilogb",      "_logb" },
            { "fpclassify", "_fpclass" },
        };

        /// <summary>名字是旧 POSIX 名，VC10 能用但会报 C4996 警告。</summary>
        internal static readonly Dictionary<string, string> SoftFunctions =
            new Dictionary<string, string>
        {
            { "strdup", "实测能用（只是 C4996 警告）；要消警告就写 _strdup" },
        };

        /// <summary>编译能过，但 VC10 的 printf/scanf 运行时不认这些格式符 → 输出不对。</summary>
        internal static readonly string[,] SoftFormats = new string[,]
        {
            { "%zu",  "%Iu（实测 %zu 会原样打印出 \"zu\"）" },
            { "%zd",  "%Id" },
            { "%hhd", "%d" },
            { "%jd",  "%I64d" },
            { "%td",  "%Id" },
        };

        internal static readonly Dictionary<string, string> Keywords =
            new Dictionary<string, string>
        {
            { "_Bool",          "C99 关键字，VC10 不认识 → 用 int（实测 error C2065）" },
            { "_Complex",       "C99 关键字，VC10 不认识" },
            { "_Imaginary",     "C99 关键字，VC10 不认识" },
            { "_Static_assert", "C11，VC10 不认识（会被当成函数调用，链接时报 LNK2019）" },
            { "_Generic",       "C11，VC10 不认识" },
            { "_Noreturn",      "C11，VC10 不认识" },
            { "_Alignas",       "C11，VC10 不认识 → 用 __declspec(align(n))" },
            { "_Alignof",       "C11，VC10 不认识 → 用 __alignof" },
            { "_Atomic",        "C11，VC10 不认识 → 用 InterlockedXxx" },
            { "_Thread_local",  "C11，VC10 不认识 → 用 __declspec(thread)" },
        };

        private static readonly HashSet<string> TypeWords = new HashSet<string>(StringComparer.Ordinal)
        {
            "void","char","short","int","long","float","double","signed","unsigned",
            "const","volatile","static","extern","register","auto","struct","union","enum",
            "size_t","wchar_t","time_t","clock_t","va_list","FILE","BOOL","bool",
            "__int8","__int16","__int32","__int64","DWORD","WORD","BYTE","HWND","HANDLE",
        };

        private static readonly HashSet<string> NotDecl = new HashSet<string>(StringComparer.Ordinal)
        {
            "return","if","for","while","switch","do","else","break","continue",
            "goto","case","default","sizeof","typedef",
        };

        private static readonly Regex DeclRe = new Regex(
            @"^(?:(?:const|volatile|static|extern|register|auto|signed|unsigned|short|long)\s+)*([A-Za-z_]\w*)",
            RegexOptions.Compiled);

        private static readonly Regex CallRe = new Regex(@"^([A-Za-z_]\w*)\s*\(", RegexOptions.Compiled);

        private static readonly Regex InitEqRe = new Regex(@"(?<![=!<>])=(?!=)", RegexOptions.Compiled);

        private static readonly Regex StringRe = new Regex("\"((?:[^\"\\\\\n]|\\\\.)*)\"", RegexOptions.Compiled);

        private static readonly Regex IncludeRe = new Regex(@"#\s*include\s*[<""]([^>""]+)[>""]", RegexOptions.Compiled);

        private static readonly Regex VlaRe = new Regex(@"\[\s*[A-Za-z_]\w*\s*\]", RegexOptions.Compiled);

        private static readonly Regex CompoundLiteralRe =
            new Regex(@"\(\s*(struct\s+\w+|union\s+\w+|[A-Za-z_]\w*_t|int|char|float|double)\s*\)\s*\{", RegexOptions.Compiled);

        private static readonly Regex DesignatedInitRe =
            new Regex(@"\.\s*[A-Za-z_]\w*\s*=|\[\s*[^\]]+\s*\]\s*=", RegexOptions.Compiled);

        /// <summary>for (int i = 0; ...) —— VC10 的 C 里是硬错误 C2143</summary>
        private static readonly Regex ForDeclRe = new Regex(
            @"\bfor\s*\(\s*(?:const\s+|unsigned\s+|signed\s+|long\s+|short\s+)*(?:int|char|float|double|long|short|size_t|struct\s+\w+|[A-Za-z_]\w*_t)\s*\**\s*[A-Za-z_]\w*\s*[=;,)]",
            RegexOptions.Compiled);

        // ---------------- 入口 ----------------

        public static List<Finding> Scan(string text)
        {
            List<Finding> found = new List<Finding>();
            string clean = Blank(text);

            ScanStatements(StripDirectives(clean), found);

            ScanMatches(clean, IncludeRe, found, "头文件", true, delegate(string name)
            {
                string hint;
                if (MissingHeaders.TryGetValue(System.IO.Path.GetFileName(name), out hint))
                    return "<" + name + "> " + hint;
                return null;
            });

            ScanMatches(clean, new Regex(@"\binline\b", RegexOptions.Compiled), found, "关键字", true,
                delegate(string s) { return "C99 的 inline：VC10 的 C 里要用 __inline（实测 error C2054）"; });
            ScanMatches(clean, new Regex(@"\brestrict\b", RegexOptions.Compiled), found, "关键字", true,
                delegate(string s) { return "C99 的 restrict：VC10 里要用 __restrict（实测 error C2146）"; });

            foreach (KeyValuePair<string, string> kv in Keywords)
            {
                string word = kv.Key;
                ScanMatches(clean, new Regex(@"\b" + Regex.Escape(word) + @"\b", RegexOptions.Compiled),
                    found, "关键字", true, delegate(string s) { return word + " —— " + kv.Value; });
            }
            foreach (KeyValuePair<string, string> kv in MissingFunctions)
            {
                string name = kv.Key;
                ScanMatches(clean, new Regex(@"\b" + Regex.Escape(name) + @"\s*\(", RegexOptions.Compiled),
                    found, "函数", true,
                    delegate(string s) { return name + "() 在 VC10 上没有：编译只给 C4013 警告，链接报 LNK2019 → " + kv.Value; });
            }
            foreach (KeyValuePair<string, string> kv in SoftFunctions)
            {
                string name = kv.Key;
                ScanMatches(clean, new Regex(@"\b" + Regex.Escape(name) + @"\s*\(", RegexOptions.Compiled),
                    found, "函数", false, delegate(string s) { return name + "()：" + kv.Value; });
            }

            ScanFormats(text, found);
            ScanReverse(clean, found);

            found.Sort(delegate(Finding a, Finding b) { return a.Start.CompareTo(b.Start); });
            return Dedupe(found);
        }

        /// <summary>
        /// 文件级检查：非 ASCII 且不带 BOM 的 UTF-8 源文件，VS2010 会按代码页 936 读，
        /// 报 warning C4819、中文注释还可能乱码。和 vs2010-c-scan.py 的判断完全一致。
        /// </summary>
        public static void ScanEncoding(string path, List<Finding> found)
        {
            try
            {
                if (string.IsNullOrEmpty(path) || !System.IO.File.Exists(path)) return;

                byte[] bytes = System.IO.File.ReadAllBytes(path);
                if (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF) return;

                bool nonAscii = false;
                for (int i = 0; i < bytes.Length; i++)
                {
                    if (bytes[i] > 127) { nonAscii = true; break; }
                }
                if (!nonAscii) return;

                try
                {
                    new UTF8Encoding(false, true).GetString(bytes);
                }
                catch (DecoderFallbackException)
                {
                    return;   // 不是 UTF-8（多半是 GBK），2010 反而正常
                }

                found.Insert(0, new Finding(0, 1, "编码",
                    "文件是非 ASCII 且不带 BOM 的 UTF-8：VS2010 会按 GBK 读，中文注释/字符串会乱码，" +
                    "还可能报 warning C4819 → 另存为「UTF-8 带签名」或 GBK", false));
            }
            catch (Exception)
            {
                // 读不到文件就不检查
            }
        }

        // ---------------- 按“语句”扫描（一行写多条也能查出来） ----------------

        /// <summary>把预处理指令整行清空，剩下的才是真正的 C 代码。</summary>
        private static string StripDirectives(string clean)
        {
            string[] lines = clean.Split('\n');
            StringBuilder sb = new StringBuilder(clean.Length);
            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i];
                if (line.TrimStart().StartsWith("#", StringComparison.Ordinal))
                {
                    for (int k = 0; k < line.Length; k++) sb.Append(' ');
                }
                else
                {
                    sb.Append(line);
                }
                if (i < lines.Length - 1) sb.Append('\n');
            }
            return sb.ToString();
        }

        private static void ScanStatements(string code, List<Finding> found)
        {
            int depth = 0;
            int paren = 0;
            int start = 0;
            HashSet<int> seenStmt = new HashSet<int>();
            List<bool> initStack = new List<bool>();

            for (int i = 0; i < code.Length; i++)
            {
                char c = code[i];
                if (c == '(') { paren++; continue; }
                if (c == ')') { if (paren > 0) paren--; continue; }
                if (paren > 0) continue;

                if (c == ';')
                {
                    ProcessStatement(code, start, i, depth, seenStmt, found);
                    start = i + 1;
                }
                else if (c == '{')
                {
                    bool outerInit = initStack.Count > 0 && initStack[initStack.Count - 1];
                    string head = code.Substring(start, i - start);
                    string h = head.TrimStart();
                    bool isInit = outerInit || (InitEqRe.IsMatch(head) &&
                        !h.StartsWith("if") && !h.StartsWith("for") && !h.StartsWith("while") &&
                        !h.StartsWith("switch") && !h.StartsWith("else") && !h.StartsWith("do"));
                    // 头部无论如何都按“声明/语句”过一遍（struct P p = {...} 的声明位置就靠这一步）
                    ProcessStatement(code, start, i, depth, seenStmt, found);
                    if (isInit)
                    {
                        // 复合字面量 (struct P){1,2} 的 { 也算初始化器大括号，得单独查一下
                        Match clit = CompoundLiteralRe.Match(head + "{");
                        if (clit.Success)
                        {
                            found.Add(new Finding(start + clit.Index, clit.Length, "复合字面量",
                                "C99 的 (类型){...} 复合字面量，VC10 报 error C2059", true));
                        }
                    }
                    initStack.Add(isInit);
                    depth++;
                    start = i + 1;
                }
                else if (c == '}')
                {
                    bool outerInit = initStack.Count > 0 && initStack[initStack.Count - 1];
                    if (outerInit) ProcessInitializer(code, start, i, found);
                    else ProcessStatement(code, start, i, depth, seenStmt, found);

                    if (initStack.Count > 0) initStack.RemoveAt(initStack.Count - 1);
                    if (depth > 0)
                    {
                        seenStmt.Remove(depth);
                        depth--;
                    }
                    start = i + 1;
                }
            }
        }

        private static void ProcessStatement(string code, int start, int end, int depth,
                                             HashSet<int> seenStmt, List<Finding> found)
        {
            if (end <= start) return;
            string raw = code.Substring(start, end - start);
            string s = raw.Trim();
            if (s.Length == 0) return;
            int abs = start + (raw.Length - raw.TrimStart().Length);

            Match fm = ForDeclRe.Match(s);
            if (fm.Success)
            {
                found.Add(new Finding(abs + fm.Index, fm.Length, "for 里声明",
                    "C89 不允许在 for 括号里声明变量；VS2010 报 error C2143 → 把 int i; 提到外面，写 for (i = 0; ...)", true));
            }

            Match cl = CompoundLiteralRe.Match(s);
            if (cl.Success)
            {
                found.Add(new Finding(abs + cl.Index, cl.Length, "复合字面量",
                    "C99 的 (类型){...} 复合字面量，VC10 报 error C2059", true));
            }

            if (depth < 1) return;

            bool isDecl = IsDeclaration(s);
            if (isDecl && seenStmt.Contains(depth) && !s.StartsWith("typedef", StringComparison.Ordinal))
            {
                found.Add(new Finding(abs, s.Length, "声明位置",
                    "C89 要求变量声明写在所有语句之前；VS2010 报 error C2143 / C2065 → 把 [" + Clip(s) +
                    "] 挪到这个 {} 块的开头", true));
            }

            if (isDecl)
            {
                Match vla = VlaRe.Match(s);
                if (vla.Success)
                {
                    found.Add(new Finding(abs + vla.Index, vla.Length, "变长数组",
                        "VLA 是 C99；VC10 报 error C2057/C2466/C2133 → 用 #define 常量或 malloc", true));
                }
            }
            else if (!s.StartsWith("case", StringComparison.Ordinal) &&
                     !s.StartsWith("default", StringComparison.Ordinal) &&
                     !s.StartsWith("else", StringComparison.Ordinal) &&
                     !s.StartsWith("goto", StringComparison.Ordinal) &&
                     !s.StartsWith("}", StringComparison.Ordinal))
            {
                seenStmt.Add(depth);
            }
        }

        /// <summary>初始化器大括号里的内容：找 C99 的 .成员= / [下标]=</summary>
        private static void ProcessInitializer(string code, int start, int end, List<Finding> found)
        {
            if (end <= start) return;
            string raw = code.Substring(start, end - start);
            Match di = DesignatedInitRe.Match(raw);
            if (di.Success)
            {
                found.Add(new Finding(start + di.Index, di.Length, "指定初始化器",
                    "C99 的 .成员= / [下标]= 初始化，VC10 报 error C2059 → 按顺序逐个赋值", true));
            }
        }

        private static bool IsDeclaration(string s)
        {
            Match m = DeclRe.Match(s);
            if (!m.Success) return false;

            string first = m.Groups[1].Value;
            if (NotDecl.Contains(first)) return false;

            Match call = CallRe.Match(s);
            if (call.Success && !TypeWords.Contains(first)) return false;   // 形如 foo(...) 的函数调用

            return TypeWords.Contains(first) ||
                   first.EndsWith("_t", StringComparison.Ordinal) ||
                   (first.Length > 1 && char.IsUpper(first[0]) && first != "NULL");
        }

        // ---------------- 字符串字面量的格式符 ----------------

        private static void ScanFormats(string text, List<Finding> found)
        {
            foreach (Match m in StringRe.Matches(text))
            {
                string lit = m.Groups[1].Value;
                if (lit.IndexOf('%') < 0) continue;
                for (int k = 0; k < SoftFormats.GetLength(0); k++)
                {
                    string bad = SoftFormats[k, 0];
                    int at = lit.IndexOf(bad, StringComparison.Ordinal);
                    if (at < 0) continue;
                    found.Add(new Finding(m.Groups[1].Index + at, bad.Length, "格式符",
                        "能编过，但 VC10 的 printf/scanf 不认 [" + bad + "] → 用 " + SoftFormats[k, 1], false));
                    break;
                }
            }
        }

        // ---------------- 反向：2010 能过、新版编不过 ----------------

        private static void ScanReverse(string clean, List<Finding> found)
        {
            ScanMatches(clean, new Regex(@"\bgets\s*\(", RegexOptions.Compiled), found, "反向兼容", false,
                delegate(string s) { return "gets() 在 VS2015 起已被移除：2010 能编（C4996 警告），2026 编不过 → 用 fgets"; });
            ScanMatches(clean, new Regex(@"\bvoid\s+main\s*\(", RegexOptions.Compiled), found, "反向兼容", false,
                delegate(string s) { return "void main() 不是标准写法：2010 放过（实测 0 警告），新版会警告 → 用 int main(void)"; });
        }

        // ---------------- 工具 ----------------

        private static void ScanMatches(string clean, Regex re, List<Finding> found, string kind, bool isError,
                                        Converter<string, string> describe)
        {
            foreach (Match m in re.Matches(clean))
            {
                string subject = (m.Groups.Count > 1 && m.Groups[1].Success) ? m.Groups[1].Value : m.Value;
                string msg = describe(subject);
                if (msg != null)
                    found.Add(new Finding(m.Index, m.Length, kind, msg, isError));
            }
        }

        private static List<Finding> Dedupe(List<Finding> list)
        {
            List<Finding> outList = new List<Finding>();
            HashSet<string> seen = new HashSet<string>();
            foreach (Finding f in list)
            {
                string key = f.Start + "|" + f.Kind;
                if (seen.Add(key)) outList.Add(f);
            }
            return outList;
        }

        private static string Clip(string s)
        {
            return s.Length <= 40 ? s : s.Substring(0, 40) + "...";
        }

        /// <summary>把注释和字符串内容替换成空格，长度不变，这样偏移量可以直接用。</summary>
        private static string Blank(string text)
        {
            StringBuilder sb = new StringBuilder(text.Length);
            int i = 0, n = text.Length, state = 0; // 0=代码 1=行注释 2=块注释 3=字符串 4=字符
            while (i < n)
            {
                char c = text[i];
                char nx = i + 1 < n ? text[i + 1] : '\0';
                if (state == 0)
                {
                    if (c == '/' && nx == '/') { state = 1; sb.Append("  "); i += 2; continue; }
                    if (c == '/' && nx == '*') { state = 2; sb.Append("  "); i += 2; continue; }
                    if (c == '"') { state = 3; sb.Append('"'); i++; continue; }
                    if (c == '\'') { state = 4; sb.Append('\''); i++; continue; }
                    sb.Append(c); i++; continue;
                }
                if (state == 1)
                {
                    if (c == '\n') { state = 0; sb.Append('\n'); } else sb.Append(' ');
                    i++; continue;
                }
                if (state == 2)
                {
                    if (c == '*' && nx == '/') { state = 0; sb.Append("  "); i += 2; continue; }
                    sb.Append(c == '\n' ? '\n' : ' '); i++; continue;
                }
                if (c == '\\') { sb.Append("  "); i += 2; continue; }
                if ((state == 3 && c == '"') || (state == 4 && c == '\'')) { state = 0; sb.Append(c); i++; continue; }
                sb.Append(c == '\n' ? '\n' : ' '); i++;
            }
            return sb.ToString();
        }
    }
}
