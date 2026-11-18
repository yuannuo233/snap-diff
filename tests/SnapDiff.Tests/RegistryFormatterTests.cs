using Microsoft.Win32;
using SnapDiff.Core;
using Xunit;

namespace SnapDiff.Tests;

/// <summary>注册表数据格式化测试：只测格式化函数本身，不碰真实注册表。</summary>
public class RegistryFormatterTests
{
    [Fact]
    public void 字符串原样保留()
    {
        Assert.Equal("hello", RegistryValueFormatter.FormatValue(RegistryValueKind.String, "hello"));
        Assert.Equal("%SystemRoot%\\explorer.exe",
            RegistryValueFormatter.FormatValue(RegistryValueKind.ExpandString, "%SystemRoot%\\explorer.exe"));
        Assert.Equal("REG_SZ", RegistryValueFormatter.GetTypeName(RegistryValueKind.String));
        Assert.Equal("REG_EXPAND_SZ", RegistryValueFormatter.GetTypeName(RegistryValueKind.ExpandString));
    }

    [Fact]
    public void DWORD按无符号显示()
    {
        Assert.Equal("42", RegistryValueFormatter.FormatValue(RegistryValueKind.DWord, 42));
        // .NET 把 DWORD 0xFFFFFFFF 读成 int -1，显示时应转回无符号 4294967295
        Assert.Equal("4294967295", RegistryValueFormatter.FormatValue(RegistryValueKind.DWord, -1));
        Assert.Equal("REG_DWORD", RegistryValueFormatter.GetTypeName(RegistryValueKind.DWord));
    }

    [Fact]
    public void QWORD按整数显示()
        => Assert.Equal("123456789012345", RegistryValueFormatter.FormatValue(RegistryValueKind.QWord, 123456789012345L));

    [Fact]
    public void 多字符串用竖线连接()
        => Assert.Equal("a | b | c",
            RegistryValueFormatter.FormatValue(RegistryValueKind.MultiString, new[] { "a", "b", "c" }));

    [Fact]
    public void 二进制超过32字节会截断()
    {
        var data = Enumerable.Range(0, 40).Select(i => (byte)i).ToArray(); // 0x00..0x27

        var first32 = RegistryValueFormatter.FormatBinary(data[..32]);
        Assert.Equal(32, first32.Split(' ').Length); // 32 个十六进制对
        Assert.StartsWith("00", first32);
        Assert.EndsWith("1F", first32);              // 第 32 个字节是 0x1F

        var full = RegistryValueFormatter.FormatValue(RegistryValueKind.Binary, data);
        Assert.StartsWith(first32, full);            // 前 32 字节正常显示
        Assert.EndsWith("…(共 40 字节)", full);       // 截断提示
        Assert.DoesNotContain(" 20 ", full);         // 第 33 个字节（0x20）没有显示
    }

    [Fact]
    public void 短二进制不截断()
    {
        var text = RegistryValueFormatter.FormatValue(RegistryValueKind.Binary, new byte[] { 0xDE, 0xAD });
        Assert.Equal("DE AD", text);
        Assert.DoesNotContain("…", text);
    }

    [Fact]
    public void 空值和NONE类型()
    {
        Assert.Equal("", RegistryValueFormatter.FormatValue(RegistryValueKind.None, null));
        Assert.Equal("", RegistryValueFormatter.FormatValue(RegistryValueKind.Binary, Array.Empty<byte>()));
        Assert.Equal("", RegistryValueFormatter.FormatValue(RegistryValueKind.String, null));
        Assert.Equal("REG_NONE", RegistryValueFormatter.GetTypeName(RegistryValueKind.None));
    }
}
