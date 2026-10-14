#!/usr/bin/env python3
# -*- coding: utf-8 -*-
r"""
vs2010-c-scan.py —— 检查 C 源码能不能在 Visual Studio 2010 (VC10 = C89 + 微软扩展) 下编译

背景：VC10 的 C 前端只到 C89；VS2019 16.7+ 才有 /std:c11，16.8 才有 /std:c17。
      所以 VS2026 上能编的 C 代码，很多写法在 VS2010 上是硬错误。

用法：
    python vs2010-c-scan.py demo.c               # 检查单个文件
    python vs2010-c-scan.py a.c b.h              # 检查多个文件
    python vs2010-c-scan.py -r .\src             # 递归检查目录
    python vs2010-c-scan.py demo.c --quiet       # 只显示错误，不显示可疑项

退出码：0 = 没发现问题；1 = 有“错误”级问题。
"""

import argparse
import os
import re
import sys

if hasattr(sys.stdout, "reconfigure"):
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")

ERR = "错误"
WARN = "可疑"

# ---------- 规则表 ----------

# 实测（VC10 cl.exe 14.00.50727）：这些头文件 VC10 真的没有 → fatal error C1083
C99_HEADERS = {
    "stdbool.h":  "VC10 没有 <stdbool.h> → 用 int，或 Windows 的 BOOL/TRUE/FALSE（实测 fatal error C1083）",
    "inttypes.h": "VC10 没有 <inttypes.h> → 打印 64 位整数用 %I64d / %I64u（实测 fatal error C1083）",
    "complex.h":  "VC10 不支持复数",
    "tgmath.h":   "VC10 不支持",
    "stdalign.h": "C11 头文件，VC10 没有",
    "stdnoreturn.h": "C11 头文件，VC10 没有",
    "threads.h":  "C11 头文件，VC10 没有",
    "uchar.h":    "C11 头文件，VC10 没有",
}

# C99 / C11 才有的函数：编译只给 C4013 警告，链接必定失败 LNK2019
C99_FUNCS = {
    "snprintf":   "_snprintf（注意它不保证补 '\\0'）或 sprintf_s",
    "vsnprintf":  "_vsnprintf 或 vsprintf_s",
    "strndup":    "没有；自己 malloc + memcpy",
    "strtok_r":   "strtok_s",
    "strerror_r": "strerror_s",
    "getline":    "没有；用 fgets",
    "getdelim":   "没有；用 fgets",
    "round":      "没有；自己写 (int)(x + 0.5)",
    "roundf":     "没有；自己写 (int)(x + 0.5f)",
    "trunc":      "没有；用 (int) 强制转换",
    "truncf":     "没有；用 (int) 强制转换",
    "fmax":       "没有；用 (a > b ? a : b)",
    "fmin":       "没有；用 (a < b ? a : b)",
    "isnan":      "用 _isnan()（在 <float.h> 里）",
    "isinf":      "没有；用 !_finite(x) && !_isnan(x) 组合判断",
    "signbit":    "没有；用 x < 0 或 _copysign 判断",
    "copysign":   "_copysign",
    "log2":       "没有；用 log(x) / log(2.0)",
    "exp2":       "没有；用 pow(2.0, x)",
    "cbrt":       "没有；用 pow(x, 1.0 / 3.0)",
    "hypot":      "_hypot",
    "fma":        "没有",
    "nearbyint":  "没有",
    "rint":       "没有",
    "remainder":  "没有",
    "lgamma":     "没有",
    "tgamma":     "没有",
    "erf":        "没有",
    "erfc":       "没有",
    "nextafter":  "_nextafter",
    "scalbn":     "_scalbn",
    "ilogb":      "_logb",
    "fpclassify": "_fpclass",
}

# 实测能用、只是会报 C4996 的旧 POSIX 名字
SOFT_FUNCS = {
    "strdup": "实测能用（只是 C4996 警告）；要消警告就写 _strdup",
}

# 能编过，但 VC10 的 printf/scanf 运行时不认 → 输出不对（实测 %zu 原样打印出 "zu"）
SOFT_FORMATS = [
    ("%zu",  '%Iu（实测 %zu 会原样打印出 "zu"）'),
    ("%zd",  "%Id"),
    ("%hhd", "%d"),
    ("%jd",  "%I64d"),
    ("%td",  "%Id"),
]

# for (int i = 0; ...) —— VC10 的 C 里是硬错误 C2143
FOR_DECL_RE = re.compile(
    r"\bfor\s*\(\s*(?:const\s+|unsigned\s+|signed\s+|long\s+|short\s+)*"
    r"(?:int|char|float|double|long|short|size_t|struct\s+\w+|[A-Za-z_]\w*_t)\s*\**[A-Za-z_]\w*\s*[=;,)]"
)

C99_KEYWORDS = {
    "_Bool":          "C99 关键字，VC10 不认识 → 用 int",
    "_Complex":       "C99 关键字，VC10 不认识",
    "_Imaginary":     "C99 关键字，VC10 不认识",
    "_Static_assert": "C11，VC10 不认识 → 用 typedef char chk[(expr)?1:-1] 或 #if",
    "_Generic":       "C11，VC10 不认识",
    "_Noreturn":      "C11，VC10 不认识",
    "_Alignas":       "C11，VC10 不认识 → 用 __declspec(align(n))",
    "_Alignof":       "C11，VC10 不认识 → 用 __alignof",
    "_Atomic":        "C11，VC10 不认识 → 用 InterlockedXxx",
    "_Thread_local":  "C11，VC10 不认识 → 用 __declspec(thread)",
}

TYPE_WORDS = {
    "void", "char", "short", "int", "long", "float", "double", "signed", "unsigned",
    "const", "volatile", "static", "extern", "register", "auto",
    "struct", "union", "enum",
    "size_t", "wchar_t", "time_t", "clock_t", "va_list", "FILE", "BOOL", "bool",
    "__int8", "__int16", "__int32", "__int64", "DWORD", "WORD", "BYTE", "HWND", "HANDLE",
}

DECL_RE = re.compile(
    r"^(?:(?:const|volatile|static|extern|register|auto|signed|unsigned|short|long)\s+)*"
    r"([A-Za-z_]\w*)"
)
CALL_RE = re.compile(r"^([A-Za-z_]\w*)\s*\(")


# ---------- 预处理：去掉注释和字符串，保留行号 ----------

def strip_comments_strings(text):
    out = []
    i, n = 0, len(text)
    state = None
    while i < n:
        c = text[i]
        nxt = text[i + 1] if i + 1 < n else ""
        if state is None:
            if c == "/" and nxt == "/":
                state = "line"; out.append("  "); i += 2; continue
            if c == "/" and nxt == "*":
                state = "block"; out.append("  "); i += 2; continue
            if c == '"':
                state = "str"; out.append('"'); i += 1; continue
            if c == "'":
                state = "chr"; out.append("'"); i += 1; continue
            out.append(c); i += 1; continue
        if state == "line":
            out.append("\n" if c == "\n" else " ")
            if c == "\n":
                state = None
            i += 1; continue
        if state == "block":
            if c == "*" and nxt == "/":
                state = None; out.append("  "); i += 2; continue
            out.append("\n" if c == "\n" else " "); i += 1; continue
        if c == "\\":
            out.append("  "); i += 2; continue
        if (state == "str" and c == '"') or (state == "chr" and c == "'"):
            state = None; out.append(c); i += 1; continue
        out.append("\n" if c == "\n" else " "); i += 1
    return "".join(out)


def find_string_literals(text):
    """返回 [(行号, 字符串内容)]，用于检查格式符"""
    res = []
    for m in re.finditer(r'"((?:[^"\\\n]|\\.)*)"', text):
        line = text.count("\n", 0, m.start()) + 1
        res.append((line, m.group(1)))
    return res


# ---------- 按“语句”扫描（与插件版 Vs2010Rules.cs 是同一套算法） ----------

NOT_DECL = {
    "return", "if", "for", "while", "switch", "do", "else", "break", "continue",
    "goto", "case", "default", "sizeof", "typedef",
}

INIT_EQ_RE = re.compile(r"(?<![=!<>])=(?!=)")
VLA_RE = re.compile(r"\[\s*[A-Za-z_]\w*\s*\]")
COMPOUND_LITERAL_RE = re.compile(
    r"\(\s*(struct\s+\w+|union\s+\w+|[A-Za-z_]\w*_t|int|char|float|double)\s*\)\s*\{")
DESIGNATED_INIT_RE = re.compile(r"\.\s*[A-Za-z_]\w*\s*=|\[\s*[^\]]+\s*\]\s*=")


def strip_directives(clean):
    """把预处理指令整行清空，剩下的才是真正的 C 代码。"""
    out = []
    for line in clean.split("\n"):
        out.append(" " * len(line) if line.lstrip().startswith("#") else line)
    return "\n".join(out)


def is_declaration(s):
    m = DECL_RE.match(s)
    if not m:
        return False
    first = m.group(1)
    if first in NOT_DECL:
        return False
    call = CALL_RE.match(s)
    if call and first not in TYPE_WORDS:
        return False       # 形如 foo(...) 的函数调用
    return (first in TYPE_WORDS or first.endswith("_t")
            or (len(first) > 1 and first[0].isupper() and first != "NULL"))


def scan_statements(code, findings, line_of):
    depth = 0
    paren = 0
    start = 0
    seen_stmt = set()
    init_stack = []

    for i, c in enumerate(code):
        if c == "(":
            paren += 1
            continue
        if c == ")":
            if paren > 0:
                paren -= 1
            continue
        if paren > 0:
            continue

        if c == ";":
            _process_statement(code, start, i, depth, seen_stmt, findings, line_of)
            start = i + 1
        elif c == "{":
            outer_init = bool(init_stack) and init_stack[-1]
            head = code[start:i]
            h = head.lstrip()
            is_init = outer_init or (
                INIT_EQ_RE.search(head) is not None
                and not h.startswith(("if", "for", "while", "switch", "else", "do"))
            )
            # 头部无论如何都按“声明/语句”过一遍（struct P p = {...} 的声明位置就靠这一步）
            _process_statement(code, start, i, depth, seen_stmt, findings, line_of)
            if is_init:
                # 复合字面量 (struct P){1,2} 的 { 也算初始化器大括号，得单独查一下
                m = COMPOUND_LITERAL_RE.search(head + "{")
                if m:
                    findings.append((line_of(start + m.start()), ERR, "复合字面量",
                                     "C99 的 (类型){...} 复合字面量，VC10 报 error C2059"))
            init_stack.append(is_init)
            depth += 1
            start = i + 1
        elif c == "}":
            outer_init = bool(init_stack) and init_stack[-1]
            if outer_init:
                m = DESIGNATED_INIT_RE.search(code[start:i])
                if m:
                    findings.append((line_of(start + m.start()), ERR, "指定初始化器",
                                     "C99 的 .成员= / [下标]= 初始化，VC10 报 error C2059 → 按顺序逐个赋值"))
            else:
                _process_statement(code, start, i, depth, seen_stmt, findings, line_of)
            if init_stack:
                init_stack.pop()
            if depth > 0:
                seen_stmt.discard(depth)
                depth -= 1
            start = i + 1


def _process_statement(code, start, end, depth, seen_stmt, findings, line_of):
    if end <= start:
        return
    raw = code[start:end]
    s = raw.strip()
    if not s:
        return
    abs_off = start + (len(raw) - len(raw.lstrip()))

    m = FOR_DECL_RE.search(s)
    if m:
        findings.append((line_of(abs_off + m.start()), ERR, "for 里声明",
                         "C89 不允许在 for 括号里声明变量；VS2010 报 error C2143 → "
                         "把 int i; 提到外面，写 for (i = 0; ...)"))

    m = COMPOUND_LITERAL_RE.search(s)
    if m:
        findings.append((line_of(abs_off + m.start()), ERR, "复合字面量",
                         "C99 的 (类型){...} 复合字面量，VC10 报 error C2059"))

    if depth < 1:
        return

    decl = is_declaration(s)
    if decl and depth in seen_stmt and not s.startswith("typedef"):
        findings.append((line_of(abs_off), ERR, "声明位置",
                         "C89 要求变量声明写在所有语句之前；VS2010 报 error C2143 / C2065 → "
                         "把 \"%s\" 挪到这个 {} 块的开头"
                         % (s if len(s) <= 40 else s[:40] + "...")))

    if decl:
        m = VLA_RE.search(s)
        if m:
            findings.append((line_of(abs_off + m.start()), ERR, "变长数组",
                             "VLA 是 C99；VC10 报 error C2057/C2466/C2133 → 用 #define 常量或 malloc"))
    elif not s.startswith(("case", "default", "else", "goto", "}")):
        seen_stmt.add(depth)


# ---------- 检查 ----------

def scan_file(path, quiet=False):
    with open(path, "rb") as f:
        raw = f.read()

    findings = []

    # 1) 文件编码：VS2010 默认按系统代码页(GBK)读源文件
    bom = raw.startswith(b"\xef\xbb\xbf")
    has_nonascii = any(b > 127 for b in raw)
    if has_nonascii and not bom:
        try:
            raw.decode("utf-8")
            findings.append((1, WARN, "编码",
                             "文件是非 ASCII 且不带 BOM 的 UTF-8：VS2010 会按 GBK 读，中文注释/字符串会乱码，"
                             "还可能报 warning C4819 → 另存为「UTF-8 带签名」或 GBK"))
        except UnicodeDecodeError:
            pass  # 多半是 GBK，2010 反而正常

    text = raw.decode("utf-8-sig", errors="replace")
    clean = strip_comments_strings(text)
    orig_lines = text.split("\n")
    clean_lines = clean.split("\n")

    # 2) 头文件
    for idx, line in enumerate(clean_lines):
        m = re.search(r"#\s*include\s*[<\"]([^>\"]+)[>\"]", line)
        if m:
            h = os.path.basename(m.group(1)).lower()
            if h in C99_HEADERS:
                findings.append((idx + 1, ERR, "头文件",
                                 "<%s> %s" % (m.group(1), C99_HEADERS[h])))

    # 3) C99/C11 关键字
    for idx, line in enumerate(clean_lines):
        for kw, hint in C99_KEYWORDS.items():
            if re.search(r"\b%s\b" % re.escape(kw), line):
                findings.append((idx + 1, ERR, "关键字", "%s —— %s" % (kw, hint)))
        if re.search(r"\brestrict\b", line):
            findings.append((idx + 1, ERR, "关键字",
                             "restrict 是 C99 → VC10 里写 __restrict（实测 error C2146）"))
        if re.search(r"\binline\b", line) and "__inline" not in line:
            findings.append((idx + 1, ERR, "关键字",
                             "inline 是 C99 → VC10 的 C 里写 __inline（实测 error C2054）"))

    # 4) C99 函数
    whole = "\n".join(clean_lines)
    for name, hint in C99_FUNCS.items():
        for m in re.finditer(r"\b%s\s*\(" % re.escape(name), whole):
            ln = whole.count("\n", 0, m.start()) + 1
            findings.append((ln, ERR, "函数",
                             "%s() 在 VC10 上没有：编译只给 C4013 警告，链接报 LNK2019 → %s" % (name, hint)))
    for name, hint in SOFT_FUNCS.items():
        for m in re.finditer(r"\b%s\s*\(" % re.escape(name), whole):
            ln = whole.count("\n", 0, m.start()) + 1
            findings.append((ln, WARN, "函数", "%s()：%s" % (name, hint)))

    # 5) 格式符（只看字符串字面量）
    for ln, lit in find_string_literals(text):
        if "%" not in lit:
            continue
        for bad, good in SOFT_FORMATS:
            if bad in lit:
                findings.append((ln, WARN, "格式符",
                                 "能编过，但 VC10 的 printf/scanf 不认 \"%s\" → 用 %s" % (bad, good)))

    # 6) 声明位置 / for 里声明 / VLA / 指定初始化器 / 复合字面量
    #    按“语句”扫描（与插件版 Vs2010Rules.cs 完全同一套算法）
    line_starts = [0]
    for i, ch in enumerate(text):
        if ch == "\n":
            line_starts.append(i + 1)

    def line_of(offset):
        import bisect
        return bisect.bisect_right(line_starts, offset)

    scan_statements(strip_directives(clean), findings, line_of)

    # 7) 反向：2010 能过、新版编不过的写法
    for idx, line in enumerate(clean_lines):
        if re.search(r"\bgets\s*\(", line):
            findings.append((idx + 1, WARN, "反向兼容",
                             "gets() 在 VS2015 起已被移除：2010 能编（带 C4996 警告），2026 直接编不过 → 用 fgets"))
        if re.search(r"\bvoid\s+main\s*\(", line):
            findings.append((idx + 1, WARN, "反向兼容",
                             "void main() 不是标准写法：2010 放过，新版会警告/报错 → 用 int main(void) 并 return 0"))

    findings.sort(key=lambda x: (x[0], x[1]))
    if quiet:
        findings = [f for f in findings if f[1] == ERR]
    return findings


def main():
    ap = argparse.ArgumentParser(description="检查 C 源码的 VS2010(VC10) 兼容性")
    ap.add_argument("paths", nargs="+", help="要检查的 .c/.h 文件或目录")
    ap.add_argument("-r", "--recursive", action="store_true", help="递归目录")
    ap.add_argument("-q", "--quiet", action="store_true", help="只报错误，不报可疑项")
    ap.add_argument("--msbuild", action="store_true",
                    help="按 MSBuild 格式输出，让错误直接进 Visual Studio 的错误列表")
    ap.add_argument("--msbuild-warn", dest="msbuild_warn", action="store_true",
                    help="同 --msbuild，但把错误降级成警告，这样构建不会被中断")
    args = ap.parse_args()

    files = []
    for p in args.paths:
        if os.path.isdir(p):
            for root, _, names in os.walk(p):
                for n in names:
                    if n.lower().endswith((".c", ".h")):
                        files.append(os.path.join(root, n))
                if not args.recursive:
                    break
        else:
            files.append(p)

    if not files:
        print("没找到 .c / .h 文件")
        return 0

    total_err = total_warn = 0
    codes = {}
    for f in files:
        findings = scan_file(f, args.quiet)
        n_err = sum(1 for x in findings if x[1] == ERR)
        n_warn = sum(1 for x in findings if x[1] == WARN)
        if args.msbuild or args.msbuild_warn:
            for ln, lvl, kind, msg in findings:
                if kind not in codes:
                    codes[kind] = "VS2010C%02d" % (len(codes) + 1)
                sev = "error" if (lvl == ERR and not args.msbuild_warn) else "warning"
                print("%s(%d): %s %s: [%s] %s" % (f, ln, sev, codes[kind], kind, msg))
            total_err += n_err
            total_warn += n_warn
            continue
        total_err += n_err
        total_warn += n_warn
        if not findings:
            print("[OK]   %s —— 没发现 VS2010 不兼容的写法" % f)
            continue
        print("[扫描] %s —— %d 个错误, %d 个可疑" % (f, n_err, n_warn))
        for ln, lvl, kind, msg in findings:
            print("   %s:%d  [%s][%s] %s" % (f, ln, lvl, kind, msg))
        print("")

    print("=" * 60)
    print("合计：%d 个错误，%d 个可疑" % (total_err, total_warn))
    if total_err:
        print("带「错误」标记的写法在 VS2010 上编不过，必须先改。")
    return 1 if total_err else 0


if __name__ == "__main__":
    sys.exit(main())
