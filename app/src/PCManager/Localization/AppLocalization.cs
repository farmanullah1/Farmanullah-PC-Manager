using System.Globalization;
using System.Resources;
using System.Windows;

namespace PCManager.Localization;

public sealed record LocalizedStrings(string Language, bool RightToLeft);

public static class AppLocalization
{
    private static readonly ResourceManager Resources =
        new("PCManager.Properties.Strings", typeof(AppLocalization).Assembly);

    public static LocalizedStrings Apply(string? language)
    {
        bool urdu = string.Equals(language, "ur", StringComparison.OrdinalIgnoreCase);
        var culture = urdu
            ? CultureInfo.GetCultureInfo("ur-PK")
            : CultureInfo.GetCultureInfo("en-US");

        CultureInfo.CurrentCulture = culture;
        CultureInfo.CurrentUICulture = culture;
        CultureInfo.DefaultThreadCurrentCulture = culture;
        CultureInfo.DefaultThreadCurrentUICulture = culture;

        return new LocalizedStrings(urdu ? "ur" : "en", urdu);
    }

    public static string Get(string key) =>
        Resources.GetString(key, CultureInfo.CurrentUICulture) ?? key;
}
