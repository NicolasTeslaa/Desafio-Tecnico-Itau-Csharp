using ClassLibrary.Domain.Entities;
using ClassLibrary.Domain.Entities.Cestas;
using ClassLibrary.Domain.Entities.Clientes;
using ClassLibrary.Domain.Entities.CompraDistribuicao;
using ClassLibrary.Domain.Entities.RebalanceamentoIR;
using Itau.InvestCycleEngine.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace MarketDataIngestionService.Data;

public sealed class MarketDataDbContext : DbContext
{
    public MarketDataDbContext(DbContextOptions<MarketDataDbContext> options) : base(options) { }

    public DbSet<Asset> Assets => Set<Asset>();
    public DbSet<InvestmentAccount> InvestmentAccounts => Set<InvestmentAccount>();
    public DbSet<PlanExecution> PlanExecutions => Set<PlanExecution>();
    public DbSet<ProgrammedPurchasePlan> ProgrammedPurchasePlans => Set<ProgrammedPurchasePlan>();
    public DbSet<Cotacoes> Cotacoes => Set<Cotacoes>();
    public DbSet<CestasRecomendacao> CestasRecomendacao => Set<CestasRecomendacao>();
    public DbSet<ItensCesta> ItensCesta => Set<ItensCesta>();
    public DbSet<Clientes> Clientes => Set<Clientes>();
    public DbSet<ContasGraficas> ContasGraficas => Set<ContasGraficas>();
    public DbSet<Custodias> Custodias => Set<Custodias>();
    public DbSet<Distribuicoes> Distribuicoes => Set<Distribuicoes>();
    public DbSet<OrdensCompra> OrdensCompra => Set<OrdensCompra>();
    public DbSet<EventosIR> EventosIR => Set<EventosIR>();
    public DbSet<Rebalanceamentos> Rebalanceamentos => Set<Rebalanceamentos>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Asset>(e =>
        {
            e.ToTable("assets");
            e.HasKey(x => x.Id);
        });

        modelBuilder.Entity<InvestmentAccount>(e =>
        {
            e.ToTable("investment_accounts");
            e.HasKey(x => x.Id);
        });

        modelBuilder.Entity<PlanExecution>(e =>
        {
            e.ToTable("plan_executions");
            e.HasKey(x => x.Id);
        });

        modelBuilder.Entity<ProgrammedPurchasePlan>(e =>
        {
            e.ToTable("programmed_purchase_plans");
            e.HasKey(x => x.Id);

            e.ComplexProperty(x => x.AmountPerRun, money =>
            {
                money.Property(p => p.Amount)
                    .HasColumnName("amount_per_run")
                    .HasPrecision(18, 2);
                money.Property(p => p.Currency)
                    .HasColumnName("amount_currency");
            });

            e.OwnsOne(x => x.Schedule, schedule =>
            {
                schedule.Property(p => p.Frequency).HasColumnName("schedule_frequency");
                schedule.Property(p => p.Interval).HasColumnName("schedule_interval");
                schedule.Property(p => p.DayOfWeek).HasColumnName("schedule_day_of_week");
                schedule.Property(p => p.DayOfMonth).HasColumnName("schedule_day_of_month");
                schedule.Property(p => p.RunAtLocalTime).HasColumnName("schedule_run_at_local_time");
            });
        });

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

        modelBuilder.Entity<EventosIR>(e =>
        {
            e.ToTable("eventos_ir");
            e.Property(x => x.ValorBase).HasPrecision(18, 2);
            e.Property(x => x.ValorIR).HasPrecision(18, 2);

            e.HasOne(x => x.Cliente)
                .WithMany()
                .HasForeignKey(x => x.ClienteId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Rebalanceamentos>(e =>
        {
            e.ToTable("rebalanceamentos");
            e.Property(x => x.TickerVendido).HasMaxLength(10);
            e.Property(x => x.TickerComprado).HasMaxLength(10);
            e.Property(x => x.ValorVenda).HasPrecision(18, 2);

            e.HasOne(x => x.Cliente)
                .WithMany()
                .HasForeignKey(x => x.ClienteId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
