using MatPlatform;
using MatPlay.Platform;

namespace MatPlay.Tests;

/// <summary>Die Definition von MatPlay ist für den Validator von MatPlatform gültig, in beiden Sprachen und gegen MatPlays Kopie des MatUi-Sprites.</summary>
public sealed class PlatformValidatorTests(PlayFactory site) : IClassFixture<PlayFactory>
{
    [Theory]
    [InlineData("de")]
    [InlineData("en")]
    public void The_description_is_valid(string language)
        => Assert.Empty(MatPlatformValidator.Validate(MatPlatformBuilder.Build(PlayPlatform.Describe, language), Sprite()));

    [Fact]
    public async Task The_served_definition_is_valid()
        => Assert.Empty(MatPlatformValidator.Validate(await site.CreateClient().GetStringAsync("/api"), Sprite()));

    [Fact]
    public void The_settings_are_few_and_the_roles_are_the_ones_of_the_app()
    {
        var definition = MatPlatformBuilder.Build(PlayPlatform.Describe, "de");
        Assert.InRange(definition.Configure.Count, 1, 10);
        Assert.Equal(["admin", "user"], definition.Roles!.Select(r => r.Key));
        Assert.Equal("matplay", definition.App.Id);
        Assert.Equal("#00819b", definition.App.Accent);
    }

    private static IReadOnlySet<string> Sprite() => MatPlatformValidator.ReadSprite(Repo.Sprite());
}
