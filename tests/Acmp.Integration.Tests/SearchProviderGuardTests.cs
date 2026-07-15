using Acmp.Modules.Decisions.Infrastructure.Persistence;
using Acmp.Modules.Decisions.Infrastructure.Search;
using Acmp.Modules.Governance.Infrastructure.Persistence;
using Acmp.Modules.Governance.Infrastructure.Search;
using Acmp.Modules.Knowledge.Infrastructure.Persistence;
using Acmp.Modules.Knowledge.Infrastructure.Search;
using Acmp.Modules.Meetings.Infrastructure.Persistence;
using Acmp.Modules.Meetings.Infrastructure.Search;
using Acmp.Modules.Topics.Infrastructure.Persistence;
using Acmp.Modules.Topics.Infrastructure.Search;
using Acmp.Shared.Contracts.Search;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace Acmp.Integration.Tests;

// Fast (no-Docker) coverage of each provider's blank-query short-circuit and its non-SQL-Server (LIKE) branch.
// The blank-query guard is unreachable through the SearchEndpoints host (which trims + returns early before
// fanning out), so it is exercised here by calling the providers directly against an InMemory store.
public sealed class SearchProviderGuardTests
{
    private readonly TestClock _clock = new();
    private readonly TestCurrentUser _user = new();

    public static IEnumerable<object[]> Providers()
    {
        yield return new object[] { "topics" };
        yield return new object[] { "decisions" };
        yield return new object[] { "adrs" };
        yield return new object[] { "minutes" };
        yield return new object[] { "documents" };
    }

    [Theory]
    [MemberData(nameof(Providers))]
    public async Task Blank_query_returns_empty_and_a_term_runs_the_like_branch(string which)
    {
        ISearchProvider provider = Build(which);

        (await provider.SearchAsync("", 5)).Should().BeEmpty();
        (await provider.SearchAsync("   ", 5)).Should().BeEmpty();

        // A real term on an empty InMemory store: the LIKE branch executes and returns nothing without error.
        (await provider.SearchAsync("architecture", 5)).Should().BeEmpty();
    }

    private ISearchProvider Build(string which) => which switch
    {
        "topics" => new TopicSearchProvider(new TopicsDbContext(InMemory<TopicsDbContext>("g-t"), _clock, _user)),
        "decisions" => new DecisionSearchProvider(new DecisionsDbContext(InMemory<DecisionsDbContext>("g-d"), _clock, _user)),
        "adrs" => new AdrSearchProvider(new GovernanceDbContext(InMemory<GovernanceDbContext>("g-g"), _clock, _user)),
        "minutes" => new MinutesSearchProvider(new MeetingsDbContext(InMemory<MeetingsDbContext>("g-m"), _clock, _user)),
        "documents" => new DocumentSearchProvider(new KnowledgeDbContext(InMemory<KnowledgeDbContext>("g-k"), _clock, _user)),
        _ => throw new ArgumentOutOfRangeException(nameof(which)),
    };

    private static DbContextOptions<T> InMemory<T>(string name) where T : DbContext =>
        new DbContextOptionsBuilder<T>().UseInMemoryDatabase(name).Options;
}
