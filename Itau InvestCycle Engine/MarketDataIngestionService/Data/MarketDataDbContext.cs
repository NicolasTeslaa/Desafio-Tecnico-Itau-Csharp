using ClassLibrary.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace MarketDataIngestionService.Data;

public sealed class MarketDataDbContext : DbContext
{
    public MarketDataDbContext(DbContextOptions<MarketDataDbContext> options) : base(options) { }

    public DbSet<Cotacoes> Cotacoes => Set<Cotacoes>();
    public DbSet<IngestaoJob> IngestaoJobs => Set<IngestaoJob>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Cotacoes>(e =>
        {
            e.ToTable("cotacoes");
            e.Property(x => x.Ticker).HasMaxLength(12).IsRequired();
            e.Property(x => x.PrecoAbertura).HasPrecision(18, 2);
            e.Property(x => x.PrecoFechamento).HasPrecision(18, 2);
            e.Property(x => x.PrecoMaximo).HasPrecision(18, 2);
            e.Property(x => x.PrecoMinimo).HasPrecision(18, 2);
        });

        modelBuilder.Entity<IngestaoJob>(e =>
        {
            e.ToTable("ingestao_jobs");
            e.HasKey(x => x.Id);
            e.Property(x => x.File).HasMaxLength(255).IsRequired();
            e.Property(x => x.StoredPath).HasMaxLength(1024).IsRequired();
            e.Property(x => x.Status).HasMaxLength(20).IsRequired();
            e.Property(x => x.Error).HasMaxLength(2000);

            e.HasIndex(x => x.CreatedAtUtc).HasDatabaseName("ix_ingestao_jobs_createdatutc");
            e.HasIndex(x => x.Status).HasDatabaseName("ix_ingestao_jobs_status");
        });
    }
}
