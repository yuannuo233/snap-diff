namespace SnapDiff.Core;

/// <summary>
/// 报告（Markdown / CSV）里用的固定中文文案——报告是“文档”，目前只有中文版。
/// 界面上的文案不在这里，在 SnapDiff.App 的 Loc 字典里（支持中英双语）。
/// </summary>
public static class DisplayText
{
    public const string Registry = "注册表";
    public const string Files = "文件";
    public const string KeyMarker = "(键本身)";
    public const string DefaultValue = "(默认值)";

    /// <summary>变化种类的中文名。</summary>
    public static string Kind(ChangeKind kind) => kind switch
    {
        ChangeKind.Added => "新增",
        ChangeKind.Removed => "删除",
        _ => "修改",
    };

    /// <summary>类别的中文名。</summary>
    public static string Category(bool isRegistry) => isRegistry ? Registry : Files;

    /// <summary>值名的显示文本：null → 键本身；空 → 默认值；其他原样。</summary>
    public static string ValueName(RegistryEntry entry) =>
        entry.ValueName is null ? KeyMarker : (entry.ValueName.Length == 0 ? DefaultValue : entry.ValueName);
}
