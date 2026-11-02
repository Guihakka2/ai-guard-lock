using System.Globalization;
using AiGuardLock.Core;

var previous = CultureInfo.CurrentUICulture;
var passed = 0;
void Check(bool condition, string name)
{
    if (!condition) throw new InvalidOperationException(name);
    passed++; Console.WriteLine("PASS: " + name);
}
try
{
    foreach (var culture in new[] { "en-US", "fr-FR", "de-DE" })
    {
        CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(culture);
        Check(UiText.Get("日本語", "English") == "English", "English fallback: " + culture);
    }
    CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("ja-JP");
    Check(UiText.Get("日本語", "English") == "日本語", "Japanese culture keeps Japanese");
    UiText.Configure(new[] { "--language=en" });
    Check(UiText.Get("日本語", "English") == "English", "explicit English override");
    var englishError = "";
    try { using var invalid = new PasswordCredential("123"); } catch (ArgumentException error) { englishError = error.Message; }
    Check(englishError.Contains("PIN", StringComparison.Ordinal) && englishError.Contains("digits", StringComparison.Ordinal), "English invalid credential explanation");
    UiText.Configure(new[] { "--language=ja" });
    Check(UiText.Get("日本語", "English") == "日本語", "explicit Japanese override");
    var japaneseError = "";
    try { using var invalid = new PasswordCredential("123"); } catch (ArgumentException error) { japaneseError = error.Message; }
    Check(japaneseError.Contains("数字", StringComparison.Ordinal), "Japanese invalid credential explanation");
    UiText.Configure(new[] { "--other-option" });
    Check(UiText.Get("日本語", "English") == "日本語", "unrelated arguments do not alter language");
    foreach (var languageArgs in new[] { new[] { "--language=fr" }, new[] { "--language=" }, new[] { "--language=en", "--language=ja" } })
    {
        var rejected = false;
        try { UiText.Configure(languageArgs); } catch (ArgumentException) { rejected = true; }
        Check(rejected, "invalid or duplicate language arguments rejected");
        Check(UiText.Get("日本語", "English") == "日本語", "invalid input does not change language");
    }
    Console.WriteLine($"{passed} localization checks passed.");
}
finally { CultureInfo.CurrentUICulture = previous; }
