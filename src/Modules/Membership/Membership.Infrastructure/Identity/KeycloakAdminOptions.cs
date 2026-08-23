using Microsoft.Extensions.Options;

namespace Acmp.Modules.Membership.Infrastructure.Identity;

// Configuration for the Keycloak Admin service account (ADR-0038).
//
// The secret is FILE-BACKED per ADR-0032: gen-secrets.sh materialises /run/secrets with
// config-key-named files, which AddKeyPerFile binds here — the same path ConnectionStrings__Acmp and
// Minio__SecretKey already take. It is never in source and never in a compose `environment:` block.
public sealed class KeycloakAdminOptions
{
    public const string SectionName = "KeycloakAdmin";

    /// <summary>Enables in-app user management. Off by default so an environment without the credential simply cannot invite.</summary>
    public bool Enabled { get; set; }

    /// <summary>Base URL of the Keycloak server, e.g. https://acmp.example/kc/.</summary>
    public string BaseUrl { get; set; } = string.Empty;

    public string Realm { get; set; } = "acmp";

    /// <summary>The confidential client whose service account holds the minimum realm-management roles — never realm-admin.</summary>
    public string ClientId { get; set; } = string.Empty;

    public string ClientSecret { get; set; } = string.Empty;
}

// Fails the host at startup rather than at the first invite, so a misconfigured environment is
// discovered on deploy instead of by an administrator mid-task. Same ValidateOnStart shape the Webex
// options use — where a missing TokenEncryptionKey stops api and worker booting rather than silently
// disabling the feature.
public sealed class KeycloakAdminOptionsValidator : IValidateOptions<KeycloakAdminOptions>
{
    public ValidateOptionsResult Validate(string? name, KeycloakAdminOptions options)
    {
        if (!options.Enabled)
            return ValidateOptionsResult.Success;

        var missing = new List<string>();
        if (string.IsNullOrWhiteSpace(options.BaseUrl)) missing.Add(nameof(options.BaseUrl));
        if (string.IsNullOrWhiteSpace(options.Realm)) missing.Add(nameof(options.Realm));
        if (string.IsNullOrWhiteSpace(options.ClientId)) missing.Add(nameof(options.ClientId));
        if (string.IsNullOrWhiteSpace(options.ClientSecret)) missing.Add(nameof(options.ClientSecret));

        if (missing.Count > 0)
            return ValidateOptionsResult.Fail(
                $"KeycloakAdmin is enabled but {string.Join(", ", missing)} {(missing.Count == 1 ? "is" : "are")} not configured.");

        if (options.ClientSecret is "CHANGE_ME")
            return ValidateOptionsResult.Fail("KeycloakAdmin:ClientSecret is still the CHANGE_ME placeholder.");

        return ValidateOptionsResult.Success;
    }
}
