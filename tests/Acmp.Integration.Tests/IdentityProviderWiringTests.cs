using Acmp.Modules.Membership.Application.Abstractions;
using Acmp.Modules.Membership.Infrastructure;
using Acmp.Modules.Membership.Infrastructure.Identity;
using Acmp.Shared;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Acmp.Integration.Tests;

// ADR-0038 — the Keycloak Admin client is registered ONLY when configured. An environment without
// the service-account credential must be UNABLE to invite, rather than booting into a feature that
// fails at the first click; and an environment that half-configures it must fail at BOOT.
//
// No container or realm needed: building the provider runs the registration lambdas, which is
// exactly the branch under test.
public sealed class IdentityProviderWiringTests
{
    private static IConfiguration Config(params (string Key, string Value)[] settings)
    {
        var values = new Dictionary<string, string?>
        {
            ["ConnectionStrings:Acmp"] = "Server=unused;Database=Acmp;TrustServerCertificate=True",
        };
        foreach (var (key, value) in settings) values[key] = value;
        return new ConfigurationBuilder().AddInMemoryCollection(values).Build();
    }

    private static ServiceProvider Build(IConfiguration config)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSharedKernel(config);
        services.AddMembershipModule(config);
        return services.BuildServiceProvider();
    }

    [Fact]
    public void Without_configuration_there_is_no_identity_provider_at_all()
    {
        using var provider = Build(Config());
        using var scope = provider.CreateScope();

        // Not "registered but disabled" — absent. The feature cannot be exercised by accident, and
        // the failure if someone tries is a missing dependency at composition, not a runtime 500
        // in front of an administrator adding a colleague.
        scope.ServiceProvider.GetService<IIdentityProvider>().Should().BeNull();
    }

    [Fact]
    public void When_enabled_and_configured_the_Keycloak_client_resolves_with_its_base_address()
    {
        using var provider = Build(Config(
            ("KeycloakAdmin:Enabled", "true"),
            ("KeycloakAdmin:BaseUrl", "https://acmp.example/kc"), // deliberately WITHOUT a trailing slash
            ("KeycloakAdmin:Realm", "acmp"),
            ("KeycloakAdmin:ClientId", "acmp-admin-sa"),
            ("KeycloakAdmin:ClientSecret", "a-real-secret")));
        using var scope = provider.CreateScope();

        scope.ServiceProvider.GetService<IIdentityProvider>().Should().BeOfType<KeycloakAdminClient>();

        var options = scope.ServiceProvider.GetRequiredService<IOptions<KeycloakAdminOptions>>().Value;
        options.Realm.Should().Be("acmp");
        options.Enabled.Should().BeTrue();
    }

    [Fact]
    public void A_BaseUrl_without_a_trailing_slash_still_resolves_relative_paths()
    {
        using var provider = Build(Config(
            ("KeycloakAdmin:Enabled", "true"),
            ("KeycloakAdmin:BaseUrl", "https://acmp.example/kc"),
            ("KeycloakAdmin:Realm", "acmp"),
            ("KeycloakAdmin:ClientId", "acmp-admin-sa"),
            ("KeycloakAdmin:ClientSecret", "a-real-secret")));
        using var scope = provider.CreateScope();

        // AddHttpClient<TClient, TImplementation> names the client after the SERVICE type, not the
        // implementation — asking for "KeycloakAdminClient" silently returns a default client with
        // no BaseAddress, which would make this test pass for the wrong reason if it asserted absence.
        var factory = scope.ServiceProvider.GetRequiredService<IHttpClientFactory>();
        var client = factory.CreateClient(nameof(IIdentityProvider));
        client.BaseAddress.Should().NotBeNull("the named client must be the configured one");

        // Without the trailing slash, Uri resolution silently DROPS the last segment — so every
        // admin call would go to /admin/... instead of /kc/admin/... behind nginx, and the symptom
        // would be a 404 that looks like a missing permission rather than a malformed base address.
        new Uri(client.BaseAddress!, "admin/realms/acmp/users").AbsolutePath
            .Should().Be("/kc/admin/realms/acmp/users");
    }
}
