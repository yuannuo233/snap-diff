using Microsoft.Win32;

namespace SnapDiff.Core;

/// <summary>
/// 注册表读取的抽象接口：把“怎么读注册表”（真实系统）和“怎么遍历、怎么记录”（快照器）分开。
/// 这样单元测试可以塞一个假实现来测试遍历逻辑，而不用碰真实注册表。
/// 接口里的所有操作都是只读的。
/// </summary>
public interface IRegistryReader
{
    /// <summary>
    /// 读取一个键：返回它的子键名列表和所有值。
    /// 键不存在或没有读取权限时返回 null（由调用方计入“跳过”）。
    /// </summary>
    /// <param name="rootName">根键名，如 HKEY_CURRENT_USER。</param>
    /// <param name="subKeyPath">相对根键的路径，空字符串表示根键本身。</param>
    RegistryKeyData? ReadKey(string rootName, string subKeyPath);
}

/// <summary>一个值的原始信息（还没有格式化）。</summary>
public sealed class RegistryValueData
{
    /// <summary>值名；空字符串表示默认值。</summary>
    public string Name { get; }

    /// <summary>注册表值类型。</summary>
    public RegistryValueKind Kind { get; }

    /// <summary>原始数据（string / int / long / byte[] / string[]，也可能是 null）。</summary>
    public object? RawValue { get; }

    public RegistryValueData(string name, RegistryValueKind kind, object? rawValue)
    {
        Name = name;
        Kind = kind;
        RawValue = rawValue;
    }
}

/// <summary>一个键的完整读取结果：子键名列表 + 全部值。</summary>
public sealed class RegistryKeyData
{
    public IReadOnlyList<string> SubKeyNames { get; }
    public IReadOnlyList<RegistryValueData> Values { get; }

    public RegistryKeyData(IReadOnlyList<string> subKeyNames, IReadOnlyList<RegistryValueData> values)
    {
        SubKeyNames = subKeyNames;
        Values = values;
    }
}
