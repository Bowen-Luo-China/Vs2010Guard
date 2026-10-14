using System;
using System.Collections.Generic;
using System.IO;

namespace Vs2010Guard
{
    /// <summary>
    /// 按项目开关的核心逻辑：目录（或任意上级目录）里有 vs2010check.on 就算开启。
    /// 命令行开关脚本、VS 菜单命令、编辑器里的波浪线都走这一份判断。
    /// </summary>
    internal static class Vs2010GuardSwitch
    {
        internal const string MarkerFileName = "vs2010check.on";

        /// <summary>开关状态变了（菜单命令切过）→ 让编辑器刷新标记。</summary>
        internal static event EventHandler Changed;

        /// <summary>这条目录链上（含自己，再到各级父目录）所有的开关文件，从最近的开始。</summary>
        internal static List<string> FindMarkersInChain(string folder)
        {
            List<string> found = new List<string>();
            if (string.IsNullOrEmpty(folder)) return found;

            DirectoryInfo dir;
            try
            {
                dir = new DirectoryInfo(folder);
            }
            catch (Exception)
            {
                return found;
            }

            while (dir != null)
            {
                try
                {
                    string marker = Path.Combine(dir.FullName, MarkerFileName);
                    if (File.Exists(marker)) found.Add(marker);
                }
                catch (Exception)
                {
                    // 权限之类的问题直接跳过
                }
                dir = dir.Parent;
            }
            return found;
        }

        internal static bool IsEnabledForFolder(string folder)
        {
            return FindMarkersInChain(folder).Count > 0;
        }

        internal static bool IsEnabledForFile(string filePath)
        {
            if (string.IsNullOrEmpty(filePath)) return false;
            string folder;
            try
            {
                folder = Path.GetDirectoryName(filePath);
            }
            catch (Exception)
            {
                return false;
            }
            return IsEnabledForFolder(folder);
        }

        /// <summary>
        /// 切换开关，返回切换后的状态（true = 已开启）。
        ///
        /// 关闭的时候会把**整条上级链**上的开关文件都删掉。因为判断是往上找父目录的，
        /// 只删当前目录那一个的话，上层目录里的开关会继续生效 —— 用户就会觉得
        /// “明明点了关闭，红线还在”。
        /// </summary>
        internal static bool Toggle(string folder)
        {
            if (string.IsNullOrEmpty(folder)) return false;

            try
            {
                List<string> existing = FindMarkersInChain(folder);
                if (existing.Count > 0)
                {
                    int removed = 0;
                    foreach (string marker in existing)
                    {
                        try
                        {
                            File.Delete(marker);
                            removed++;
                        }
                        catch (Exception ex)
                        {
                            Vs2010GuardDiagnostics.Log("删不掉 " + marker + "：" + ex.Message);
                        }
                    }

                    if (removed == 0) return true;   // 一个都没删掉（多半是权限），那还是开着
                    RaiseChanged();
                    return false;
                }

                string target = Path.Combine(folder, MarkerFileName);
                File.WriteAllText(target,
                    "这个文件存在 = 对本目录（含子目录）开启 VS2010 兼容性检查。" + Environment.NewLine +
                    "删掉它就关闭。" + Environment.NewLine);
                RaiseChanged();
                return true;
            }
            catch (Exception ex)
            {
                Vs2010GuardDiagnostics.Log("切换开关失败：" + ex.Message);
                return IsEnabledForFolder(folder);
            }
        }

        internal static void RaiseChanged()
        {
            EventHandler handler = Changed;
            if (handler != null) handler(null, EventArgs.Empty);
        }
    }
}
