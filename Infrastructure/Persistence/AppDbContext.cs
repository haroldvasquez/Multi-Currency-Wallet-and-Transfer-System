using Application.Interfaces;
using Domain.Models;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence;

public class AppDbContext : DbContext, IAppDbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options)
        : base(options)
    {
    }

    public virtual DbSet<Account> Accounts { get; set; }
    public virtual DbSet<Customer> Customers { get; set; }
    public virtual DbSet<Movement> Movements { get; set; }
    public virtual DbSet<Transfer> Transfers { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Account>(entity =>
        {
            entity.HasKey(e => e.AccountId).HasName("accounts_pkey");

            entity.ToTable("account");

            entity.HasIndex(e => e.AccountNumber, "account_account_number_key").IsUnique();

            entity.Property(e => e.AccountId)
                .ValueGeneratedNever()
                .HasColumnName("account_id");
            entity.Property(e => e.AccountNumber)
                .HasMaxLength(255)
                .HasColumnName("account_number");
            entity.Property(e => e.AccountStatus)
                .HasMaxLength(30)
                .HasColumnName("account_status");
            entity.Property(e => e.Balance)
                .HasPrecision(18, 2)
                .HasColumnName("balance");
            entity.Property(e => e.Currency)
                .HasMaxLength(3)
                .HasColumnName("currency");
            entity.Property(e => e.CustomerId).HasColumnName("customer_id");
            entity.Property(e => e.OpeningBalance)
                .HasPrecision(18, 2)
                .HasColumnName("opening_balance");
            entity.Property(e => e.Version)
                .HasDefaultValue(1L)
                .HasColumnName("version");

            entity.HasOne(d => d.Customer)
                .WithMany(p => p.Accounts)
                .HasForeignKey(d => d.CustomerId)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("fk_customer_id");
        });

        modelBuilder.Entity<Customer>(entity =>
        {
            entity.HasKey(e => e.CustomerId).HasName("customer_pk");

            entity.ToTable("customer");

            entity.HasIndex(e => e.DocumentId, "customer_document_id_key").IsUnique();

            entity.Property(e => e.CustomerId)
                .ValueGeneratedNever()
                .HasColumnName("customer_id");
            entity.Property(e => e.DocumentId)
                .HasMaxLength(255)
                .HasColumnName("document_id");
            entity.Property(e => e.Email)
                .HasMaxLength(255)
                .HasColumnName("email");
            entity.Property(e => e.Name)
                .HasMaxLength(255)
                .HasColumnName("name");
        });

        modelBuilder.Entity<Movement>(entity =>
        {
            entity.HasKey(e => e.MovementId).HasName("movement_pk");

            entity.ToTable("movement");

            entity.Property(e => e.MovementId)
                .ValueGeneratedNever()
                .HasColumnName("movement_id");
            entity.Property(e => e.AccountId).HasColumnName("account_id");
            entity.Property(e => e.Amount)
                .HasPrecision(18, 2)
                .HasColumnName("amount");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("created_at");
            entity.Property(e => e.MovementDescription).HasColumnName("movement_description");
            entity.Property(e => e.MovementType)
                .HasMaxLength(30)
                .HasColumnName("movement_type");
            entity.Property(e => e.NewBalance)
                .HasPrecision(18, 2)
                .HasColumnName("new_balance");
            entity.Property(e => e.PreviousBalance)
                .HasPrecision(18, 2)
                .HasColumnName("previous_balance");
            entity.Property(e => e.TransferId).HasColumnName("transfer_id");

            entity.HasOne(d => d.Account)
                .WithMany(p => p.Movements)
                .HasForeignKey(d => d.AccountId)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("fk_movement_account");

            entity.HasOne(d => d.Transfer)
                .WithMany(p => p.Movements)
                .HasForeignKey(d => d.TransferId)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("fk_movement_transfer");
        });

        modelBuilder.Entity<Transfer>(entity =>
        {
            entity.HasKey(e => e.TransferId).HasName("transfer_pk");

            entity.ToTable("transfer");

            entity.HasIndex(e => e.IdempotencyKey, "transfer_idempotency_key_key").IsUnique();

            entity.Property(e => e.TransferId)
                .ValueGeneratedNever()
                .HasColumnName("transfer_id");
            entity.Property(e => e.Amount)
                .HasPrecision(18, 2)
                .HasColumnName("amount");
            entity.Property(e => e.ConvertedAmount)
                .HasPrecision(18, 2)
                .HasColumnName("converted_amount");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("created_at");
            entity.Property(e => e.ExchangeRate)
                .HasPrecision(18, 8)
                .HasColumnName("exchange_rate");
            entity.Property(e => e.IdempotencyKey).HasColumnName("idempotency_key");
            entity.Property(e => e.SourceAccountId).HasColumnName("source_account_id");
            entity.Property(e => e.TargetAccountId).HasColumnName("target_account_id");
            entity.Property(e => e.TransferStatus)
                .HasMaxLength(30)
                .HasColumnName("transfer_status");

            entity.HasOne(d => d.SourceAccount)
                .WithMany(p => p.TransferSourceAccounts)
                .HasForeignKey(d => d.SourceAccountId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("fk_transfer_source_account");

            entity.HasOne(d => d.TargetAccount)
                .WithMany(p => p.TransferTargetAccounts)
                .HasForeignKey(d => d.TargetAccountId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("fk_transfer_target_account");
        });
    }
}
