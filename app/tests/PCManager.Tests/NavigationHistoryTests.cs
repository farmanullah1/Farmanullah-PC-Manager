using PCManager.Core.Models;
using Xunit;

namespace PCManager.Tests;

public sealed class NavigationHistoryTests
{
    [Fact]
    public void Back_and_forward_return_the_visited_sequence()
    {
        var history = new NavigationHistory<string>();
        history.Push("home");
        history.Push("storage");

        Assert.True(history.TryBack("debloat", out string? storage));
        Assert.Equal("storage", storage);
        Assert.True(history.TryBack("storage", out string? home));
        Assert.Equal("home", home);
        Assert.True(history.TryForward("home", out string? forwardStorage));
        Assert.Equal("storage", forwardStorage);
        Assert.True(history.TryForward("storage", out string? forwardDebloat));
        Assert.Equal("debloat", forwardDebloat);
        Assert.False(history.TryForward("debloat", out _));
    }

    [Fact]
    public void New_navigation_clears_the_forward_branch()
    {
        var history = new NavigationHistory<string>();
        history.Push("home");
        history.Push("storage");
        Assert.True(history.TryBack("storage", out _));

        history.Push("home");
        Assert.False(history.CanGoForward);
    }
}
