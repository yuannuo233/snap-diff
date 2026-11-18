using System.Security.Cryptography;

namespace SnapDiff.Core;

/// <summary>文件哈希工具：流式计算 SHA256；读不了（权限/被占用）就返回 null，不抛异常。</summary>
public static class FileHasher
{
    /// <summary>计算文件的 SHA256，返回小写十六进制（64 个字符）。</summary>
    public static string? ComputeSha256(string path)
    {
        try
        {
            using var stream = new FileStream(
                path,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                bufferSize: 1024 * 1024,
                FileOptions.SequentialScan);
            return Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or IOException)
        {
            return null; // 读不了就算了：哈希留空，对比时自动退回“大小 + 修改时间”
        }
    }
}
