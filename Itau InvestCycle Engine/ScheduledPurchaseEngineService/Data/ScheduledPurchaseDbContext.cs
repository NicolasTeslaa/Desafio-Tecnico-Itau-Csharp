using ClassLibrary.Domain.Entities;
using ClassLibrary.Domain.Entities.Cestas;
using ClassLibrary.Domain.Entities.Clientes;
using ClassLibrary.Domain.Entities.CompraDistribuicao;
using Microsoft.EntityFrameworkCore;

namespace ScheduledPurchaseEngineService.Data;

public sealed class ScheduledPurchaseDbContext : DbContext
{
    public ScheduledPurchaseDbContext(DbContextOptions<ScheduledPurchaseDbContext> options) : base(options) { }

    public DbSet<Cotacoes> Cotacoes => Set<Cotacoes>();
    public DbSet<CestasRecomendacao> CestasRecomendacao => Set<CestasRecomendacao>();
    public DbSet<ItensCesta> ItensCesta => Set<ItensCesta>();
    public DbSet<Clientes> Clientes => Set<Clientes>();
    public DbSet<ClienteValorMensalHistorico> ClienteValorMensalHistorico => Set<ClienteValorMensalHistorico>();
    public DbSet<ContasGraficas> ContasGraficas => Set<ContasGraficas>();
    public DbSet<Custodias> Custodias => Set<Custodias>();
    public DbSet<Distribuicoes> Distribuicoes => Set<Distribuicoes>();
    public DbSet<OrdensCompra> OrdensCompra => Set<OrdensCompra>();

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

        modelBuilder.Entity<CestasRecomendacao>(e =>
        {
            e.ToTable("cestas_recomendacao");
            e.Property(x => x.Nome).HasMaxLength(100);
        });

        modelBuilder.Entity<ItensCesta>(e =>
        {
            e.ToTable("itens_cesta");
            e.Property(x => x.Ticker).HasMaxLength(10);
            e.Property(x => x.Percentual).HasPrecision(18, 6);

            e.HasOne(x => x.Cesta)
                .WithMany()
                .HasForeignKey(x => x.CestaId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Clientes>(e =>
        {
            e.ToTable("clientes");

            e.Property(x => x.Nome).HasMaxLength(200);

            e.Property(x => x.CPF)
                .HasMaxLength(11)
                .IsRequired();

            e.Property(x => x.Email).HasMaxLength(200);

            e.HasIndex(x => x.CPF)
                .IsUnique()
                .HasDatabaseName("ux_clientes_cpf");
        });

        modelBuilder.Entity<ClienteValorMensalHistorico>(e =>
        {
            e.ToTable("cliente_valor_mensal_historico");
            e.HasKey(x => x.Id);
            e.Property(x => x.ValorAnterior).HasMaxLength(32).IsRequired();
            e.Property(x => x.ValorNovo).HasMaxLength(32).IsRequired();
            e.Property(x => x.DataAlteracaoUtc).IsRequired();

            e.HasIndex(x => new { x.ClienteId, x.DataAlteracaoUtc })
                .HasDatabaseName("ix_hist_valor_cliente_data");
        });

        modelBuilder.Entity<ContasGraficas>(e =>
        {
            e.ToTable("contas_graficas");
            e.Property(x => x.NumeroConta).HasMaxLength(20);

            e.HasOne(x => x.Cliente)
                .WithMany()
                .HasForeignKey(x => x.ClienteId)
                .OnDelete(DeleteBehavior.Cascade);

            e.HasIndex(x => x.NumeroConta)
                .IsUnique()
                .HasDatabaseName("ux_numeroConta_contas");
        });

        modelBuilder.Entity<Custodias>(e =>
        {
            e.ToTable("custodias");
            e.Property(x => x.Ticker).HasMaxLength(10);
            e.Property(x => x.PrecoMedio).HasPrecision(18, 2);

            e.HasOne(x => x.ContasGraficas)
                .WithMany()
                .HasForeignKey(x => x.ContasGraficasId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Distribuicoes>(e =>
        {
            e.ToTable("distribuicoes");
            e.Property(x => x.Ticker).HasMaxLength(10);
            e.Property(x => x.Valor).HasPrecision(18, 2);
        });

        modelBuilder.Entity<OrdensCompra>(e =>
        {
            e.ToTable("ordens_compra");
            e.Property(x => x.Ticker).HasMaxLength(10);
            e.Property(x => x.PrecoUnitario).HasPrecision(18, 2);

            e.HasOne(x => x.ContaGrafica)
                .WithMany()
                .HasForeignKey(x => x.ContaMasterId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }
}
