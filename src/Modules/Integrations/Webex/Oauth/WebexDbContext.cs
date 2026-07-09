using Microsoft.EntityFrameworkCore;

namespace Acmp.Modules.Integrations.Webex.Oauth;

// The Webex adapter's own tiny schema — holds only the encrypted OAuth token row. Kept isolated in a "webex"
// schema so it never touches domain module tables (ADR-0001). Provisioned by the API's MigrationRunner.
public sealed class WebexDbContext : DbContext
{
    public const string Schema = "webex";

    public WebexDbContext(DbContextOptions<WebexDbContext> options) : base(options) { }

    public DbSet<WebexToken> Tokens => Set<WebexToken>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);
        var token = modelBuilder.Entity<WebexToken>();
        token.ToTable("oauth_tokens");
        token.HasKey(x => x.Id);
        token.Property(x => x.Id).ValueGeneratedNever();
        token.Property(x => x.AccessTokenCipher).HasMaxLength(4000);
        token.Property(x => x.RefreshTokenCipher).HasMaxLength(4000);
    }
}
