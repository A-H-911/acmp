using Acmp.Shared.Domain.Entities;
using Acmp.Shared.Infrastructure.Audit;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Acmp.Application.Tests.Audit;

// ADR-0026 (PR1 step 3) — the SaveChanges interceptor records changed scalar deltas of each mutated
// AuditableEntity into the request-scoped buffer, keyed by (SubjectType, PublicId).
public class AuditCaptureInterceptorTests
{
    private sealed class Widget : AuditableEntity
    {
        public string Name { get; set; } = string.Empty;
        public int Count { get; set; }
    }

    private sealed class TestDb : DbContext
    {
        private readonly string _name;
        private readonly AuditCaptureInterceptor _interceptor;
        public TestDb(string name, AuditCaptureInterceptor interceptor) { _name = name; _interceptor = interceptor; }
        public DbSet<Widget> Widgets => Set<Widget>();
        protected override void OnConfiguring(DbContextOptionsBuilder o) =>
            o.UseInMemoryDatabase(_name).AddInterceptors(_interceptor);
    }

    [Fact]
    public async Task Captures_added_then_modified_scalar_deltas()
    {
        var name = "cap-" + Guid.NewGuid();
        var buffer = new AuditChangeBuffer();
        var interceptor = new AuditCaptureInterceptor(buffer);

        Guid pid;
        await using (var db = new TestDb(name, interceptor))
        {
            var w = new Widget { Name = "A", Count = 1 };
            db.Add(w);
            await db.SaveChangesAsync();
            pid = w.PublicId;
        }

        var added = buffer.Take("Widget", pid.ToString());
        added.Should().NotBeNull();
        added!.BeforeJson.Should().BeNull("an insert has no prior state");
        added.AfterJson.Should().Contain("\"Name\":\"A\"").And.Contain("\"Count\":1");

        await using (var db = new TestDb(name, interceptor))
        {
            var w = await db.Widgets.FirstAsync();
            w.Count = 5;
            await db.SaveChangesAsync();
        }

        var modified = buffer.Take("Widget", pid.ToString());
        modified.Should().NotBeNull();
        modified!.BeforeJson.Should().Contain("\"Count\":1");
        modified.AfterJson.Should().Contain("\"Count\":5");
        modified.AfterJson.Should().NotContain("Name", "only the changed property is in the delta");
    }

    [Fact]
    public async Task Captures_deleted_scalar_state_as_before_only()
    {
        var name = "cap-" + Guid.NewGuid();
        var buffer = new AuditChangeBuffer();
        var interceptor = new AuditCaptureInterceptor(buffer);

        Guid pid;
        await using (var db = new TestDb(name, interceptor))
        {
            var w = new Widget { Name = "Z", Count = 9 };
            db.Add(w);
            await db.SaveChangesAsync();
            pid = w.PublicId;
        }

        buffer.Take("Widget", pid.ToString()); // drain the insert capture

        await using (var db = new TestDb(name, interceptor))
        {
            var w = await db.Widgets.FirstAsync();
            db.Remove(w);
            await db.SaveChangesAsync();
        }

        var deleted = buffer.Take("Widget", pid.ToString());
        deleted.Should().NotBeNull();
        deleted!.BeforeJson.Should().Contain("\"Name\":\"Z\"").And.Contain("\"Count\":9");
        deleted.AfterJson.Should().BeNull("a delete has no post-state");
    }

    [Fact]
    public void Take_matches_type_and_id_exactly_and_never_guesses()
    {
        var buffer = new AuditChangeBuffer();
        buffer.Add(new AuditChange("Vote", "id-1", null, "{}"));

        buffer.Take("Vote", "id-2").Should().BeNull("a wrong id must not match a different capture");
        buffer.Take("Risk", "id-1").Should().BeNull("a wrong type must not match");
        buffer.Take("Vote", "id-1").Should().NotBeNull();
        buffer.Take("Vote", "id-1").Should().BeNull("a consumed capture is removed");
    }

    [Fact]
    public void Take_without_an_id_matches_the_first_capture_of_that_type()
    {
        var buffer = new AuditChangeBuffer();
        buffer.Add(new AuditChange("Vote", "id-1", null, "{}"));

        buffer.Take("Risk", null).Should().BeNull("no capture of that type exists");
        buffer.Take("Vote", null).Should().NotBeNull("the single-entity case matches by type alone");
        buffer.Take("Vote", null).Should().BeNull("the matched capture was removed");
    }
}
