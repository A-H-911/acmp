using Microsoft.Extensions.Options;

namespace Acmp.Modules.Integrations.Webex;

// Fail-closed config validation (audit finding M1). When the adapter is ENABLED, the token-encryption key
// must be a real secret — present, long enough, and not the shipped CHANGE_ME placeholder. Without this an
// operator who flips Webex:Enabled but leaves the placeholder would encrypt the persisted OAuth access/refresh
// tokens under a publicly derivable key (SHA-256 of "CHANGE_ME…"). Wired with ValidateOnStart, so a weak key
// is a boot failure, not a silent at-rest exposure. When disabled the adapter isn't wired at all → no key needed.
public sealed class WebexOptionsValidator : IValidateOptions<WebexOptions>
{
    private const int MinKeyLength = 16;

    public ValidateOptionsResult Validate(string? name, WebexOptions options)
    {
        if (!options.Enabled)
            return ValidateOptionsResult.Success;

        var key = options.TokenEncryptionKey;
        if (string.IsNullOrWhiteSpace(key) || key.Length < MinKeyLength ||
            key.StartsWith("CHANGE_ME", StringComparison.OrdinalIgnoreCase))
            return ValidateOptionsResult.Fail(
                $"Webex:TokenEncryptionKey must be a real secret of at least {MinKeyLength} characters when Webex:Enabled is true " +
                "(the persisted OAuth tokens are encrypted with it). Set a strong random value in the environment.");

        return ValidateOptionsResult.Success;
    }
}
