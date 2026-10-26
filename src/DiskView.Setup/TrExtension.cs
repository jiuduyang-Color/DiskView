using System.Windows;
using System.Windows.Markup;

namespace DiskView.Setup;

/// <summary>XAML 中使用的本地化标记扩展：{app:Tr Key}。</summary>
[MarkupExtensionReturnType(typeof(string))]
public sealed class TrExtension : MarkupExtension
{
    public TrExtension() { }

    public TrExtension(string key) => Key = key;

    [ConstructorArgument("key")]
    public string Key { get; set; } = string.Empty;

    public override object ProvideValue(IServiceProvider serviceProvider) => Core.Loc.I[Key];
}
