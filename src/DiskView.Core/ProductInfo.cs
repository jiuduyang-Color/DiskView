namespace DiskView.Core;

/// <summary>产品级常量，安装器与卸载逻辑共用。</summary>
public static class ProductInfo
{
    public const string Version = "1.0.0";
    public const string DisclaimerVersion = "1.0";
    public const string Publisher = "DiskView";
    public const string ExeName = "DiskSpaceAnalyzer.exe";

#if APP_LANG_EN
    public const string FolderName = "DiskSpaceAnalyzer-EN";
    public const string UninstallKey = @"Software\Microsoft\Windows\CurrentVersion\Uninstall\DiskSpaceAnalyzerEN";
#else
    public const string FolderName = "DiskSpaceAnalyzer";
    public const string UninstallKey = @"Software\Microsoft\Windows\CurrentVersion\Uninstall\DiskSpaceAnalyzer";
#endif
}
