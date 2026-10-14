#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
check-parity.py —— 验证「命令行扫描器(Python)」和「VS 插件(C#)」两套实现结果完全一致

用法：python check-parity.py [语料目录]
默认语料目录：samples/diff

它会：
  1) 编译 vsix/_rulecheck（就是插件用的那份 Vs2010Rules.cs）
  2) 用两个实现分别扫一遍语料
  3) 按「文件 + 行号 + 级别 + 类型」逐条比对
退出码：0 = 完全一致；1 = 有差异
"""

import os
import re
import subprocess
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
DEFAULT_CORPUS = os.path.join(HERE, "samples", "diff")
RULE_PROJ = os.path.join(HERE, "vsix", "_rulecheck", "RuleCheck.csproj")
RULE_EXE = os.path.join(HERE, "vsix", "_rulecheck", "bin", "Debug", "net472", "RuleCheck.exe")
SCANNER = os.path.join(HERE, "vs2010-c-scan.py")

PY_LINE = re.compile(r"^\s+(.+?):(\d+)\s+\[(错误|可疑)\]\[(.+?)\]")


def run(cmd):
    p = subprocess.run(cmd, capture_output=True, text=True, encoding="utf-8", errors="replace")
    return p.stdout or ""


def main():
    corpus = sys.argv[1] if len(sys.argv) > 1 else DEFAULT_CORPUS
    files = sorted(
        os.path.join(corpus, n) for n in os.listdir(corpus) if n.lower().endswith(".c")
    )
    if not files:
        print("语料目录里没有 .c 文件: " + corpus)
        return 1

    print("编译规则检查器 ...")
    build = subprocess.run(["dotnet", "build", RULE_PROJ, "/v:q", "/nologo"],
                           capture_output=True, text=True, encoding="utf-8", errors="replace")
    if not os.path.exists(RULE_EXE):
        print("编译失败：")
        print(build.stdout)
        print(build.stderr)
        return 1

    py_keys = set()
    for line in run([sys.executable, SCANNER] + files).splitlines():
        m = PY_LINE.match(line)
        if m:
            py_keys.add((os.path.basename(m.group(1)), int(m.group(2)),
                         "E" if m.group(3) == "错误" else "W", m.group(4)))

    cs_keys = set()
    for line in run([RULE_EXE] + files).splitlines():
        parts = line.strip().split("|")
        if len(parts) >= 4 and parts[1].isdigit():
            cs_keys.add((parts[0], int(parts[1]), parts[2], parts[3]))

    print("")
    print("命令行版 %d 条 / 插件版 %d 条" % (len(py_keys), len(cs_keys)))
    only_py = sorted(py_keys - cs_keys)
    only_cs = sorted(cs_keys - py_keys)
    for k in only_py:
        print("  只在命令行版: %s:%d [%s][%s]" % k)
    for k in only_cs:
        print("  只在插件版  : %s:%d [%s][%s]" % k)

    if not only_py and not only_cs:
        print("==> 两个实现完全一致（文件 / 行号 / 级别 / 类型 全对齐）")
        return 0
    print("==> 有差异，需要修")
    return 1


if __name__ == "__main__":
    sys.exit(main())
