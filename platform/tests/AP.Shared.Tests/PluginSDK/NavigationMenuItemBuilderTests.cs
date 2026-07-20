using AP.Shared.PluginSDK.Navigation;
using FluentAssertions;
using Xunit;

namespace AP.Shared.Tests.PluginSDK;

public class NavigationMenuItemBuilderTests
{
    [Fact]
    public void Build_ShouldReturnEmpty_WhenNoContributors()
    {
        var result = NavigationMenuItemBuilder.Build(Array.Empty<INavigationContributor>(), _ => true);
        result.Should().BeEmpty();
    }

    [Fact]
    public void Build_ShouldCollectAndSortItemsByOrder()
    {
        var contributors = new INavigationContributor[]
        {
            new TestContributor(new NavigationMenuItem
            {
                Label = "B",
                NavigationTarget = "BView",
                Order = 200
            }),
            new TestContributor(new NavigationMenuItem
            {
                Label = "A",
                NavigationTarget = "AView",
                Order = 100
            })
        };

        var result = NavigationMenuItemBuilder.Build(contributors, _ => true);

        result.Should().HaveCount(2);
        result[0].NavigationTarget.Should().Be("AView");
        result[1].NavigationTarget.Should().Be("BView");
    }

    [Fact]
    public void Build_ShouldFilterItemsByPermission()
    {
        var contributors = new INavigationContributor[]
        {
            new TestContributor(
                new NavigationMenuItem { Label = "Allowed", NavigationTarget = "AllowedView", Order = 100, Permission = "allowed" },
                new NavigationMenuItem { Label = "Denied", NavigationTarget = "DeniedView", Order = 200, Permission = "denied" })
        };

        var result = NavigationMenuItemBuilder.Build(contributors, p => p == "allowed");

        result.Should().HaveCount(1);
        result[0].NavigationTarget.Should().Be("AllowedView");
        result[0].IsDefault.Should().BeTrue("唯一可见项应被设为默认");
    }

    [Fact]
    public void Build_ShouldApplyVisibilityFilter()
    {
        var contributors = new INavigationContributor[]
        {
            new TestContributor(
                new NavigationMenuItem { Label = "Show", NavigationTarget = "ShowView", Order = 100 },
                new NavigationMenuItem { Label = "Hide", NavigationTarget = "HideView", Order = 200 })
        };

        var result = NavigationMenuItemBuilder.Build(contributors, _ => true, visibilityFilter: i => i.NavigationTarget == "ShowView");

        result.Should().HaveCount(1);
        result[0].NavigationTarget.Should().Be("ShowView");
    }

    [Fact]
    public void Build_ShouldHonorDefaultTargetFromConfiguration()
    {
        var contributors = new INavigationContributor[]
        {
            new TestContributor(
                new NavigationMenuItem { Label = "Home", NavigationTarget = "HomeView", Order = 100 },
                new NavigationMenuItem { Label = "Settings", NavigationTarget = "SettingsView", Order = 200 })
        };

        var result = NavigationMenuItemBuilder.Build(contributors, _ => true, "SettingsView");

        result.Should().HaveCount(2);
        result.Single(i => i.NavigationTarget == "SettingsView").IsDefault.Should().BeTrue();
        result.Single(i => i.NavigationTarget == "HomeView").IsDefault.Should().BeFalse();
    }

    [Fact]
    public void Build_ShouldDeduplicateByNavigationTarget_PickLowerOrder()
    {
        var contributors = new INavigationContributor[]
        {
            new TestContributor(new NavigationMenuItem { Label = "First", NavigationTarget = "SameView", Order = 50 }),
            new TestContributor(new NavigationMenuItem { Label = "Second", NavigationTarget = "SameView", Order = 10 })
        };

        var result = NavigationMenuItemBuilder.Build(contributors, _ => true);

        result.Should().HaveCount(1);
        result[0].Label.Should().Be("Second");
        result[0].Order.Should().Be(10);
    }

    [Fact]
    public void Build_ShouldIgnoreItemsWithEmptyTargetOrLabel()
    {
        var contributors = new INavigationContributor[]
        {
            new TestContributor(
                new NavigationMenuItem { Label = "Valid", NavigationTarget = "ValidView", Order = 100 },
                new NavigationMenuItem { Label = "", NavigationTarget = "EmptyLabelView", Order = 200 },
                new NavigationMenuItem { Label = "EmptyTarget", NavigationTarget = "", Order = 300 })
        };

        var result = NavigationMenuItemBuilder.Build(contributors, _ => true);

        result.Should().HaveCount(1);
        result[0].NavigationTarget.Should().Be("ValidView");
    }

    private sealed class TestContributor : INavigationContributor
    {
        private readonly NavigationMenuItem[] _items;

        public TestContributor(params NavigationMenuItem[] items)
        {
            _items = items;
        }

        public IEnumerable<NavigationMenuItem> GetMenuItems() => _items;
    }
}
