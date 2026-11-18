namespace SnapDiff.App;

/// <summary>
/// 多语言字典：界面上的每一个字都从这里取（Loc.T("键")），禁止在 XAML 或代码里硬编码文案。
/// 目前支持 zh-CN / en-US 两套完整文案；运行时可切换，选择保存在设置文件里（见 AppSettings）。
/// </summary>
public static class Loc
{
    public const string ZhCn = "zh-CN";
    public const string EnUs = "en-US";

    private static readonly Dictionary<string, Dictionary<string, string>> Catalog = new()
    {
        [ZhCn] = new Dictionary<string, string>
        {
            ["App.Title"] = "快照侦探 SnapDiff — 查出软件到底改了系统的什么",
            ["App.Name"] = "快照侦探 SnapDiff",
            ["Nav.Step1"] = "拍快照 A（选范围）",
            ["Nav.Step2"] = "安装 / 运行目标软件",
            ["Nav.Step3"] = "拍快照 B",
            ["Nav.Step4"] = "查看对比结果",
            ["Sidebar.Readonly"] = "只读模式 · 不上传任何数据",
            ["Menu.LangZh"] = "简体中文",
            ["Menu.LangEn"] = "English",
            ["Menu.About"] = "关于快照侦探…",
            ["About.Title"] = "关于快照侦探",
            ["About.Text"] = "快照侦探 SnapDiff v0.1\n\n安装软件前后各拍一张只读“系统快照”，对比出软件新增、修改、删除了哪些文件和注册表项。\n\n100% 只读：不写注册表、不改你的文件、不联网。\n\nMIT 开源许可 · SnapDiff Contributors",
            ["Group.Registry"] = "注册表范围",
            ["Group.Folders"] = "文件夹范围",
            ["Group.Advanced"] = "高级选项",
            ["Check.Hkcu"] = "HKEY_CURRENT_USER（当前用户）",
            ["Check.Hklm"] = "HKEY_LOCAL_MACHINE（整台电脑）",
            ["Check.Hash"] = "计算文件 SHA256（更精确但更慢；仅对小于 8 MB 的文件计算）",
            ["Label.AdminHint"] = "提示：未以管理员身份运行，HKLM 中少数受保护的键会被自动跳过（计入“已跳过”统计）。",
            ["Label.Exclusions"] = "注册表排除清单（每行一个路径前缀，大小写不敏感；命中的整个子树不拍）：",
            ["Label.ExclusionHint"] = "注：开机自启动键（…\\CurrentVersion\\Run 等）不在默认排除清单里——它们往往是软件留下的最重要线索。",
            ["Label.Scope"] = "本次快照范围（与快照 A 完全相同）：",
            ["Label.Search"] = "搜索：",
            ["Btn.ResetExclusions"] = "恢复默认排除清单",
            ["Btn.AddFolder"] = "添加文件夹…",
            ["Btn.RemoveFolder"] = "移除选中项",
            ["Btn.ResetFolders"] = "恢复默认文件夹",
            ["Btn.StartA"] = "开始拍摄快照 A",
            ["Btn.StartB"] = "开始拍摄快照 B",
            ["Btn.Cancel"] = "取消",
            ["Btn.GoStep3"] = "我已完成，继续拍快照 B →",
            ["Btn.BackStep1"] = "← 重新拍快照 A",
            ["Btn.ExportMd"] = "导出 Markdown 报告…",
            ["Btn.ExportCsv"] = "导出 CSV 表格…",
            ["Btn.Restart"] = "重新开始",
            ["Btn.Clear"] = "清空",
            ["Progress.Format"] = "阶段：{0}    已处理 {1:N0} 项    已跳过 {2:N0}    {3}",
            ["Progress.DiffFormat"] = "阶段：{0}    已处理 {1:N0} 条",
            ["Progress.StartRegistry"] = "开始扫描注册表…",
            ["Progress.StartFiles"] = "开始扫描文件…",
            ["Progress.Saving"] = "正在写快照文件…",
            ["Progress.Done"] = "快照完成。",
            ["Phase.Registry"] = "扫描注册表",
            ["Phase.Files"] = "扫描文件",
            ["Phase.Save"] = "保存快照",
            ["Phase.Diff"] = "对比两张快照",
            ["Phase.Done"] = "完成",
            ["Phase.Prepare"] = "准备中",
            ["Msg.NeedScope"] = "请至少勾选一个注册表根键，或至少添加一个文件夹。",
            ["Msg.Cancelled"] = "已取消本次扫描，快照未保存。",
            ["Msg.SnapshotError"] = "拍摄快照时出错：{0}\n\n本次快照文件已删除。",
            ["Msg.DiffError"] = "对比时出错：{0}",
            ["Msg.Exported"] = "已导出：{0}",
            ["Msg.ExportFailed"] = "导出失败：{0}",
            ["Msg.Unhandled"] = "发生未处理的错误，程序可以继续使用。",
            ["Msg.SavedTo"] = "快照 A 已保存到：",
            ["Dialog.SaveA"] = "保存快照 A（安装软件【前】）",
            ["Dialog.SaveB"] = "保存快照 B（安装软件【后】）",
            ["Dialog.SnapFilter"] = "快照侦探快照文件 (*.snapdiff)|*.snapdiff|所有文件 (*.*)|*.*",
            ["Dialog.AllFiles"] = "所有文件 (*.*)|*.*",
            ["Dialog.NameA"] = "快照A_{0}",
            ["Dialog.NameB"] = "快照B_{0}",
            ["Dialog.NameReport"] = "SnapDiff对比报告_{0}",
            ["Dialog.ExportMd"] = "导出 Markdown 报告",
            ["Dialog.ExportCsv"] = "导出 CSV 表格",
            ["Dialog.ChooseFolder"] = "选择要监视的文件夹",
            ["Step2.Header"] = "现在去安装 / 运行你要检查的软件",
            ["Step2.Body"] = "把要检查的软件安装好（绿色软件就运行一次），等它把该写的文件和注册表都写完，然后回到本窗口拍第二张快照。",
            ["Step3.Header"] = "拍快照 B（范围必须与快照 A 完全相同，这样对比才有意义）",
            ["Step4.Header"] = "对比结果",
            ["Summary.Detail"] = "注册表 {0} 项 · 文件 {1} 项",
            ["Tree.Added"] = "新增",
            ["Tree.Removed"] = "删除",
            ["Tree.Modified"] = "修改",
            ["Tree.Size"] = "大小",
            ["Tree.NewKey"] = "新建键",
            ["Tree.RemovedKey"] = "删除的键",
            ["Tree.DefaultValue"] = "(默认值)",
            ["Tree.RootDir"] = "(根目录)",
            ["Tree.HiveRoot"] = "(根键下)",
            ["Tree.GroupNode"] = "{0}  ({1:N0} 项)",
            ["Tree.Truncated"] = "… 本组共 {0:N0} 项，只显示前 {1:N0} 项（可用搜索缩小范围）",
            ["Tree.NoMatch"] = "(没有匹配搜索条件的结果)",
            ["Tree.NoChanges"] = "两张快照之间没有发现任何变化",
            ["Tree.KindNode"] = "{0}（注册表 {1:N0} 项，文件 {2:N0} 项）",
            ["Tree.RegistryNode"] = "注册表  ({0:N0} 项)",
            ["Tree.FilesNode"] = "文件  ({0:N0} 项)",
            ["Scope.None"] = "（未选）",
            ["Scope.RegistryPrefix"] = "注册表：",
            ["Scope.Exclusions"] = "；排除 {0} 条前缀",
            ["Scope.FoldersPrefix"] = "；文件夹：",
            ["Scope.HashOn"] = "；SHA256：开（小于 8 MB）",
            ["Scope.HashOff"] = "；SHA256：关",
        },

        [EnUs] = new Dictionary<string, string>
        {
            ["App.Title"] = "SnapDiff — Find out exactly what a program changed on your PC",
            ["App.Name"] = "SnapDiff",
            ["Nav.Step1"] = "Take snapshot A (pick scope)",
            ["Nav.Step2"] = "Install / run the program",
            ["Nav.Step3"] = "Take snapshot B",
            ["Nav.Step4"] = "Review the diff",
            ["Sidebar.Readonly"] = "Read-only · no data uploaded",
            ["Menu.LangZh"] = "简体中文",
            ["Menu.LangEn"] = "English",
            ["Menu.About"] = "About SnapDiff…",
            ["About.Title"] = "About SnapDiff",
            ["About.Text"] = "SnapDiff v0.1\n\nTake one read-only snapshot before you install something and another one after — SnapDiff shows exactly which files and registry entries the program added, changed, or removed.\n\n100% read-only: never writes the registry, never touches your files, fully offline.\n\nMIT License · SnapDiff Contributors",
            ["Group.Registry"] = "Registry scope",
            ["Group.Folders"] = "Folder scope",
            ["Group.Advanced"] = "Advanced",
            ["Check.Hkcu"] = "HKEY_CURRENT_USER (current user)",
            ["Check.Hklm"] = "HKEY_LOCAL_MACHINE (whole machine)",
            ["Check.Hash"] = "Compute file SHA256 (more accurate but slower; files under 8 MB only)",
            ["Label.AdminHint"] = "Note: not running as administrator — a few protected HKLM keys will be skipped (counted as skipped).",
            ["Label.Exclusions"] = "Registry exclusion list (one path prefix per line, case-insensitive; matching subtrees are skipped):",
            ["Label.ExclusionHint"] = "Note: autostart keys (…\\CurrentVersion\\Run etc.) are NOT excluded by default — they are often the most important traces.",
            ["Label.Scope"] = "Snapshot scope (identical to snapshot A):",
            ["Label.Search"] = "Search:",
            ["Btn.ResetExclusions"] = "Restore defaults",
            ["Btn.AddFolder"] = "Add folder…",
            ["Btn.RemoveFolder"] = "Remove selected",
            ["Btn.ResetFolders"] = "Restore defaults",
            ["Btn.StartA"] = "Take snapshot A",
            ["Btn.StartB"] = "Take snapshot B",
            ["Btn.Cancel"] = "Cancel",
            ["Btn.GoStep3"] = "Done — take snapshot B →",
            ["Btn.BackStep1"] = "← Retake snapshot A",
            ["Btn.ExportMd"] = "Export Markdown report…",
            ["Btn.ExportCsv"] = "Export CSV…",
            ["Btn.Restart"] = "Start over",
            ["Btn.Clear"] = "Clear",
            ["Progress.Format"] = "Phase: {0}    Processed {1:N0}    Skipped {2:N0}    {3}",
            ["Progress.DiffFormat"] = "Phase: {0}    Processed {1:N0} entries",
            ["Progress.StartRegistry"] = "Scanning registry…",
            ["Progress.StartFiles"] = "Scanning files…",
            ["Progress.Saving"] = "Writing snapshot file…",
            ["Progress.Done"] = "Snapshot finished.",
            ["Phase.Registry"] = "scanning registry",
            ["Phase.Files"] = "scanning files",
            ["Phase.Save"] = "saving snapshot",
            ["Phase.Diff"] = "diffing snapshots",
            ["Phase.Done"] = "done",
            ["Phase.Prepare"] = "preparing",
            ["Msg.NeedScope"] = "Select at least one registry root, or add at least one folder.",
            ["Msg.Cancelled"] = "Scan cancelled; no snapshot was saved.",
            ["Msg.SnapshotError"] = "Error while taking snapshot: {0}\n\nThe partial snapshot file was deleted.",
            ["Msg.DiffError"] = "Diff failed: {0}",
            ["Msg.Exported"] = "Exported: {0}",
            ["Msg.ExportFailed"] = "Export failed: {0}",
            ["Msg.Unhandled"] = "An unexpected error occurred. The app can continue.",
            ["Msg.SavedTo"] = "Snapshot A saved to:",
            ["Dialog.SaveA"] = "Save snapshot A (BEFORE installing)",
            ["Dialog.SaveB"] = "Save snapshot B (AFTER installing)",
            ["Dialog.SnapFilter"] = "SnapDiff snapshot (*.snapdiff)|*.snapdiff|All files (*.*)|*.*",
            ["Dialog.AllFiles"] = "All files (*.*)|*.*",
            ["Dialog.NameA"] = "SnapshotA_{0}",
            ["Dialog.NameB"] = "SnapshotB_{0}",
            ["Dialog.NameReport"] = "SnapDiffReport_{0}",
            ["Dialog.ExportMd"] = "Export Markdown report",
            ["Dialog.ExportCsv"] = "Export CSV",
            ["Dialog.ChooseFolder"] = "Choose a folder to watch",
            ["Step2.Header"] = "Now install / run the program you want to inspect",
            ["Step2.Body"] = "Install it (or run it once if it is a portable app), let it finish writing its files and registry entries, then come back here for the second snapshot.",
            ["Step3.Header"] = "Take snapshot B (the scope must be identical to snapshot A, or the diff would be meaningless)",
            ["Step4.Header"] = "Diff results",
            ["Summary.Detail"] = "registry {0} · files {1}",
            ["Tree.Added"] = "Added",
            ["Tree.Removed"] = "Removed",
            ["Tree.Modified"] = "Modified",
            ["Tree.Size"] = "size",
            ["Tree.NewKey"] = "new key",
            ["Tree.RemovedKey"] = "removed key",
            ["Tree.DefaultValue"] = "(default value)",
            ["Tree.RootDir"] = "(root folder)",
            ["Tree.HiveRoot"] = "(hive root)",
            ["Tree.GroupNode"] = "{0}  ({1:N0})",
            ["Tree.Truncated"] = "… {0:N0} items in this group, showing first {1:N0} (use search to narrow down)",
            ["Tree.NoMatch"] = "(no results match the search)",
            ["Tree.NoChanges"] = "No changes found between the two snapshots",
            ["Tree.KindNode"] = "{0} (registry {1:N0}, files {2:N0})",
            ["Tree.RegistryNode"] = "Registry  ({0:N0})",
            ["Tree.FilesNode"] = "Files  ({0:N0})",
            ["Scope.None"] = "(none)",
            ["Scope.RegistryPrefix"] = "Registry: ",
            ["Scope.Exclusions"] = "; {0} exclusion prefixes",
            ["Scope.FoldersPrefix"] = "; Folders: ",
            ["Scope.HashOn"] = "; SHA256: on (files < 8 MB)",
            ["Scope.HashOff"] = "; SHA256: off",
        },
    };

    /// <summary>当前语言。</summary>
    public static string CurrentLanguage { get; private set; } = ZhCn;

    /// <summary>语言切换后触发；界面订阅它来刷新所有文字。</summary>
    public static event Action? LanguageChanged;

    /// <summary>程序启动时调用：读取设置里的语言并生效。</summary>
    public static void Initialize()
    {
        AppSettings.Load();
        Apply(AppSettings.Language);
    }

    /// <summary>切换语言（并持久化到设置文件）。</summary>
    public static void SetLanguage(string language)
    {
        if (language != CurrentLanguage)
        {
            Apply(language);
            AppSettings.Language = language;
            AppSettings.Save();
        }
    }

    private static void Apply(string language)
    {
        if (language != ZhCn && language != EnUs) language = ZhCn;
        CurrentLanguage = language;
        LanguageChanged?.Invoke();
    }

    /// <summary>取文案：当前语言缺键时退回中文，再缺就原样返回键名（方便发现漏翻）。</summary>
    public static string T(string key)
    {
        if (Catalog[CurrentLanguage].TryGetValue(key, out var text)) return text;
        if (Catalog[ZhCn].TryGetValue(key, out var fallback)) return fallback;
        return key;
    }
}
