using System.Runtime.Versioning;
using SnapDiff.Core;

// SnapDiff 是 Windows 专用工具；测试项目同样声明平台，避免平台分析器警告刷屏。
[assembly: SupportedOSPlatform("windows")]

namespace SnapDiff.Tests;

/// <summary>测试专用临时目录：全部建在测试自己的 bin 目录下（TestTemp），不碰系统其他任何地方。</summary>
internal static class TestPaths
{
    /// <summary>创建一个全新的临时目录（每次测试用独立的随机名字，互不干扰）。</summary>
    public static string NewTempDir()
    {
        var dir = Path.Combine(AppContext.BaseDirectory, "TestTemp", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        return dir;
    }
}

/// <summary>测试共用的小工具：快速把一批条目写成一个合法的快照文件。</summary>
internal static class SnapshotTestHelper
{
    public static void WriteSnapshot(string path, IEnumerable<RegistryEntry> registry, IEnumerable<FileEntry> files)
    {
        using var writer = new SnapshotWriter(path, new SnapshotMetadata
        {
            CreatedAtUtc = DateTime.UtcNow,
            MachineName = "TEST-MACHINE",
        });
        foreach (var entry in registry) writer.WriteRegistry(entry);
        foreach (var entry in files) writer.WriteFile(entry);
        writer.Complete(new SnapshotCounts());
    }
}
