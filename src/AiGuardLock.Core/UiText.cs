using System.Globalization;

namespace AiGuardLock.Core;

public static class UiText
{
    public static string Get(string japanese, string english) =>
        CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "ja" ? japanese : english;

    public static void Configure(string[] args)
    {
        var selections = args.Where(value => value.StartsWith("--language=", StringComparison.Ordinal)).ToArray();
        if (selections.Length == 0) return;
        if (selections.Length != 1 || selections[0] is not ("--language=en" or "--language=ja"))
            throw new ArgumentException("Use one language option: --language=en or --language=ja.", nameof(args));
        CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(selections[0]["--language=".Length..]);
    }
}
