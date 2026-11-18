using System.IO.Compression;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SnapDiff.Core;

/// <summary>
/// 把快照写成一个 .snapdiff 文件：内容是 JSON Lines，整体用 GZip 压缩。
/// 用法：new 一个 → 扫描过程中不断调 WriteRegistry / WriteFile → 最后 Complete(counts) → Dispose。
/// 边扫边写、逐行落盘，所以就算扫到几十万条记录，内存占用也很小。
/// </summary>
public sealed class SnapshotWriter : IDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = false,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private readonly StreamWriter _writer;

    // 文件扫描是多线程并行的，多条线程会同时往这里送记录——写文件必须加锁串行化
    private readonly object _gate = new();

    /// <summary>创建快照文件并立刻写入 header（元数据）行。</summary>
    public SnapshotWriter(string path, SnapshotMetadata metadata)
    {
        var fileStream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None);
        var gzip = new GZipStream(fileStream, CompressionLevel.Fastest);
        _writer = new StreamWriter(gzip, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        WriteLine(new SnapshotLine { Kind = SnapshotFile.KindHeader, Header = metadata });
    }

    /// <summary>写入一条注册表记录（键本身 或 一个值）。</summary>
    public void WriteRegistry(RegistryEntry entry) =>
        WriteLine(new SnapshotLine { Kind = SnapshotFile.KindRegistry, Registry = entry });

    /// <summary>写入一条文件记录。</summary>
    public void WriteFile(FileEntry entry) =>
        WriteLine(new SnapshotLine { Kind = SnapshotFile.KindFile, File = entry });

    /// <summary>扫描结束后调用：写入 footer（统计数字）并把缓冲落盘。</summary>
    public void Complete(SnapshotCounts counts)
    {
        WriteLine(new SnapshotLine { Kind = SnapshotFile.KindFooter, Footer = counts });
        _writer.Flush();
    }

    private void WriteLine(SnapshotLine line)
    {
        lock (_gate)
        {
            _writer.WriteLine(JsonSerializer.Serialize(line, JsonOptions));
        }
    }

    public void Dispose() => _writer.Dispose();
}
