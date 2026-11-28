using System.Globalization;
using PCManager.Localization;
using Xunit;

namespace PCManager.Tests;

public sealed class LocalizationTests
{
    [Fact]
    public void English_resources_resolve_navigation_keys()
    {
        CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("en-US");

        Assert.Equal("Home", AppLocalization.Get("Home"));
        Assert.Equal("Settings", AppLocalization.Get("Settings"));
    }

    [Fact]
    public void Urdu_resources_resolve_navigation_keys()
    {
        CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("ur-PK");

        Assert.Equal("ہوم", AppLocalization.Get("Home"));
        Assert.Equal("ترتیبات", AppLocalization.Get("Settings"));
    }
}
