using Acmp.Modules.Knowledge.Application.Contracts;
using Acmp.Modules.Knowledge.Domain;

namespace Acmp.Modules.Knowledge.Application.Internal;

// Aggregate → read-model projection, shared by the queries and the command return values. All lookups are
// in-module (knowledge schema only) — a document never joins another module's tables (ADR-0001).
internal static class KnowledgeMapping
{
    public static DocumentSummaryDto ToSummary(Document d) => new(
        d.PublicId, d.Key, d.Title, d.Status.ToString(), d.Category, d.Tags.ToList(),
        d.OwnerUserId, d.Version, d.CreatedAt, d.UpdatedAt);

    public static DocumentDetailDto ToDetail(Document d) => new(
        d.PublicId, d.Key, d.Title, d.Body, d.Status.ToString(), d.Category, d.Tags.ToList(),
        d.OwnerUserId, d.Version,
        d.Versions.OrderBy(v => v.Version).Select(ToVersion).ToList(),
        d.CreatedAt, d.UpdatedAt);

    private static DocumentVersionDto ToVersion(DocumentVersion v) =>
        new(v.PublicId, v.Version, v.Title, v.Body, v.SavedAt, v.SavedByUserId);

    public static TemplateSummaryDto ToSummary(Template t) => new(
        t.PublicId, t.Key, t.Name, t.TargetType.ToString(), t.Status.ToString(), t.Version, t.CreatedAt, t.UpdatedAt);

    public static TemplateDetailDto ToDetail(Template t) => new(
        t.PublicId, t.Key, t.Name, t.TargetType.ToString(), t.Body, t.Status.ToString(), t.Version, t.CreatedAt, t.UpdatedAt);
}
