using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace ScheduledPurchaseEngineService.Data;

public sealed class ScheduledPurchaseDbContextFactory : IDesignTimeDbContextFactory<ScheduledPurchaseDbContext>
{
    public ScheduledPurchaseDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<ScheduledPurchaseDbContext>();

        optionsBuilder.UseMySql(
            "Server=localhost;Port=3306;Database=investCycle;Uid=root;Pwd=12345678;SslMode=None;",
            new MySqlServerVersion(new Version(8, 0, 36)));

        return new ScheduledPurchaseDbContext(optionsBuilder.Options);
    }
}
