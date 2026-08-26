namespace Acmp.Shared.Infrastructure.Configuration;

/*
 * WBS-24.5 (DW-036 / FR-155, NFR-059, NFR-060; DEC-080 / SC-035) — one row of the externalized
 * runtime configuration store the data architecture specifies at SEC-103: Key (unique), Value holding
 * JSON, Scope, Guid key. SEC-080 names this table as where "retention settings for legal/compliance to
 * set later" live, which is why retention is configured HERE rather than in appsettings.
 *
 * ⚠ THE TYPE IS `ConfigurationSetting` AND THE TABLE IS `Configuration`. The spec names the table
 * `Configuration`; a C# type of that name would collide with `Microsoft.Extensions.Configuration` and
 * with EF's own `IEntityTypeConfiguration` convention, under which every `*Configuration.cs` in this
 * solution is a mapping class rather than an entity. The table name follows the spec; the type name
 * avoids a collision that already misleads greps — searching `class Configuration` returns only EF
 * mapping classes, which is how the absence of this table went unnoticed until 2026-08-27.
 *
 * ⚠ SECRETS DO NOT BELONG HERE. SEC-103 is explicit: "Secrets stay in env/secret store, not here."
 * Nothing enforces that at the type level, so it is stated where a writer will read it.
 *
 * WRITE-AUDITED, NOT WRITE-ONCE. Unlike AuditEvent next door, this row is meant to change: SEC-077
 * classifies a "retention/immutability config change" as a privileged action that must be AUDITED, so
 * mutation is legitimate and it is the audit trail that makes it accountable.
 */
public sealed class ConfigurationSetting
{
    private ConfigurationSetting() { }

    private ConfigurationSetting(string key, string valueJson, string scope)
    {
        Id = Guid.NewGuid();
        Key = key;
        ValueJson = valueJson;
        Scope = scope;
    }

    public Guid Id { get; private set; }

    /// <summary>Unique across the store. Dotted, lowercase by convention: `retention.topic.years`.</summary>
    public string Key { get; private set; } = string.Empty;

    /// <summary>The value, as JSON. JSON rather than a scalar so a period, a unit and a note travel together.</summary>
    public string ValueJson { get; private set; } = string.Empty;

    /// <summary>Groups keys for display and authorization, e.g. `retention`.</summary>
    public string Scope { get; private set; } = string.Empty;

    public static ConfigurationSetting Create(string key, string valueJson, string scope) =>
        new(Normalize(key), valueJson, Normalize(scope));

    /// <summary>Replaces the value. The KEY and SCOPE are identity here and never move.</summary>
    public void SetValue(string valueJson) => ValueJson = valueJson;

    // Keys are matched exactly by the unique index, so casing and stray whitespace would silently create
    // a SECOND row for what a reader means as one setting. Normalising on the way in is cheaper than a
    // duplicate nobody can see.
    private static string Normalize(string s) => s.Trim().ToLowerInvariant();
}
