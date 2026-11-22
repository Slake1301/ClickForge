using System.Windows.Markup;
using ClickForge.Core;

namespace ClickForge.UI;

[MarkupExtensionReturnType(typeof(string))]
public sealed class TranslateExtension(string russian) : MarkupExtension
{
    public override object ProvideValue(IServiceProvider serviceProvider) => UiText.Get(russian);
}
