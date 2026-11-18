using Microsoft.Win32;
using SnapDiff.Core;
using Xunit;

namespace SnapDiff.Tests;

/// <summary>
/// 注册表快照“遍历逻辑”测试：用假实现（FakeRegistryReader）代替真实注册表，
/// 只测怎么遍历、怎么排除、怎么计数——绝不碰真机注册表。
/// （真实的 WindowsRegistryReader 涉及真实系统，不做单元测试，由人工验收。）
/// </summary>
public class RegistrySnapshotterTests
{
    /// <summary>假的注册表：一个字典，键是 “根键” 或 “根键\相对路径”，值是键数据。</summary>
    private sealed class FakeRegistryReader : IRegistryReader
    {
        public Dictionary<string, RegistryKeyData> Keys = new(StringComparer.OrdinalIgnoreCase);

        public RegistryKeyData? ReadKey(string rootName, string subKeyPath)
        {
            var full = subKeyPath.Length == 0 ? rootName : rootName + "\\" + subKeyPath;
            return Keys.TryGetValue(full, out var data) ? data : null;
        }
    }

    private static RegistryKeyData Key(string[] subKeys, params RegistryValueData[] values) => new(subKeys, values);

    private static RegistryValueData Val(string name, RegistryValueKind kind, object? raw) => new(name, kind, raw);

    [Fact]
    public void 会记录键本身和每个值()
    {
        var reader = new FakeRegistryReader();
        reader.Keys["HKEY_CURRENT_USER"] = Key(new[] { "Software" });
        reader.Keys["HKEY_CURRENT_USER\\Software"] = Key(new[] { "Foo" });
        reader.Keys["HKEY_CURRENT_USER\\Software\\Foo"] = Key(Array.Empty<string>(), Val("Name", RegistryValueKind.String, "bar"));

        var entries = new List<RegistryEntry>();
        var counts = new SnapshotCounts();
        var options = new SnapshotOptions { RegistryRoots = { "HKEY_CURRENT_USER" } };

        new RegistrySnapshotter(reader).Capture(options, counts, entries.Add);

        Assert.Equal(3, counts.RegistryKeyCount);   // HKCU 根 + Software + Foo 三个键本身
        Assert.Equal(1, counts.RegistryValueCount); // Foo 的一个值
        Assert.Contains(entries, e => e.ValueName is null && e.KeyPath == "HKEY_CURRENT_USER\\Software\\Foo");
        Assert.Contains(entries, e => e.ValueName == "Name" && e.Data == "bar" && e.TypeName == "REG_SZ");
    }

    [Fact]
    public void 排除前缀会剪掉整个子树()
    {
        var reader = new FakeRegistryReader();
        reader.Keys["HKEY_CURRENT_USER"] = Key(new[] { "Software" });
        reader.Keys["HKEY_CURRENT_USER\\Software"] = Key(new[] { "Noisy", "Good" });
        reader.Keys["HKEY_CURRENT_USER\\Software\\Noisy"] = Key(Array.Empty<string>(), Val("N", RegistryValueKind.String, "噪音"));
        reader.Keys["HKEY_CURRENT_USER\\Software\\Good"] = Key(Array.Empty<string>(), Val("V", RegistryValueKind.DWord, 7));

        var entries = new List<RegistryEntry>();
        var counts = new SnapshotCounts();
        var options = new SnapshotOptions
        {
            RegistryRoots = { "HKEY_CURRENT_USER" },
            ExcludedRegistryPrefixes = { "Software\\Noisy" },
        };

        new RegistrySnapshotter(reader).Capture(options, counts, entries.Add);

        Assert.DoesNotContain(entries, e => e.KeyPath.Contains("Noisy", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(entries, e => e.ValueName == "V" && e.Data == "7"); // 没被排除的键正常扫到
    }

    [Fact]
    public void 排除前缀大小写不敏感()
    {
        var reader = new FakeRegistryReader();
        reader.Keys["HKEY_CURRENT_USER"] = Key(new[] { "Software" });
        reader.Keys["HKEY_CURRENT_USER\\Software"] = Key(new[] { "NOISY" });
        reader.Keys["HKEY_CURRENT_USER\\Software\\NOISY"] = Key(Array.Empty<string>());

        var entries = new List<RegistryEntry>();
        var options = new SnapshotOptions
        {
            RegistryRoots = { "HKEY_CURRENT_USER" },
            ExcludedRegistryPrefixes = { "software\\noisy" }, // 小写排除大写键
        };

        new RegistrySnapshotter(reader).Capture(options, counts: new SnapshotCounts(), entries.Add);

        Assert.DoesNotContain(entries, e => e.KeyPath.Contains("NOISY"));
    }

    [Fact]
    public void 读不到的键会被计数跳过()
    {
        var reader = new FakeRegistryReader(); // 空字典 = 全都读不到（模拟无权限）

        var entries = new List<RegistryEntry>();
        var counts = new SnapshotCounts();
        var options = new SnapshotOptions { RegistryRoots = { "HKEY_CURRENT_USER" } };

        new RegistrySnapshotter(reader).Capture(options, counts, entries.Add);

        Assert.Empty(entries);
        Assert.Equal(1, counts.SkippedRegistryKeyCount); // 根键本身读不到，计一次
    }
}
