using MatPlatform;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;

// Die Factories geben ihre Einstellungen über Umgebungsvariablen an Program (Program liest sie, bevor der Host gebaut wird),
// deshalb laufen die Testklassen nacheinander.
[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace MatPlay.Tests;

/// <summary>
/// Die ganze App im Speicher auf einem eigenen leeren Datenordner (MATPLAY_DATA). <see cref="ApiKey"/> ist <c>MAT_API_KEY</c>, außer
/// <c>withKey</c> ist false; mit einer <c>authority</c> ist die Anmeldung über MatIdentity an, mit einer festen OpenID-Konfiguration,
/// damit kein MatIdentity laufen muss (die Tests sehen sich nur die Weiterleitungen an).
/// </summary>
public class PlayFactory : WebApplicationFactory<Program>
{
    public const string ApiKey = "test-key-0123456789";

    private readonly string _data = Path.Combine(Path.GetTempPath(), "matplay-tests-" + Guid.NewGuid().ToString("N"));
    private readonly string? _authority;
    private readonly bool _reachable;

    public PlayFactory()
        : this(withKey: true, authority: null)
    {
    }

    protected PlayFactory(bool withKey, string? authority, bool reachable = true)
    {
        _authority = authority;
        _reachable = reachable;
        Environment.SetEnvironmentVariable("MATPLAY_DATA", _data);
        Environment.SetEnvironmentVariable(MatPlatformSettings.ApiKeyVariable, withKey ? ApiKey : null);
        Environment.SetEnvironmentVariable(MatPlatformSettings.AuthorityVariable, authority);
        Environment.SetEnvironmentVariable(MatPlatformSettings.ClientIdVariable, null);
        Environment.SetEnvironmentVariable(MatPlatformSettings.ClientSecretVariable, authority is null ? null : "test-secret");
        Environment.SetEnvironmentVariable(MatPlatformSettings.InternalUrlVariable, null);
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");

        if (_authority is not null && _reachable)
        {
            var configuration = new OpenIdConnectConfiguration
            {
                Issuer = _authority,
                AuthorizationEndpoint = _authority + "/connect/authorize",
                TokenEndpoint = _authority + "/connect/token",
                EndSessionEndpoint = _authority + "/connect/logout",
            };

            builder.ConfigureTestServices(services =>
            {
                services.PostConfigure<OpenIdConnectOptions>(MatIdentitySignIn.Scheme, options =>
                {
                    options.Configuration = configuration;
                    options.ConfigurationManager = new StaticConfigurationManager<OpenIdConnectConfiguration>(configuration);
                });
                services.PostConfigure<JwtBearerOptions>(MatApiAccess.BearerScheme, options =>
                {
                    options.Configuration = configuration;
                    options.ConfigurationManager = new StaticConfigurationManager<OpenIdConnectConfiguration>(configuration);
                });
            });
        }
    }

    /// <summary>Ein Client, der Cookies behält und Weiterleitungen nicht folgt (die Tests sehen sich jeden Schritt an).</summary>
    public HttpClient Browser() => CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = true });

    public HttpClient Api()
    {
        var client = CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        client.DefaultRequestHeaders.Add("X-Api-Key", ApiKey);
        return client;
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        try
        {
            Directory.Delete(_data, recursive: true);
        }
        catch (IOException)
        {
            // SQLite hält die Datei evtl. noch kurz; den Temp-Ordner räumt das System später auf.
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}

/// <summary>Ohne <c>MAT_API_KEY</c>: die geschützten Endpunkte antworten jedem mit 401.</summary>
public sealed class NoKeyFactory() : PlayFactory(withKey: false, authority: null);

/// <summary>Mit <c>MATIDENTITY_AUTHORITY</c> (ein MatIdentity, das nicht laufen muss).</summary>
public sealed class IdentityFactory() : PlayFactory(withKey: true, authority: IdentityFactory.Authority)
{
    public const string Authority = "http://identity.test";
}

/// <summary>Mit <c>MATIDENTITY_AUTHORITY</c> auf eine Adresse, unter der nichts antwortet (MatIdentity ist aus).</summary>
public sealed class UnreachableIdentityFactory() : PlayFactory(withKey: true, authority: "http://127.0.0.1:9", reachable: false);

/// <summary>Hilfen der Tests.</summary>
internal static class Repo
{
    /// <summary>Der Ordner mit <c>MatPlay.slnx</c>.</summary>
    public static string Root()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "MatPlay.slnx")))
        {
            dir = dir.Parent;
        }

        return dir!.FullName;
    }

    public static string WebRoot() => Path.Combine(Root(), "src", "MatPlay", "wwwroot");

    public static string Sprite() => Path.Combine(WebRoot(), "matui", "matui-icons.svg");
}
