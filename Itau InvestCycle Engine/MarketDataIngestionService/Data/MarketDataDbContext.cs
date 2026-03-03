using ClassLibrary.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace MarketDataIngestionService.Data;

public sealed class MarketDataDbContext : DbContext
{
    public MarketDataDbContext(DbContextOptions<MarketDataDbContext> options) : base(options) { }

    public DbSet<Cotacoes> Cotacoes => Set<Cotacoes>();

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
    }
}
