using FinSyncNexus.Models;
using Microsoft.EntityFrameworkCore;

namespace FinSyncNexus.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<AppUser> Users => Set<AppUser>();
    public DbSet<ConnectionStatus> Connections => Set<ConnectionStatus>();
    public DbSet<InvoiceRecord> Invoices => Set<InvoiceRecord>();
    public DbSet<CustomerRecord> Customers => Set<CustomerRecord>();
    public DbSet<AccountRecord> Accounts => Set<AccountRecord>();
    public DbSet<PaymentRecord> Payments => Set<PaymentRecord>();
    public DbSet<ExpenseRecord> Expenses => Set<ExpenseRecord>();
    public DbSet<SyncErrorLog> SyncErrors => Set<SyncErrorLog>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<AppUser>(entity =>
        {
            entity.HasIndex(u => u.Email).IsUnique();
            entity.Property(u => u.Email).HasMaxLength(256);
            entity.Property(u => u.FullName).HasMaxLength(100);
        });

        modelBuilder.Entity<ConnectionStatus>()
            .HasIndex(c => new { c.UserId, c.Provider })
            .IsUnique();

        modelBuilder.Entity<InvoiceRecord>()
            .Property(i => i.Amount)
            .HasPrecision(18, 2);

        modelBuilder.Entity<InvoiceRecord>()
            .HasIndex(i => new { i.UserId, i.Provider, i.ExternalId })
            .IsUnique();

        modelBuilder.Entity<CustomerRecord>()
            .HasIndex(c => new { c.UserId, c.Provider, c.ExternalId })
            .IsUnique();

        modelBuilder.Entity<AccountRecord>()
            .HasIndex(a => new { a.UserId, a.Provider, a.ExternalId })
            .IsUnique();

        modelBuilder.Entity<PaymentRecord>()
            .Property(p => p.Amount)
            .HasPrecision(18, 2);

        modelBuilder.Entity<PaymentRecord>()
            .HasIndex(p => new { p.UserId, p.Provider, p.ExternalId })
            .IsUnique();

        modelBuilder.Entity<ExpenseRecord>()
            .Property(e => e.Amount)
            .HasPrecision(18, 2);

        modelBuilder.Entity<ExpenseRecord>()
            .HasIndex(e => new { e.UserId, e.Provider, e.ExternalId })
            .IsUnique();
    }
}
