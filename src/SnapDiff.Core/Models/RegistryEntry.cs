namespace SnapDiff.Core;

/// <summary>
/// 注册表快照里的一条记录：要么是一个“键本身”（ValueName == null，表示这个键存在），
/// 要么是键下的一个“值”（名字 + 类型 + 格式化后的数据）。
/// </summary>
public sealed class RegistryEntry
{
    /// <summary>完整键路径，形如 HKEY_CURRENT_USER\SOFTWARE\…。</summary>
    public string KeyPath { get; set; } = "";

    /// <summary>值名。null 表示“键本身”这条记录；空字符串表示默认值（俗称“(默认)”）。</summary>
    public string? ValueName { get; set; }

    /// <summary>值类型名，如 REG_SZ、REG_DWORD；键本身记录为空字符串。</summary>
    public string TypeName { get; set; } = "";

    /// <summary>格式化成可打印文本的数据（二进制会截断显示，见 <see cref="RegistryValueFormatter"/>）。</summary>
    public string Data { get; set; } = "";

    /// <summary>
    /// 对比用的“身份”字符串：键路径 + 值名。
    /// 用控制字符 \uFFFF 做分隔符（值名里不会出现）；
    /// “键本身”记录用 \0 前缀放进另一个“命名空间”，避免和值混淆。
    /// </summary>
    public string GetIdentity() => ValueName is null ? "\0" + KeyPath : KeyPath + "\uFFFF" + ValueName;
}
