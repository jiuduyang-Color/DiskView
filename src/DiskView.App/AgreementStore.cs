using System.IO;
using DiskView.Core;

namespace DiskView.App;

/// <summary>记录用户已同意的免责声明版本，下次启动时不再重复提示。</summary>
internal static class AgreementStore
{
    private static string FilePath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        ProductInfo.FolderName,
        "agreement.txt");

    private static string Token(string version) => $"{(Loc.IsEnglish ? "en" : "zh")}:{version}";

    public static bool IsAccepted()
    {
        try
        {
            if (!File.Exists(FilePath)) return false;
            return string.Equals(File.ReadAllText(FilePath).Trim(), Token(ProductInfo.DisclaimerVersion),
                StringComparison.Ordinal);
        }
        catch
        {
            return false;
        }
    }

    public static void Accept()
    {
        try
        {
            string? folder = Path.GetDirectoryName(FilePath);
            if (folder != null) Directory.CreateDirectory(folder);
            File.WriteAllText(FilePath, Token(ProductInfo.DisclaimerVersion));
        }
        catch
        {
            // 写入失败时下次启动会再次提示，不影响本次使用
        }
    }
}
