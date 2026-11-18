using SnapDiff.Core;
using Xunit;

namespace SnapDiff.Tests;

/// <summary>对比引擎测试：新增 / 删除 / 修改的识别规则（不碰真实注册表和真实系统文件）。</summary>
public class DiffEngineTests
{
    private static RegistryEntry Reg(string keyPath, string? valueName, string type = "", string data = "")
        => new() { KeyPath = keyPath, ValueName = valueName, TypeName = type, Data = data };

    private static FileEntry MakeFile(string root, string rel, long size = 1, string? sha = null, int minute = 0)
        => new()
        {
            RootPath = root,
            RelativePath = rel,
            Size = size,
            ModifiedTimeUtc = new DateTime(2026, 1, 1, 0, minute, 0, DateTimeKind.Utc),
            Sha256 = sha,
        };

    [Fact]
    public void 新建的键和值会被识别为新增()
    {
        var a = new[] { Reg("HKCU\\Software\\Old", null) };
        var b = new[]
        {
            Reg("HKCU\\Software\\Old", null),
            Reg("HKCU\\Software\\NewApp", null),
            Reg("HKCU\\Software\\NewApp", "InstallPath", "REG_SZ", "C:\\NewApp"),
        };

        var result = SnapshotComparer.Diff(a, Array.Empty<FileEntry>(), b, Array.Empty<FileEntry>());

        Assert.Equal(2, result.RegistryChanges.Count(c => c.Kind == ChangeKind.Added));
        Assert.DoesNotContain(result.RegistryChanges, c => c.Kind == ChangeKind.Removed);
        Assert.DoesNotContain(result.RegistryChanges, c => c.Kind == ChangeKind.Modified);
    }

    [Fact]
    public void 消失的键和值会被识别为删除()
    {
        var a = new[]
        {
            Reg("HKCU\\Software\\Gone", null),
            Reg("HKCU\\Software\\Gone", "Setting", "REG_SZ", "x"),
        };
        var b = Array.Empty<RegistryEntry>();

        var result = SnapshotComparer.Diff(a, Array.Empty<FileEntry>(), b, Array.Empty<FileEntry>());

        Assert.Equal(2, result.RegistryChanges.Count(c => c.Kind == ChangeKind.Removed));
        Assert.NotNull(result.RegistryChanges[0].Before);
        Assert.Null(result.RegistryChanges[0].After);
        Assert.DoesNotContain(result.RegistryChanges, c => c.Kind == ChangeKind.Added);
    }

    [Fact]
    public void 数据或类型变化会被识别为修改()
    {
        var a = new[]
        {
            Reg("HKCU\\K", "Data", "REG_SZ", "old"),
            Reg("HKCU\\K", "Type", "REG_SZ", "same"),
        };
        var b = new[]
        {
            Reg("HKCU\\K", "Data", "REG_SZ", "new"),      // 数据变了
            Reg("HKCU\\K", "Type", "REG_DWORD", "same"),  // 类型变了（数据相同）
        };

        var result = SnapshotComparer.Diff(a, Array.Empty<FileEntry>(), b, Array.Empty<FileEntry>());

        Assert.Equal(2, result.RegistryChanges.Count(c => c.Kind == ChangeKind.Modified));
        Assert.Equal("old", result.RegistryChanges[0].Before!.Data);
        Assert.Equal("new", result.RegistryChanges[0].After!.Data);
    }

    [Fact]
    public void 完全相同的条目不会报变化()
    {
        var a = new[] { Reg("HKCU\\K", "V", "REG_SZ", "一样"), Reg("HKCU\\K", null) };
        var b = new[] { Reg("HKCU\\K", "V", "REG_SZ", "一样"), Reg("HKCU\\K", null) };

        var result = SnapshotComparer.Diff(a, Array.Empty<FileEntry>(), b, Array.Empty<FileEntry>());

        Assert.Equal(0, result.Summary.Total());
    }

    [Fact]
    public void 文件新增和删除的识别()
    {
        var a = new[] { MakeFile("C:\\R", "gone.txt", 3) };
        var b = new[] { MakeFile("C:\\R", "fresh.txt", 9, minute: 2) };

        var result = SnapshotComparer.Diff(Array.Empty<RegistryEntry>(), a, Array.Empty<RegistryEntry>(), b);

        Assert.Equal(1, result.Summary.AddedFiles);
        Assert.Equal(1, result.Summary.RemovedFiles);
        Assert.Equal(0, result.Summary.ModifiedFiles);
    }

    [Fact]
    public void 文件大小或修改时间变化算修改()
    {
        var a = new[]
        {
            MakeFile("C:\\App", "a.dll", size: 100, minute: 1),
            MakeFile("C:\\App", "b.dll", size: 100, minute: 1),
        };
        var b = new[]
        {
            MakeFile("C:\\App", "a.dll", size: 200, minute: 1), // 大小变了
            MakeFile("C:\\App", "b.dll", size: 100, minute: 2), // 修改时间变了
        };

        var result = SnapshotComparer.Diff(Array.Empty<RegistryEntry>(), a, Array.Empty<RegistryEntry>(), b);

        Assert.Equal(2, result.Summary.ModifiedFiles);
    }

    [Fact]
    public void 大小时间相同但哈希不同算修改_哈希缺失则不算()
    {
        // 两边都有哈希且不同 → 修改
        var a1 = new[] { MakeFile("C:\\App", "x.dll", 100, "aaaa", 1) };
        var b1 = new[] { MakeFile("C:\\App", "x.dll", 100, "bbbb", 1) };
        Assert.Equal(1, SnapshotComparer.Diff(Array.Empty<RegistryEntry>(), a1, Array.Empty<RegistryEntry>(), b1).Summary.ModifiedFiles);

        // 哈希缺失（比如文件太大没算）→ 退回“大小+时间”判断，不算变化
        var a2 = new[] { MakeFile("C:\\App", "x.dll", 100, null, 1) };
        var b2 = new[] { MakeFile("C:\\App", "x.dll", 100, null, 1) };
        Assert.Equal(0, SnapshotComparer.Diff(Array.Empty<RegistryEntry>(), a2, Array.Empty<RegistryEntry>(), b2).Summary.Total());
    }

    [Fact]
    public void 路径和键名大小写不敏感()
    {
        // Windows 路径和注册表键名都不区分大小写，同一个文件/键写法不同也算同一条
        var fileA = new[] { MakeFile("c:\\app", "X.DLL", 1) };
        var fileB = new[] { MakeFile("C:\\APP", "x.dll", 1) };
        Assert.Equal(0, SnapshotComparer.Diff(Array.Empty<RegistryEntry>(), fileA, Array.Empty<RegistryEntry>(), fileB).Summary.Total());

        var regA = new[] { Reg("HKCU\\Software", "V", "REG_SZ", "1") };
        var regB = new[] { Reg("hkcu\\software", "v", "REG_SZ", "1") };
        Assert.Equal(0, SnapshotComparer.Diff(regA, Array.Empty<FileEntry>(), regB, Array.Empty<FileEntry>()).Summary.Total());
    }

    [Fact]
    public void 快照文件对比与内存对比结果一致()
    {
        // 这是“序列化往返后对比结果一致”的端到端验证
        var dir = TestPaths.NewTempDir();
        var pathA = Path.Combine(dir, "a.snapdiff");
        var pathB = Path.Combine(dir, "b.snapdiff");

        var regA = new[] { Reg("HKCU\\K1", "V", "REG_SZ", "old") };
        var regB = new[] { Reg("HKCU\\K1", "V", "REG_SZ", "new"), Reg("HKCU\\K2", null) };
        var fileA = new[] { MakeFile("C:\\R", "gone.txt", 3), MakeFile("C:\\R", "same.txt", 3) };
        var fileB = new[] { MakeFile("C:\\R", "same.txt", 3), MakeFile("C:\\R", "fresh.txt", 9) };

        SnapshotTestHelper.WriteSnapshot(pathA, regA, fileA);
        SnapshotTestHelper.WriteSnapshot(pathB, regB, fileB);

        var fromMemory = SnapshotComparer.Diff(regA, fileA, regB, fileB);
        var fromFiles = SnapshotComparer.DiffFiles(pathA, pathB);

        Assert.Equal(fromMemory.Summary.Total(), fromFiles.Summary.Total());
        Assert.Equal(fromMemory.Summary.AddedRegistry, fromFiles.Summary.AddedRegistry);
        Assert.Equal(fromMemory.Summary.ModifiedRegistry, fromFiles.Summary.ModifiedRegistry);
        Assert.Equal(fromMemory.Summary.AddedFiles, fromFiles.Summary.AddedFiles);
        Assert.Equal(fromMemory.Summary.RemovedFiles, fromFiles.Summary.RemovedFiles);

        Assert.Equal(1, fromFiles.Summary.AddedRegistry);    // 新键 K2（键本身记录）
        Assert.Equal(1, fromFiles.Summary.ModifiedRegistry); // K1 的值 V 数据变了
        Assert.Equal(1, fromFiles.Summary.AddedFiles);       // fresh.txt
        Assert.Equal(1, fromFiles.Summary.RemovedFiles);     // gone.txt
        Assert.NotNull(fromFiles.SnapshotA);
        Assert.NotNull(fromFiles.SnapshotB);
    }
}
