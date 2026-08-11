using Acmp.Modules.Membership.Infrastructure.Identity;
using FluentAssertions;

namespace Acmp.Application.Tests.Membership;

// ADR-0038 / ADR-0032 — a half-configured identity integration must stop the host AT BOOT, not at
// the first invite. The validator runs under ValidateOnStart for the same reason the Webex options
// do: a missing credential that only surfaces when an administrator clicks a button is discovered by
// the administrator, in front of a user they are trying to add.
public class KeycloakAdminOptionsTests
{
    private static KeycloakAdminOptions Valid() => new()
    {
        Enabled = true,
        BaseUrl = "https://acmp.example/kc/",
        Realm = "acmp",
        ClientId = "acmp-admin-sa",
        ClientSecret = "a-real-secret",
    };

    [Fact]
    public void Disabled_needs_no_configuration_at_all()
    {
        // Off by default, so an environment without the service account simply cannot invite —
        // rather than booting into a feature that fails on use.
        var result = new KeycloakAdminOptionsValidator().Validate(null, new KeycloakAdminOptions());
        result.Succeeded.Should().BeTrue();
    }

    [Fact]
    public void A_fully_configured_client_passes() =>
        new KeycloakAdminOptionsValidator().Validate(null, Valid()).Succeeded.Should().BeTrue();

    [Theory]
    [InlineData(nameof(KeycloakAdminOptions.BaseUrl))]
    [InlineData(nameof(KeycloakAdminOptions.Realm))]
    [InlineData(nameof(KeycloakAdminOptions.ClientId))]
    [InlineData(nameof(KeycloakAdminOptions.ClientSecret))]
    public void Enabled_but_missing_any_required_value_fails_and_NAMES_the_value(string missing)
    {
        var options = Valid();
        switch (missing)
        {
            case nameof(KeycloakAdminOptions.BaseUrl): options.BaseUrl = ""; break;
            case nameof(KeycloakAdminOptions.Realm): options.Realm = ""; break;
            case nameof(KeycloakAdminOptions.ClientId): options.ClientId = ""; break;
            default: options.ClientSecret = ""; break;
        }

        var result = new KeycloakAdminOptionsValidator().Validate(null, options);

        result.Failed.Should().BeTrue();
        // Naming the missing key is the difference between a five-second fix and a bisect.
        result.FailureMessage.Should().Contain(missing);
    }

    [Fact]
    public void The_CHANGE_ME_placeholder_is_rejected()
    {
        var options = Valid();
        options.ClientSecret = "CHANGE_ME";

        var result = new KeycloakAdminOptionsValidator().Validate(null, options);

        // A placeholder that boots is worse than one that does not: it looks configured. The cloud
        // env template ships CHANGE_ME for every secret, so this is the realistic failure.
        result.Failed.Should().BeTrue();
        result.FailureMessage.Should().Contain("CHANGE_ME");
    }

    [Fact]
    public void Several_missing_values_are_reported_together()
    {
        var options = Valid();
        options.ClientId = "";
        options.ClientSecret = "";

        var result = new KeycloakAdminOptionsValidator().Validate(null, options);

        // One boot, one complete list — not a fix-and-rerun loop.
        result.FailureMessage.Should().Contain(nameof(KeycloakAdminOptions.ClientId));
        result.FailureMessage.Should().Contain(nameof(KeycloakAdminOptions.ClientSecret));
    }
}
