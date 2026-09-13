using System.Reflection;
using Acmp.Modules.Integrations.Webex;
using Acmp.Shared.Contracts.Notifications;
using FluentAssertions;

namespace Acmp.Application.Tests.Notifications;

// AC-160: "the event types are one catalog, and a test fails if any module publishes an event type the
// catalog does not name". Every module declares its categories as `const string Category*` fields on its
// *Notifications classes; this reads them from EVERY Acmp.Modules.* assembly in the test output, so a new
// module is covered without editing this file. Both directions are checked: a module category the catalog
// lacks (the preferences page could never offer it), and a catalog entry no module declares (a dead row, the
// shape DEF-164 found in the SPA).
public class NotificationCatalogTests
{
    private static IReadOnlyList<(string Owner, string Value)> ModuleCategories()
    {
        var found = new List<(string, string)>();
        foreach (var path in Directory.GetFiles(AppContext.BaseDirectory, "Acmp.Modules.*.dll"))
        {
            foreach (var type in Assembly.LoadFrom(path).GetTypes().Where(t => t.Name.EndsWith("Notifications", StringComparison.Ordinal)))
            {
                foreach (var f in type.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)
                             .Where(f => f.IsLiteral && f.FieldType == typeof(string) && f.Name.StartsWith("Category", StringComparison.Ordinal)))
                    found.Add(($"{type.FullName}.{f.Name}", (string)f.GetRawConstantValue()!));
            }
        }
        return found;
    }

    [Fact]
    public void The_scan_has_a_subject()
    {
        // A scan that found nothing would pass the next two tests vacuously; 24 today, across seven modules.
        ModuleCategories().Should().HaveCountGreaterThanOrEqualTo(24);
    }

    [Fact]
    public void Every_category_a_module_declares_is_in_the_catalog()
    {
        ModuleCategories().Where(c => !NotificationCategories.Contains(c.Value))
            .Should().BeEmpty("the preferences page can only offer event types the catalog names");
    }

    [Fact]
    public void Every_catalog_entry_is_declared_by_a_module()
    {
        var declared = ModuleCategories().Select(c => c.Value).ToHashSet(StringComparer.Ordinal);
        NotificationCategories.All.Select(c => c.Name).Where(n => !declared.Contains(n))
            .Should().BeEmpty("a catalog row no module publishes is a toggle that does nothing");
    }

    [Fact]
    public void Catalog_names_are_unique_and_every_group_is_a_known_heading()
    {
        NotificationCategories.All.Select(c => c.Name).Should().OnlyHaveUniqueItems();
        NotificationCategories.All.Select(c => c.Group).Distinct()
            .Should().BeSubsetOf(new[] { "meetings", "topics", "decisions", "actions", "risks", "governance", "notifications" });
    }

    [Fact]
    public void The_webex_allowlist_names_only_catalog_categories()
    {
        WebexEligibleEvents.Categories.Where(c => !NotificationCategories.Contains(c)).Should().BeEmpty();
    }

    [Fact] // AC-161 (DEC-188): the two digests are catalog rows a member can switch, and never reach the shared space.
    public void Both_digest_types_are_in_the_catalog_under_notifications_and_not_in_the_webex_space_set()
    {
        NotificationCategories.All.Where(c => c.Group == "notifications").Select(c => c.Name)
            .Should().Equal(NotificationCategories.DailyDigest, NotificationCategories.WeeklyDigest);
        WebexEligibleEvents.Includes(NotificationCategories.DailyDigest).Should().BeFalse();
        WebexEligibleEvents.Includes(NotificationCategories.WeeklyDigest).Should().BeFalse();
    }

    [Fact]
    public void Contains_is_ordinal_and_rejects_unknown_names()
    {
        NotificationCategories.Contains("AgendaPublished").Should().BeTrue();
        NotificationCategories.Contains("agendapublished").Should().BeFalse();
        NotificationCategories.Contains("MinutesReady").Should().BeFalse(); // DEF-164's dead SPA key
    }
}
