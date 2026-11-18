using System.Text;
using SnapDiff.Core;
using Xunit;

namespace SnapDiff.Tests;

/// <summary>CSV 导出测试：重点是字段转义必须符合 RFC 4180，Excel 才能正确打开。</summary>
public class CsvExporterTests
{
    [Theory]
    [InlineData("", "")]
    [InlineData("abc", "abc")]
    [InlineData("普通中文", "普通中文")]
    [InlineData("a,b", "\"a,b\"")]                       // 含逗号 → 加引号
    [InlineData("he said \"hi\"", "\"he said \"\"hi\"\"\"")] // 引号翻倍再加引号
    [InlineData("line1\nline2", "\"line1\nline2\"")]     // 含换行 → 加引号
    [InlineData("a\r\nb", "\"a\r\nb\"")]                 // CRLF 同样加引号
    public void 字段转义符合RFC4180(string input, string expected)
        => Assert.Equal(expected, CsvExporter.EscapeField(input));

    [Fact]
    public void 导出的文件带BOM且表头正确()
    {
        var dir = TestPaths.NewTempDir();
        var path = Path.Combine(dir, "report.csv");
        CsvExporter.Export(new DiffResult(), path);

        var bytes = File.ReadAllBytes(path);
        Assert.Equal(new byte[] { 0xEF, 0xBB, 0xBF }, bytes[..3]); // UTF-8 BOM
        var text = Encoding.UTF8.GetString(bytes[3..]);
        Assert.StartsWith("变化类别,变化类型,根,路径,值名,", text);
    }

    [Fact]
    public void 含逗号引号换行的数据能被RFC4180解析器还原()
    {
        var dir = TestPaths.NewTempDir();
        var path = Path.Combine(dir, "report.csv");

        var diff = new DiffResult();
        diff.RegistryChanges.Add(new RegistryChange(
            ChangeKind.Modified,
            new RegistryEntry { KeyPath = "HKCU\\K", ValueName = "V", TypeName = "REG_SZ", Data = "旧,数据\"带引号\"\n第二行" },
            new RegistryEntry { KeyPath = "HKCU\\K", ValueName = "V", TypeName = "REG_SZ", Data = "新数据" }));

        CsvExporter.Export(diff, path);

        var rows = ParseCsv(File.ReadAllText(path));
        Assert.True(rows.Count >= 2);
        var dataRow = rows[1];
        // 表头列序（0 起）：0类别 1类型 2根 3路径 4值名 5旧类型 6旧数据 7新类型 8新数据 …
        Assert.Equal("注册表", dataRow[0]);
        Assert.Equal("修改", dataRow[1]);
        Assert.Equal("HKCU\\K", dataRow[2]);
        Assert.Equal("V", dataRow[4]);
        Assert.Equal("旧,数据\"带引号\"\n第二行", dataRow[6]);
        Assert.Equal("新数据", dataRow[8]);
    }

    [Fact]
    public void 文件行的大小时间哈希列正确()
    {
        var dir = TestPaths.NewTempDir();
        var path = Path.Combine(dir, "report.csv");

        var diff = new DiffResult();
        diff.FileChanges.Add(new FileChange(
            ChangeKind.Added,
            null,
            new FileEntry { RootPath = "C:\\P", RelativePath = "新文件.txt", Size = 42, ModifiedTimeUtc = new DateTime(2026, 2, 3, 4, 5, 6, DateTimeKind.Utc), Sha256 = "abc123" }));

        CsvExporter.Export(diff, path);

        var rows = ParseCsv(File.ReadAllText(path));
        var dataRow = rows[1];
        Assert.Equal("文件", dataRow[0]);
        Assert.Equal("新增", dataRow[1]);
        Assert.Equal("C:\\P", dataRow[2]);
        Assert.Equal("新文件.txt", dataRow[3]);
        Assert.Equal("", dataRow[9]); // 旧大小（新增没有旧值）
        Assert.Equal("42", dataRow[10]);
        Assert.Equal("", dataRow[11]); // 旧修改时间（新增没有旧值）
        Assert.Equal("2026-02-03 04:05:06 (UTC)", dataRow[12]);
        Assert.Equal("abc123", dataRow[14]);
    }

    /// <summary>一个迷你的 RFC 4180 解析器，专门用来验证我们导出的 CSV 是不是合法。</summary>
    private static List<string[]> ParseCsv(string text)
    {
        var rows = new List<string[]>();
        var row = new List<string>();
        var field = new StringBuilder();
        bool inQuotes = false;

        for (int i = 0; i < text.Length; i++)
        {
            char ch = text[i];
            if (inQuotes)
            {
                if (ch == '"')
                {
                    if (i + 1 < text.Length && text[i + 1] == '"')
                    {
                        field.Append('"');
                        i++;
                    }
                    else
                    {
                        inQuotes = false;
                    }
                }
                else
                {
                    field.Append(ch);
                }
            }
            else if (ch == '"')
            {
                inQuotes = true;
            }
            else if (ch == ',')
            {
                row.Add(field.ToString());
                field.Clear();
            }
            else if (ch == '\n')
            {
                row.Add(field.ToString());
                field.Clear();
                rows.Add(row.ToArray());
                row.Clear();
            }
            else if (ch == '\r')
            {
                // 裸 \r 忽略（CRLF 里的 \r 已由 \n 分支处理）
            }
            else
            {
                field.Append(ch);
            }
        }

        if (field.Length > 0 || row.Count > 0)
        {
            row.Add(field.ToString());
            rows.Add(row.ToArray());
        }
        return rows;
    }
}
