using System.IO.Compression;
using SnapDiff.Core;
using Xunit;

namespace SnapDiff.Tests;

/// <summary>快照存储测试：写出去的 .snapdiff 文件必须能完整读回来（逐字段一致）。</summary>
public class StorageRoundTripTests
{
    [Fact]
    public void 文件确实是GZip格式()
    {
        var dir = TestPaths.NewTempDir();
        var path = Path.Combine(dir, "s.snapdiff");
        SnapshotTestHelper.WriteSnapshot(path, Array.Empty<RegistryEntry>(), Array.Empty<FileEntry>());

        using var fs = File.OpenRead(path);
        Assert.Equal(0x1F, fs.ReadByte()); // GZip 魔数第一字节
        Assert.Equal(0x8B, fs.ReadByte()); // GZip 魔数第二字节
    }

    [Fact]
    public void 元数据和选项能完整往返()
    {
        var dir = TestPaths.NewTempDir();
        var path = Path.Combine(dir, "s.snapdiff");

        var metadata = new SnapshotMetadata
        {
            CreatedAtUtc = new DateTime(2026, 1, 2, 3, 4, 5, DateTimeKind.Utc),
            MachineName = "测试机器",
            UserName = "张三",
            AppVersion = "0.1",
            Options = new SnapshotOptions
            {
                RegistryRoots = { "HKEY_CURRENT_USER", "HKEY_LOCAL_MACHINE" },
                ExcludedRegistryPrefixes = { "AppEvents", "Software\\Microsoft\\Tracing" },
                FileSystemRoots = { "C:\\Program Files", "D:\\工具" },
                ComputeSha256 = true,
            },
        };

        using (var writer = new SnapshotWriter(path, metadata))
        {
            writer.Complete(new SnapshotCounts());
        }

        var header = SnapshotReader.ReadHeader(path);
        Assert.Equal(SnapshotFile.CurrentFormatVersion, header.FormatVersion);
        Assert.Equal(metadata.CreatedAtUtc, header.CreatedAtUtc);
        Assert.Equal("测试机器", header.MachineName);
        Assert.Equal("张三", header.UserName);
        Assert.Equal(2, header.Options.RegistryRoots.Count);
        Assert.Contains("AppEvents", header.Options.ExcludedRegistryPrefixes);
        Assert.Contains("D:\\工具", header.Options.FileSystemRoots);
        Assert.True(header.Options.ComputeSha256);
    }

    [Fact]
    public void 注册表和文件条目能完整往返_含特殊字符()
    {
        var dir = TestPaths.NewTempDir();
        var path = Path.Combine(dir, "s.snapdiff");

        var registry = new List<RegistryEntry>
        {
            new() { KeyPath = "HKCU\\软件\\测试", ValueName = null }, // 键本身
            new() { KeyPath = "HKCU\\软件\\测试", ValueName = "", TypeName = "REG_SZ", Data = "默认值内容" },
            new() { KeyPath = "HKCU\\软件\\测试", ValueName = "带,逗号\"引号\n换行", TypeName = "REG_BINARY", Data = "AA BB …(共 40 字节)" },
        };
        var files = new List<FileEntry>
        {
            new() { RootPath = "C:\\App", RelativePath = "子目录\\文件.txt", Size = 12345, ModifiedTimeUtc = new DateTime(2026, 5, 6, 7, 8, 9, DateTimeKind.Utc), Sha256 = null },
            new() { RootPath = "C:\\App", RelativePath = "x.dll", Size = 1, ModifiedTimeUtc = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc), Sha256 = "abcd" },
        };

        using (var writer = new SnapshotWriter(path, new SnapshotMetadata()))
        {
            foreach (var entry in registry) writer.WriteRegistry(entry);
            foreach (var entry in files) writer.WriteFile(entry);
            writer.Complete(new SnapshotCounts());
        }

        var data = SnapshotReader.Load(path);
        Assert.NotNull(data.Counts);
        Assert.Equal(3, data.RegistryEntries.Count);
        Assert.Equal(2, data.FileEntries.Count);

        for (int i = 0; i < registry.Count; i++)
        {
            Assert.Equal(registry[i].KeyPath, data.RegistryEntries[i].KeyPath);
            Assert.Equal(registry[i].ValueName, data.RegistryEntries[i].ValueName);
            Assert.Equal(registry[i].TypeName, data.RegistryEntries[i].TypeName);
            Assert.Equal(registry[i].Data, data.RegistryEntries[i].Data);
            Assert.Equal(registry[i].GetIdentity(), data.RegistryEntries[i].GetIdentity());
        }

        for (int i = 0; i < files.Count; i++)
        {
            Assert.Equal(files[i].RootPath, data.FileEntries[i].RootPath);
            Assert.Equal(files[i].RelativePath, data.FileEntries[i].RelativePath);
            Assert.Equal(files[i].Size, data.FileEntries[i].Size);
            Assert.Equal(files[i].ModifiedTimeUtc, data.FileEntries[i].ModifiedTimeUtc);
            Assert.Equal(files[i].Sha256, data.FileEntries[i].Sha256);
        }
    }

    [Fact]
    public void 往返之后对比结果保持一致()
    {
        var dir = TestPaths.NewTempDir();
        var pathA = Path.Combine(dir, "a.snapdiff");
        var pathB = Path.Combine(dir, "b.snapdiff");

        var regA = new[] { new RegistryEntry { KeyPath = "HKCU\\K", ValueName = "V", TypeName = "REG_SZ", Data = "旧" } };
        var regB = new[] { new RegistryEntry { KeyPath = "HKCU\\K", ValueName = "V", TypeName = "REG_SZ", Data = "新" } };
        SnapshotTestHelper.WriteSnapshot(pathA, regA, Array.Empty<FileEntry>());
        SnapshotTestHelper.WriteSnapshot(pathB, regB, Array.Empty<FileEntry>());

        var before = SnapshotComparer.Diff(regA, Array.Empty<FileEntry>(), regB, Array.Empty<FileEntry>());
        var after = SnapshotComparer.DiffFiles(pathA, pathB);

        Assert.Equal(before.Summary.Total(), after.Summary.Total());
        Assert.Equal(1, after.Summary.ModifiedRegistry);
        Assert.Equal("旧", after.RegistryChanges[0].Before!.Data);
        Assert.Equal("新", after.RegistryChanges[0].After!.Data);
    }

    [Fact]
    public void 空快照也能往返()
    {
        var dir = TestPaths.NewTempDir();
        var path = Path.Combine(dir, "empty.snapdiff");
        SnapshotTestHelper.WriteSnapshot(path, Array.Empty<RegistryEntry>(), Array.Empty<FileEntry>());

        var data = SnapshotReader.Load(path);
        Assert.Empty(data.RegistryEntries);
        Assert.Empty(data.FileEntries);
    }
}
