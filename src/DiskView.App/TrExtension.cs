using System.Windows.Markup;
using DiskView.Core;

namespace DiskView.App;

/// <summary>XAML 中取界面文案：{app:Tr Scan}</summary>
public sealed class TrExtension : MarkupExtension
{
    public TrExtension()
    {
    }

    public TrExtension(string key) => Key = key;

    public string Key { get; set; } = string.Empty;

    public override object ProvideValue(IServiceProvider serviceProvider) => Loc.I[Key];
}
