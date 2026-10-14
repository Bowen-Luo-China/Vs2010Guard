# Vs2010Guard —— 在 VS2022/2026 里检查 VS2010(C89) 兼容性

> 课程要求交 VS2010 能编的作业，但你平时想用新版 Visual Studio 写？
> 装上这个插件，**VS2010 编不过的写法会在编辑器里直接标红线**（鼠标悬停给出真实报错号和改法），
> 绿线表示"能编过但结果不对"。默认对所有项目完全静默，要检查哪个项目一键开关。

**只想用、不想看源码：[下载 `Vs2010Guard-一键安装.zip`](Vs2010Guard-一键安装.zip) → 解压 → 双击「安装.bat」。**

- 支持 Visual Studio 2022 / 2026（64 位，用的是 VS2026 官方 VSIX 模板的写法）
- 规则表里的每一条都用**真实的 VS2010 cl.exe 编译对照验证**过（不是猜的）
- 许可证：[MIT](LICENSE)

> 下文出现的 `D:\vs2010-kit` 指你克隆/解压到的目录，按自己的实际位置替换即可。

---

## 这个项目解决什么问题


学校的考试/作业按 **VS2010** 的标准来，但你平时用的是 **VS2026**。
VS2010 的 C 前端只到 **C89 + 微软扩展**（VS 直到 2019 16.7 才有 `/std:c11`），
所以很多"2026 上跑得好好的"写法，拿到 2010 上直接编不过。

本机实测环境：
- **VS2026** Community 18.10.3 —— `D:\Application\VS\Production`
- **VS2010** 旗舰版 RTM（devenv 10.0.30319.1，cl.exe **16.00.30319.01**）—— `D:\Application\VS2010`

---

## 0. 五分钟上手：建 C 项目 → 开插件 → 运行

### 0.1 在 VS2026 里建一个 C 项目

VS 里**没有单独的"C 项目"模板**，用 C++ 的空项目就行——关键是**文件名后缀写 `.c`**（后缀决定按 C 还是 C++ 编译）。

1. **文件 → 新建 → 项目**
2. 搜索框输入 `空项目`，选 **空项目（Empty Project，C++）** → 下一步
3. 项目名比如 `homework`，位置选你的作业文件夹（**建议就放在你要交作业的那个目录里**）→ 创建
4. 右侧**解决方案资源管理器**里：右键 **源文件** → **添加 → 新建项**
5. 选 **C++ 文件(.cpp)**，把下面的名字**改成 `main.c`**（后缀一定要 .c）→ 添加
6. 写代码，然后 **Ctrl+F5** 运行

> 想确认是按 C 编译的：右键 .c 文件 → 属性 → **C/C++ → 高级 → 编译为**，应该显示"编译为 C 代码"。

### 0.2 让插件开始检查（默认是关的）

插件装好后就一直在，但**默认对所有项目都不检查**——只有目录里有 `vs2010check.on` 才生效。

- **方法 A（推荐）**：打开你的解决方案，点 **工具** 菜单 →「**开启 VS2010 兼容性检查（当前解决方案）**」，
  或者直接**右键解决方案节点**选同一项。开启后编辑器里的标记会立刻刷新。
- **方法 B**：把 `开启2010检查.bat` 复制到作业文件夹里双击。
- 不想要了：同一个菜单再点一次，或双击 `关闭2010检查.bat`。

开了之后，凡是 VS2010 编不过的地方就会标出来：**红线 = 2010 直接编不过**，**绿线 = 能编过但结果不对**，鼠标悬停看原因和改法。

### 0.3 现成的例子（不用自己建）

```
samples\c-project\homework.sln
```

这个示例项目已经开好了检查，里面的代码**在 VS2026 里能编译能运行**，但插件会标出 10 处 VS2010 会炸的写法：

| 行 | 写法 | VS2010 上 |
|---|---|---|
| 2 | `#include <stdbool.h>` | fatal error C1083 |
| 12 | `for (int i = 0; ...)` | error C2143 |
| 16/17/19/21 | 声明写在语句之后 | error C2143 / C2065 |
| 19 | `{ .score = 90 }` 指定初始化器 | error C2059 |
| 22 | `snprintf` | 链接 LNK2019 |
| 25 | `%zu` | 运行时会原样打印出 `zu` |

双击打开就能看到效果。

### 0.4 怎么运行程序

- **Ctrl+F5**（开始执行不调试）：最常用，程序跑完窗口会停住等你按回车，适合交作业演示。
- **F5**（开始调试）：带调试器，能打断点。
- 生成的 exe 在 `bin\x64\Debug\homework.exe`，双击也能跑。
- 如果报"无法启动程序"，先看下面的**输出窗口**是不是有编译错误（有错误就不会生成 exe）。

### 0.5 中文注释乱码 / warning C4819

如果 .c 文件里有中文，而文件是**不带 BOM 的 UTF-8**，编译时会警告 `C4819`（VS2026 也会报，不只是 2010）。
解决：**文件 → 高级保存选项 → 编码选「UTF-8 带签名」**（或者改成"简体中文 GBK"）。存完警告就没了——这一点本机实测过。

### 0.6 顺便：机房那台 VS2010 怎么建

**文件 → 新建 → 项目 → Win32 控制台应用程序** → 名称/位置 → **下一步** → 勾 **空项目** → 完成；
然后右键 **源文件 → 添加 → 新建项 → C++ 文件(.cpp)**，名字写成 `main.c` → 写代码 → **Ctrl+F5**。
（VS2010 的界面里叫"Win32 控制台应用程序"，和 2026 的"空项目"是一回事。）

---

## 1. 三条路线

| 路线 | 效果 | 需要什么 |
|---|---|---|
| **A. VSIX 插件（已装好）** | 2026 编辑器里直接波浪线 + 悬停提示，VS 菜单里一键开关 | 已安装，见下面"按项目开关" |
| **B. 编译时检查** | 每次编译，问题出现在错误列表，双击跳到行 | 在 `.vcxproj` 里 import 一行 |
| **C. 命令行扫描** | 交作业前跑一下，终端列出所有问题 | Python 3 |

---

## 2. 【重点】按项目开关：默认全关，不碰你正常的工作

插件**默认对所有项目完全静默**——只有某个目录（或它的任意上级目录）里存在
**`vs2010check.on`** 这个空文件时，该目录下的 `.c/.h` 才会被检查。

### 怎么开 / 怎么关

**方法 A：在 VS 里点菜单**（推荐）

- **工具** 菜单 →「开启 / 关闭 VS2010 兼容性检查（当前解决方案）」
- 或者 **右键解决方案节点** → 同一项

菜单项带勾选状态，操作结果会显示在状态栏；开启/关闭后编辑器里的标记**立刻刷新**，不用重开文件。

**方法 B：用 .bat 对任意文件夹开关**

把 `开启2010检查.bat` 复制到**作业文件夹**里双击 → 出现 `vs2010check.on` → 红线 2 秒内自动出现。
不要了就双击 `关闭2010检查.bat`。也可以把文件夹**拖到** `.bat` 上，对指定目录开关。

**方法 C：VS 的"外部工具"菜单**（想在 VS2010 上也有个菜单项时用）

工具 → 外部工具(E)... → 添加：

| 栏位 | 填什么 |
|---|---|
| 标题 | `开关 VS2010 检查` |
| 命令 | `powershell.exe` |
| 参数 | `-NoProfile -ExecutionPolicy Bypass -File "D:\vs2010-kit\toggle-check.ps1" "$(SolutionDir)"` |
| 初始目录 | `$(SolutionDir)` |

勾上「使用输出窗口」。这个在 VS2010 上同样能配（那台机器装不了这个扩展，用这个就有菜单项了）。

### 两个容易踩的点（都修好了，但要知道）

1. **判断是"往上找父目录"的**：只要本目录**或任意上级目录**里有 `vs2010check.on` 就算开启。
   所以**关闭的时候会把整条上级链上的开关文件一起清掉**，否则上层那个会继续生效——
   表现就是"明明点了关闭，红线还在"。清理了哪些文件会写在状态栏和输出里。
2. **不管用哪种方式开关，标记都会在 2 秒内自动刷新**（插件在轮询开关文件），
   不用重开 .c 文件。菜单那条路是事件驱动、立刻刷新。

### 波浪线两种颜色

- **红色** = VS2010 直接编不过（`error Cxxxx` 或链接失败）
- **绿色** = 能编过，但结果不对 / 在新版上编不过，注意一下就行

### 想临时对全部文件生效（调试用）

启动 VS 前设置环境变量 `VS2010GUARD_FORCE=1`。

---

## 3. 实测对照表（用真实 VS2010 RTM 的 cl.exe 一条条编译验证过）

### 3.1 硬错误 —— 2010 上真的编不过

| 写法 | VS2010 真实报错 | 改成 |
|---|---|---|
| 变量声明写在语句**之后** | `error C2143` + `error C2065` | 声明全挪到函数/块的开头 |
| `for (int i = 0; ...)` | `error C2143`（连报 4 个）+ C2065 | `int i;` 提到外面，写 `for (i = 0; ...)` |
| `#include <stdbool.h>` | `fatal error C1083` | 用 `int`，或 Windows 的 `BOOL/TRUE/FALSE` |
| `#include <inttypes.h>` | `fatal error C1083` | 打印 64 位整数用 `%I64d` |
| `int a[n];`（变长数组） | `error C2057` / `C2466` / `C2133` | `#define` 常量，或 `malloc` |
| `{ .x = 1 }` / `{ [2] = 7 }` | `error C2059` | 逐个字段赋值 |
| `(struct P){1,2}` 复合字面量 | `error C2059` | 先定义变量再赋值 |
| `inline` | `error C2054` / `C2085` / `C2143` | 写 `__inline` |
| `restrict` | `error C2146` / `C2061` / `C2059` | 写 `__restrict` |
| `_Bool` | `error C2065` | 用 `int` |
| `snprintf` `round` `isnan` `fmax` 等 C99 函数 | 编译只有 `warning C4013`，**链接时报 `LNK2019`** | `_snprintf` / 自己写 `(int)(x+0.5)` / `_isnan` |

### 3.2 能编过，但结果不对 / 要留神

| 写法 | 实测结果 | 说明 |
|---|---|---|
| `printf("%zu", ...)` | **编过，运行时原样打印出 `zu`** | 用 `%Iu`（`%zd/%hhd/%jd/%td` 同理要换） |
| `printf("%lld", ...)` | **编过，而且打印正确** | 不用改（我一开始以为要换 `%I64d`，实测是错的） |
| `#include <stdint.h>` | **实测存在**（VS2010 RTM 自带的 `VC\include\stdint.h`） | 不用改 |
| `strdup` | 能用，只有 `warning C4996` | 想消警告用 `_strdup` |
| 中文注释但文件是 **UTF-8 无 BOM** | `warning C4819`：该文件包含不能在当前代码页(936)中表示的字符 | 另存为「UTF-8 带签名」或 GBK |

### 3.3 反过来：2010 能过、2026 反而不过

| 写法 | VS2010 | VS2026 |
|---|---|---|
| `gets()` | 只有 `C4996` 警告，能用 | VS2015 起已从 CRT 移除 |
| `void main()` | 实测 0 警告，放过 | 警告/报错 |
| `scanf` `strcpy` `sprintf` | 只是 C4996 警告 | 开了 SDL 会当错误 |

考试按 2010 的标准写没问题，但你自己在 2026 里调试时要知道它们为什么会红。

### 3.4 不用管的（实测 2010 完全正常）

`//` 单行注释 · `long long` · `int a[10] = {1,2,3};` · 函数里嵌套块开头的声明 · `%I64d` + `__int64`

---

## 4. 路线 B：编译时检查（不想用插件就这个）

在 `.vcxproj` 最后一个 `</Project>` 之前加一行：

```xml
<Import Project="D:\vs2010-kit\msbuild\Vs2010Guard.targets" />
```

默认以**警告**形式进错误列表，不影响构建。想让它挡住构建：`/p:Vs2010CheckAsError=true`；临时关掉：`/p:Vs2010CheckEnabled=false`。

## 5. 路线 C：命令行扫描

```powershell
python D:\vs2010-kit\vs2010-c-scan.py 你的文件.c
python D:\vs2010-kit\vs2010-c-scan.py -r D:\作业目录
python D:\vs2010-kit\vs2010-c-scan.py --msbuild-warn a.c
```

样例：`samples/demo.c`（一堆典型问题，应报 11 错 + 2 疑）、`samples/demo2.c`（只该报指定初始化器，用来验证不误报）。

---

## 6. 插件的打包、安装、调试

```powershell
# 打包（用的就是你机器上 VS2026 自带的 MSBuild 18）
& "D:\Application\VS\Production\MSBuild\Current\Bin\amd64\MSBuild.exe" ^
  "D:\vs2010-kit\vsix\Vs2010Guard.sln" /restore /p:Configuration=Debug

# 安装（先关掉 VS）
& "D:\Application\VS\Production\Common7\IDE\VSIXInstaller.exe" /quiet ^
  "D:\vs2010-kit\vsix\Vs2010Guard\bin\Debug\Vs2010Guard.vsix"

# 卸载
& "D:\Application\VS\Production\Common7\IDE\VSIXInstaller.exe" /quiet ^
  /uninstall:Vs2010Guard.6e1d2b3a-4c5f-4a7b-9e10-9a8b7c6d5e4f
```

想调试就打开 `vsix\Vs2010Guard.sln` 按 **F5**（实验实例，不动你正在用的 VS）。
改了规则以后想确认插件还能编译（不需要开 VS）：`dotnet build vsix\_compilecheck\Verify.csproj`。

### 分享给别人：一键安装包

`一键安装包\`（也打成了 `Vs2010Guard-一键安装.zip`，约 22 KB，直接发这个）：

| 文件 | 作用 |
|---|---|
| `安装.bat` | 双击就装：用 vswhere 找到 VS2022/2026，再调 VSIXInstaller |
| `Vs2010Guard.vsix` | 插件本体 |
| `开启2010检查.bat` / `关闭2010检查.bat` | 复制到作业文件夹双击，对本目录开关 |
| `使用说明.txt` | 给拿到包的人看的说明（GBK 编码，记事本直接看） |

### 打包时踩过的坑（记下来免得再踩）

| 现象 | 原因 | 解药 |
|---|---|---|
| `VSSDK1311`：manifest 缺 ProductArchitecture | 它是 `<InstallationTarget>` 的**子元素**，不是属性 | `<ProductArchitecture>amd64</ProductArchitecture>` |
| MEF 组件完全不被加载（编辑器里毫无反应，也没有报错） | VSIX 清单里写了 `dllPath=` | 改成 `Path=` |
| VS 的错误聚合器请求 `IErrorTag`，不是 `ErrorTag` | `CreateTagger<T>` 里判断错类型就永远返回 null | 两个都接受 |
| **包装了但完全不加载，没有任何报错** | pkgdef 里生成的是 `"Assembly"=...`，VS 在扩展目录找不到程序集 | csproj 里加 `<UseCodebase>true</UseCodebase>`，让它生成 `"CodeBase"="$PackageFolder$\...dll"` |
| `error CS0012: MenuCommandService 在未引用的程序集中` | 需要 `System.Design` | 加 `<Reference Include="System.Design" />` |
| **菜单项怎么都不出现**（命令在命令表里、包也加载了，就是没有那一项） | 分组挂在了老的 `IDG_VS_MM_TOOLSADDINS`（"工具菜单里的 add-ins 分组"）上，**VS2026 不再渲染这个分组** | 把分组的父级改成 **"工具"菜单本身**：`<Parent guid="guidSHLMainMenu" id="IDM_VS_MENU_TOOLS" />`（VS2026 官方 VSIX 模板就是这么写的，模板文件在 `...\Microsoft.Vsix.TemplatesPackage\ItemTemplates\...\CSharpCustomCommandItemTemplate\VSPackage.vsct`） |
| VSIXInstaller 静默失败 / 返回 2004 | 有残留的 `MSBuild.exe` 进程，VSIXInstaller 会拒绝改动扩展 | 先 `Stop-Process MSBuild` 再装；卡住时看 `%TEMP%\dd_VSIXInstaller_*.log` |

## 7. 排查用的诊断日志

启动 VS 前设置环境变量 `VS2010GUARD_LOG` 指向一个 .log 文件，插件会往里面写：
创建了 tagger、扫了哪个文件、扫出多少处、或者为什么跳过。平时不设置就完全静默。

## 8. 已验证 / 未验证

- ✅ 规则表：**每一条都用真实 VS2010 的 cl.exe 编译验证过**（第 3 节的报错号都是实测输出）
- ✅ 插件：在 VS2026 里实测——开了开关 `发现 13 处问题`，关掉开关打印`已跳过`、编辑器里没有任何标记
- ✅ 规则引擎：插件（C#）与命令行（Python）对同一样例输出逐条一致
- ✅ 菜单命令：实测日志 `Vs2010GuardPackage 已加载`、`菜单命令已注册`
- ✅ 开关逻辑：单元测试通过（无标记=False → Toggle 建标记=True → 子目录文件也生效 → 再 Toggle=False）
- ✅ **菜单项端到端实测**（2026-10-14）：`工具` 菜单里确实出现「开启/关闭 VS2010 兼容性检查」；
  用 DTE 模拟点击执行后，插件日志打出 `菜单切换：… -> 开启`，目标目录里真的生成了 `vs2010check.on`
- ⚠️ 没做的：红线渲染的截图确认（日志已证明 ErrorTag 产出，渲染是 VS 自己的事）

### 排查这些问题的可靠手段（留个备忘）

- **别靠截图看菜单**：用 DTE 的 CommandBars 直接读菜单结构，比 UI Automation 稳得多：
  ```powershell
  $dte = [Runtime.InteropServices.Marshal]::GetActiveObject('VisualStudio.DTE.18.0')
  foreach ($b in $dte.CommandBars) { if ($b.Name -eq 'MenuBar') {
    foreach ($c in $b.Controls) { if ($c.Caption -like '*工具*') {
      foreach ($s in $c.CommandBar.Controls) { $s.Caption } } } } }
  ```
  还能直接 `$control.Execute()` 模拟点击。
- **判断 VSCT 有没有被加载**：`$dte.Commands` 里按你的 cmdset GUID 过滤，能查到就说明 ctmenu 解析成功了
  （注意：命令的 `Name` 字段是空的属于正常，不能据此判断）。

## 9. 两个方向的对比测试

### 9.1 插件 vs 真 VS2010

拿 `samples\diff\` 里的 22 个 C 文件做差分测试：每个文件先用**真 VC10 的 cl.exe 编译**，记下每一条 error/warning 的行号，再用**插件的规则引擎**跑一遍，逐行对账。

```
22 个文件（其中 3 个 2010 提前 fatal error 中断）
==> 命中 15，级别高于 2010 4，级别低于 2010 0，漏报 1，误报 0
另有 5 条"能编过但结果不对 / 反向兼容"的提示（2010 确实编得过）
```

怎么读这个结果：

- **误报 0**：插件报的每一条，2010 上都真的有对应问题。
- **漏报 1**（`t12_inline.c`）：2010 在 L1 报 `inline` 错之后，L2 又连带报了个 `C4013`；插件只报了根因 L1。属于编译器的级联报错，不是漏检。
- **级别高于 2010 4 条**（`snprintf` `round` `isnan` `_Static_assert`）：编译期 2010 只给 `warning C4013`，但**链接期必定失败**，实测输出：
  ```
  warning C4013: "snprintf"未定义；假设外部返回 int
  error LNK2019: 无法解析的外部符号 _snprintf，该符号在函数 _main 中被引用
  ```
  所以插件标红是对的，提示文字里也写明了"编译只给 C4013 警告，链接报 LNK2019"。
- **3 个"中断"文件**：`<stdbool.h>` 这类 fatal error 会让 2010 直接停止编译，后面的构造它根本没机会报——插件照报不误，这是优点不是误报。

### 9.2 命令行版 vs 插件版：结果必须完全一致

两个实现（`vs2010-c-scan.py` 和插件里的 `Vs2010Rules.cs`）用的是**同一套算法**：
先 `Blank()` 把注释和字符串涂成空格（偏移量不变），再 `StripDirectives()` 清掉预处理行，
然后**按语句**（而不是按行）扫描——跟踪括号深度和"这个大括号是初始化器还是语句块"，
所以 `int main(void){ int a=1; printf(...); int b=2; }` 这种一行写多条也能查出来。

一条命令验证两边一致：

```powershell
python D:\vs2010-kit\check-parity.py
```

它会编译 `vsix\_rulecheck`（里面就是插件用的那份 `Vs2010Rules.cs`），
把两个实现都跑一遍 `samples\diff`，按「文件 + 行号 + 级别 + 类型」逐条比对。
当前结果：

```
命令行版 35 条 / 插件版 35 条
==> 两个实现完全一致（文件 / 行号 / 级别 / 类型 全对齐）
```

退出码 0 = 一致，1 = 有差异。**改规则以后请跑一下这个**。

### 9.3 差分测试怎么复现

```powershell
# 1) 用真 VC10 编译语料，看 2010 到底报什么
cmd /c D:\vs2010-kit\samples\diff\_run_cl.bat
# 2) 验证"链接期失败"的说法（snprintf / isnan / _Static_assert）
cmd /c D:\vs2010-kit\samples\diff\_run_link.bat
# 3) 用插件同一套规则跑一遍
& D:\vs2010-kit\vsix\_rulecheck\bin\Debug\net472\RuleCheck.exe ^
  (Get-ChildItem D:\vs2010-kit\samples\diff\*.c | % FullName)
```

## 10. 改规则要改两个地方（然后必须跑一致性检查）

`vs2010-c-scan.py`（命令行）和 `vsix/Vs2010Guard/Vs2010Rules.cs`（插件）是同一套规则、同一套算法，改完跑：

```powershell
python D:\vs2010-kit\check-parity.py          # 两边结果必须完全一致
dotnet build D:\vs2010-kit\vsix\_compilecheck\Verify.csproj   # 插件还能编译
```
