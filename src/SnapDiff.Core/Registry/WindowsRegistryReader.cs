using System.Security;
using Microsoft.Win32;

namespace SnapDiff.Core;

/// <summary>
/// 真实的注册表读取器（只在 Windows 上可用）。
/// 全程只读：只用 OpenSubKey 读，绝不写入、不删除。
/// 在 64 位系统上读取 64 位视图——32 位安装程序写到 WOW6432Node 的内容同样能被看到。
/// </summary>
public sealed class WindowsRegistryReader : IRegistryReader, IDisposable
{
    // 缓存已经打开的根键（HKCU / HKLM…），整个快照过程只打开一次
    private readonly Dictionary<string, RegistryKey> _baseKeys = new(StringComparer.OrdinalIgnoreCase);

    private static readonly RegistryView View =
        Environment.Is64BitOperatingSystem ? RegistryView.Registry64 : RegistryView.Default;

    /// <inheritdoc />
    public RegistryKeyData? ReadKey(string rootName, string subKeyPath)
    {
        try
        {
            var baseKey = GetBaseKey(rootName);
            if (baseKey is null) return null;

            bool opened = subKeyPath.Length > 0;
            var key = opened ? baseKey.OpenSubKey(subKeyPath) : baseKey;
            if (key is null) return null;

            try
            {
                var subKeys = key.GetSubKeyNames();
                var values = new List<RegistryValueData>();
                foreach (var name in key.GetValueNames())
                {
                    try
                    {
                        var kind = key.GetValueKind(name);
                        // DoNotExpandEnvironmentNames：REG_EXPAND_SZ 里的 %xxx% 保持原样，方便逐字对比
                        var raw = key.GetValue(name, null, RegistryValueOptions.DoNotExpandEnvironmentNames);
                        values.Add(new RegistryValueData(name, kind, raw));
                    }
                    catch (Exception ex) when (ex is UnauthorizedAccessException or SecurityException or IOException)
                    {
                        // 单个值读不了就跳过这个值，不影响整个键
                    }
                }
                return new RegistryKeyData(subKeys, values);
            }
            finally
            {
                if (opened) key.Dispose(); // 自己打开的键自己关；根键留给 Dispose() 统一关闭
            }
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or SecurityException or IOException or ArgumentException)
        {
            // 键不存在或没有读取权限：返回 null，由调用方计入“跳过”
            return null;
        }
    }

    private RegistryKey? GetBaseKey(string rootName)
    {
        if (_baseKeys.TryGetValue(rootName, out var cached)) return cached;
        if (!TryMapHive(rootName, out var hive)) return null;
        var opened = RegistryKey.OpenBaseKey(hive, View);
        _baseKeys[rootName] = opened;
        return opened;
    }

    private static bool TryMapHive(string rootName, out RegistryHive hive)
    {
        switch (rootName.Trim().ToUpperInvariant())
        {
            case "HKEY_CURRENT_USER":
            case "HKCU":
                hive = RegistryHive.CurrentUser;
                return true;
            case "HKEY_LOCAL_MACHINE":
            case "HKLM":
                hive = RegistryHive.LocalMachine;
                return true;
            case "HKEY_CLASSES_ROOT":
            case "HKCR":
                hive = RegistryHive.ClassesRoot;
                return true;
            case "HKEY_USERS":
                hive = RegistryHive.Users;
                return true;
            case "HKEY_CURRENT_CONFIG":
                hive = RegistryHive.CurrentConfig;
                return true;
            default:
                hive = default;
                return false;
        }
    }

    /// <summary>关闭所有打开的根键。</summary>
    public void Dispose()
    {
        foreach (var key in _baseKeys.Values) key.Dispose();
        _baseKeys.Clear();
    }
}
