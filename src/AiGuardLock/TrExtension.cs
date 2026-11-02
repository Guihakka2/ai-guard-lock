using System.Windows.Markup;
using AiGuardLock.Core;

namespace AiGuardLock;

[MarkupExtensionReturnType(typeof(string))]
public sealed class TrExtension(string japanese, string english) : MarkupExtension
{
    public override object ProvideValue(IServiceProvider serviceProvider) => UiText.Get(japanese, english);
}
