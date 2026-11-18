using System.IO.Compression;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SnapDiff.Core;

/// <summary>快照文件读取器。全部方法都是流式/逐行的，读几十万条记录也不会占用大内存（除非主动调用 Load 全量载入）。</summary>
public static class SnapshotReader
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    /// <summary>只读文件第一行（头部元数据），非常快。</summary>
    public static SnapshotMetadata ReadHeader(string path)
    {
        using var fileStream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        using var gzip = new GZipStream(fileStream, CompressionMode.Decompress);
        using var reader = new StreamReader(gzip, Encoding.UTF8);

        var line = reader.ReadLine();
        if (string.IsNullOrEmpty(line))
            throw new InvalidDataException("快照文件是空的：" + path);
        var parsed = JsonSerializer.Deserialize<SnapshotLine>(line, JsonOptions);
        if (parsed?.Kind != SnapshotFile.KindHeader || parsed.Header is null)
            throw new InvalidDataException("快照文件缺少头部（header）行：" + path);
        return parsed.Header;
    }

    /// <summary>
    /// 逐行流式读取整个快照文件（第一行 header，中间是记录，最后是 footer）。
    /// 返回的是延迟枚举：读一行解一行，适合边读边处理。
    /// </summary>
    public static IEnumerable<SnapshotLine> ReadLines(string path)
    {
        using var fileStream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        using var gzip = new GZipStream(fileStream, CompressionMode.Decompress);
        using var reader = new StreamReader(gzip, Encoding.UTF8);

        string? line;
        while ((line = reader.ReadLine()) is not null)
        {
            if (line.Length == 0) continue;
            var parsed = JsonSerializer.Deserialize<SnapshotLine>(line, JsonOptions);
            if (parsed is not null) yield return parsed;
        }
    }

    /// <summary>
    /// 把整个快照完整载入内存（对比时用于“快照 A”——它需要被装进字典；快照 B 走流式）。
    /// </summary>
    public static SnapshotData Load(string path)
    {
        var data = new SnapshotData { Metadata = ReadHeader(path) };
        foreach (var line in ReadLines(path))
        {
            switch (line.Kind)
            {
                case SnapshotFile.KindRegistry:
                    if (line.Registry is not null) data.RegistryEntries.Add(line.Registry);
                    break;
                case SnapshotFile.KindFile:
                    if (line.File is not null) data.FileEntries.Add(line.File);
                    break;
                case SnapshotFile.KindFooter:
                    data.Counts = line.Footer;
                    break;
            }
        }
        return data;
    }
}
