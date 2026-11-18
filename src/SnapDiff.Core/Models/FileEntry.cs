namespace SnapDiff.Core;

/// <summary>文件快照里的一条记录：一个文件的大小、修改时间和可选的 SHA256。</summary>
public sealed class FileEntry
{
    /// <summary>这个文件所在的扫描根目录（如 C:\Program Files），相对路径相对它计算。</summary>
    public string RootPath { get; set; } = "";

    /// <summary>相对根目录的路径（如 SubDir\a.dll）。</summary>
    public string RelativePath { get; set; } = "";

    /// <summary>文件大小（字节）。</summary>
    public long Size { get; set; }

    /// <summary>最后修改时间（UTC）。</summary>
    public DateTime ModifiedTimeUtc { get; set; }

    /// <summary>SHA256（小写十六进制，64 个字符）；未计算时为 null。</summary>
    public string? Sha256 { get; set; }

    /// <summary>对比用的“身份”：根目录 + 相对路径。用 \uFFFF 做分隔符避免歧义。</summary>
    public string GetIdentity() => RootPath + "\uFFFF" + RelativePath;

    /// <summary>显示用的完整路径。</summary>
    public string GetFullPath() => Path.Combine(RootPath, RelativePath);
}
