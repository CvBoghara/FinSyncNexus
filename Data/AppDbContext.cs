using FinSyncNexus.Models;
using Microsoft.EntityFrameworkCore;

namespace FinSyncNexus.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<ConnectionStatus> Connections => Set<ConnectionStatus>();
    public DbSet<InvoiceRecord> Invoices => Set<InvoiceRecord>();
    public DbSet<CustomerRecord> Customers => Set<CustomerRecord>();
    public DbSet<AccountRecord> Accounts => Set<AccountRecord>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<ConnectionStatus>()
            .HasIndex(c => c.Provider)
            .IsUnique();

        modelBuilder.Entity<InvoiceRecord>()
            .Property(i => i.Amount)
            .HasPrecision(18, 2);

        modelBuilder.Entity<InvoiceRecord>()
            .HasIndex(i => new { i.Provider, i.ExternalId })
            .IsUnique();

        modelBuilder.Entity<CustomerRecord>()
            .HasIndex(c => new { c.Provider, c.ExternalId })
            .IsUnique();

        modelBuilder.Entity<AccountRecord>()
            .HasIndex(a => new { a.Provider, a.ExternalId })
            .IsUnique();
    }
}
