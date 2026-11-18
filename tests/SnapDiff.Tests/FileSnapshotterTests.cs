using SnapDiff.Core;
using Xunit;

namespace SnapDiff.Tests;

/// <summary>
/// 文件快照测试：临时目录全部建在测试自己的 bin 目录下（TestTemp），
/// 不碰系统其他任何地方；只读测试目录，不修改任何真实用户文件。
/// </summary>
public class FileSnapshotterTests
{
    [Fact]
    public void 能枚举嵌套文件_记录大小_相对路径和哈希()
    {
        var dir = TestPaths.NewTempDir();
        Directory.CreateDirectory(Path.Combine(dir, "sub"));
        File.WriteAllText(Path.Combine(dir, "hello.txt"), "hello"); // 5 字节，SHA256 已知
        File.WriteAllBytes(Path.Combine(dir, "sub", "data.bin"), new byte[] { 1, 2, 3 });

        var entries = new List<FileEntry>();
        var counts = new SnapshotCounts();
        var options = new SnapshotOptions { FileSystemRoots = { dir }, ComputeSha256 = true };

        new FileSnapshotter().Capture(options, counts, entries.Add);

        Assert.Equal(2, counts.FileCount);

        var hello = entries.Single(e => e.RelativePath == "hello.txt");
        Assert.Equal(5, hello.Size);
        Assert.Equal("2cf24dba5fb0a30e26e83b2ac5b9e29e1b161e5c1fa7425e73043362938b9824", hello.Sha256);
        Assert.Equal(dir, hello.RootPath, ignoreCase: true);
        Assert.True(hello.ModifiedTimeUtc > DateTime.UtcNow.AddMinutes(-5)); // 记录的是 UTC 修改时间

        var data = entries.Single(e => e.RelativePath.EndsWith("data.bin", StringComparison.OrdinalIgnoreCase));
        Assert.Equal(3, data.Size);
        Assert.NotNull(data.Sha256);
        Assert.Equal(64, data.Sha256!.Length); // SHA256 十六进制固定 64 个字符
    }

    [Fact]
    public void 关闭哈希时Sha256为空()
    {
        var dir = TestPaths.NewTempDir();
        File.WriteAllText(Path.Combine(dir, "a.txt"), "content");

        var entries = new List<FileEntry>();
        var options = new SnapshotOptions { FileSystemRoots = { dir }, ComputeSha256 = false };

        new FileSnapshotter().Capture(options, new SnapshotCounts(), entries.Add);

        var entry = Assert.Single(entries);
        Assert.Null(entry.Sha256);
    }

    [Fact]
    public void 大于8MB的文件不计算哈希()
    {
        var dir = TestPaths.NewTempDir();
        File.WriteAllBytes(Path.Combine(dir, "big.bin"), new byte[SnapshotOptions.MaxHashFileSizeBytes + 1]); // 8MB + 1 字节

        var entries = new List<FileEntry>();
        var options = new SnapshotOptions { FileSystemRoots = { dir }, ComputeSha256 = true };

        new FileSnapshotter().Capture(options, new SnapshotCounts(), entries.Add);

        var entry = Assert.Single(entries);
        Assert.Equal((long)SnapshotOptions.MaxHashFileSizeBytes + 1, entry.Size);
        Assert.Null(entry.Sha256); // 超过 8MB 不算哈希
    }

    [Fact]
    public void 不存在的根目录会被安全跳过()
    {
        var options = new SnapshotOptions
        {
            FileSystemRoots = { Path.Combine(TestPaths.NewTempDir(), "这个目录不存在") },
        };

        var entries = new List<FileEntry>();
        var counts = new SnapshotCounts();

        new FileSnapshotter().Capture(options, counts, entries.Add); // 不应抛异常

        Assert.Empty(entries);
        Assert.Equal(0, counts.FileCount);
    }
}
