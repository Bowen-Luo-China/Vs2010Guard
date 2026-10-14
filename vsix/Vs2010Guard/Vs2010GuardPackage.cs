using System;
using System.ComponentModel.Design;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;

namespace Vs2010Guard
{
    /// <summary>
    /// VS 里的菜单入口：工具菜单 + 解决方案右键，一键开关“当前解决方案”的 VS2010 检查。
    /// </summary>
    [PackageRegistration(UseManagedResourcesOnly = true, AllowsBackgroundLoading = true)]
    [InstalledProductRegistration("Vs2010Guard", "在 VS 里标出 VS2010 (VC10 / C89) 编不过的 C 写法", "1.0")]
    [ProvideMenuResource("Menus.ctmenu", 1)]
    // 启动就加载，避免"菜单已显示但命令处理器还没注册"的竞态
    [ProvideAutoLoad(Microsoft.VisualStudio.Shell.Interop.UIContextGuids80.NoSolution, PackageAutoLoadFlags.BackgroundLoad)]
    [ProvideAutoLoad(Microsoft.VisualStudio.Shell.Interop.UIContextGuids80.SolutionExists, PackageAutoLoadFlags.BackgroundLoad)]
    [Guid(PackageGuidString)]
    public sealed class Vs2010GuardPackage : AsyncPackage
    {
        public const string PackageGuidString = "9f2c6a41-7b3d-4e58-9c1a-6d5e8f3b2a70";

        private static readonly Guid CmdSetGuid = new Guid("3a7d1e94-5c2b-4f86-b1d3-8e6a09c47f52");
        private const int ToggleCommandId = 0x0100;

        protected override async Task InitializeAsync(CancellationToken cancellationToken,
                                                     IProgress<ServiceProgressData> progress)
        {
            await base.InitializeAsync(cancellationToken, progress);
            await JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);

            Vs2010GuardDiagnostics.Log("Vs2010GuardPackage 已加载（菜单命令注册中）");

            OleMenuCommandService commandService =
                await GetServiceAsync(typeof(IMenuCommandService)) as OleMenuCommandService;
            if (commandService == null)
            {
                Vs2010GuardDiagnostics.Log("拿不到 IMenuCommandService，菜单命令没注册上");
                return;
            }

            CommandID commandId = new CommandID(CmdSetGuid, ToggleCommandId);
            OleMenuCommand command = new OleMenuCommand(OnToggle, commandId);
            command.BeforeQueryStatus += OnBeforeQueryStatus;
            commandService.AddCommand(command);

            Vs2010GuardDiagnostics.Log("菜单命令已注册");
        }

        private void OnBeforeQueryStatus(object sender, EventArgs e)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            OleMenuCommand command = sender as OleMenuCommand;
            if (command == null) return;

            string folder = GetSolutionFolder();
            bool enabled = Vs2010GuardSwitch.IsEnabledForFolder(folder);

            command.Visible = true;
            command.Enabled = !string.IsNullOrEmpty(folder);
            command.Checked = enabled;
            command.Text = enabled
                ? "关闭 VS2010 兼容性检查（当前解决方案）"
                : "开启 VS2010 兼容性检查（当前解决方案）";
        }

        private void OnToggle(object sender, EventArgs e)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            string folder = GetSolutionFolder();
            if (string.IsNullOrEmpty(folder))
            {
                ShowMessage("现在没有打开解决方案。\n\n可以改用 vs2010-kit 里的「开启2010检查.bat」对任意文件夹开关。");
                return;
            }

            System.Collections.Generic.List<string> before = Vs2010GuardSwitch.FindMarkersInChain(folder);
            bool enabled = Vs2010GuardSwitch.Toggle(folder);
            Vs2010GuardDiagnostics.Log("菜单切换：" + folder + " -> " + (enabled ? "开启" : "关闭") +
                "（切换前这条目录链上有 " + before.Count + " 个开关文件）");

            if (enabled)
            {
                SetStatus("VS2010 兼容性检查：已开启 —— " + folder +
                          "   （红线 = 2010 编不过；绿线 = 能编过但结果不对）");
            }
            else
            {
                string extra = before.Count > 1
                    ? "   顺带清掉了上级目录里的 " + (before.Count - 1) + " 个开关文件（不然它们还会把检查打开）"
                    : "";
                SetStatus("VS2010 兼容性检查：已关闭 —— " + folder + extra);
            }
        }

        private string GetSolutionFolder()
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            IVsSolution solution = GetService(typeof(SVsSolution)) as IVsSolution;
            if (solution == null) return null;

            string directory, solutionFile, options;
            if (solution.GetSolutionInfo(out directory, out solutionFile, out options) != 0) return null;
            if (string.IsNullOrEmpty(solutionFile)) return null;   // 没有打开解决方案

            if (!string.IsNullOrEmpty(directory)) return directory;
            try
            {
                return Path.GetDirectoryName(solutionFile);
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>把结果写到状态栏，而不是弹个框打断人。</summary>
        private void SetStatus(string text)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            try
            {
                IVsStatusbar statusBar = GetService(typeof(SVsStatusbar)) as IVsStatusbar;
                if (statusBar != null) statusBar.SetText(text);
            }
            catch (Exception)
            {
                // 状态栏写不进去不影响功能
            }
            Vs2010GuardDiagnostics.Log("状态栏：" + text);
        }

        private void ShowMessage(string message)
        {
            VsShellUtilities.ShowMessageBox(
                this, message, "VS2010 兼容性检查",
                OLEMSGICON.OLEMSGICON_INFO, OLEMSGBUTTON.OLEMSGBUTTON_OK,
                OLEMSGDEFBUTTON.OLEMSGDEFBUTTON_FIRST);
        }
    }
}
