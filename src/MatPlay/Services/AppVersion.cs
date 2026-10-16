namespace MatPlay.Services;

/// <summary>Die laufende Version: <c>APP_VERSION</c> aus dem Build (CI), lokal <c>local-&lt;datum&gt;</c>.</summary>
public static class AppVersion
{
    public static string Current { get; } =
        Environment.GetEnvironmentVariable("APP_VERSION") is { Length: > 0 } version
            ? version
            : $"local-{DateTime.UtcNow:yyyyMMdd}";
}
