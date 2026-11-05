using Merkato.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Merkato.Infrastructure.Persistence;

public class MerkatoDbContext : DbContext
{
    public MerkatoDbContext(DbContextOptions<MerkatoDbContext> options) : base(options)
    {
    }

    public DbSet<User> Users => Set<User>();
    public DbSet<WishlistItem> WishlistItems => Set<WishlistItem>();
    public DbSet<PriceAlert> PriceAlerts => Set<PriceAlert>();
    public DbSet<UserNotification> UserNotifications => Set<UserNotification>();
    public DbSet<ProductListingDetail> ProductListingDetails => Set<ProductListingDetail>();
    public DbSet<ProductReview> ProductReviews => Set<ProductReview>();
    public DbSet<ProductPriceSnapshot> ProductPriceSnapshots => Set<ProductPriceSnapshot>();
    public DbSet<GroupBuy> GroupBuys => Set<GroupBuy>();
    public DbSet<GroupBuyMember> GroupBuyMembers => Set<GroupBuyMember>();
    public DbSet<ReturnRequest> ReturnRequests => Set<ReturnRequest>();
    public DbSet<LoyaltyAccount> LoyaltyAccounts => Set<LoyaltyAccount>();
    public DbSet<LoyaltyTransaction> LoyaltyTransactions => Set<LoyaltyTransaction>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<WholesalePriceTier> WholesalePriceTiers => Set<WholesalePriceTier>();
    public DbSet<WholesaleRequest> WholesaleRequests => Set<WholesaleRequest>();
    public DbSet<AffiliateProfile> AffiliateProfiles => Set<AffiliateProfile>();
    public DbSet<AffiliateReferral> AffiliateReferrals => Set<AffiliateReferral>();
    public DbSet<AffiliateCommission> AffiliateCommissions => Set<AffiliateCommission>();
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<PaymentTransaction> PaymentTransactions => Set<PaymentTransaction>();
    public DbSet<EscrowAccount> EscrowAccounts => Set<EscrowAccount>();
    public DbSet<LedgerTransaction> LedgerTransactions => Set<LedgerTransaction>();
    public DbSet<LedgerEntry> LedgerEntries => Set<LedgerEntry>();
    public DbSet<InspectionWindow> InspectionWindows => Set<InspectionWindow>();
    public DbSet<Dispute> Disputes => Set<Dispute>();
    public DbSet<Delivery> Deliveries => Set<Delivery>();
    public DbSet<OtpAttemptTracker> OtpAttemptTrackers => Set<OtpAttemptTracker>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<IdempotencyRecord> IdempotencyRecords => Set<IdempotencyRecord>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Buyer engagement: persisted, user-scoped records with unique active relationships.
        modelBuilder.Entity<WishlistItem>(e => { e.HasIndex(x => new { x.BuyerId, x.ProductId }).IsUnique(); e.HasIndex(x => x.BuyerId); });
        modelBuilder.Entity<PriceAlert>(e => { e.HasIndex(x => new { x.BuyerId, x.ProductId, x.IsActive }); e.Property(x => x.TargetPriceEtb).HasColumnType("numeric(18,4)"); });
        modelBuilder.Entity<UserNotification>(e => { e.HasIndex(x => new { x.UserId, x.IsRead, x.CreatedAt }); e.Property(x => x.Title).HasMaxLength(160).IsRequired(); e.Property(x => x.Message).HasMaxLength(1000).IsRequired(); e.Property(x => x.Type).HasMaxLength(40).IsRequired(); });

        // Extended commerce features
        modelBuilder.Entity<ProductListingDetail>(e => { e.HasKey(x => x.ProductId); e.Property(x => x.ProductId).ValueGeneratedNever(); e.HasIndex(x => x.StockQuantity); });
        modelBuilder.Entity<ProductReview>(e => { e.HasIndex(x => new { x.ProductId, x.BuyerId }).IsUnique(); e.HasIndex(x => x.OrderId); });
        modelBuilder.Entity<ProductPriceSnapshot>(e => { e.HasIndex(x => new { x.ProductId, x.RecordedAt }); e.Property(x => x.PriceEtb).HasColumnType("numeric(18,4)"); });
        modelBuilder.Entity<GroupBuy>(e => { e.HasIndex(x => new { x.Status, x.ExpiresAt }); e.Property(x => x.RegularUnitPriceEtb).HasColumnType("numeric(18,4)"); e.Property(x => x.DiscountUnitPriceEtb).HasColumnType("numeric(18,4)"); });
        modelBuilder.Entity<GroupBuyMember>(e => { e.HasIndex(x => new { x.GroupBuyId, x.BuyerId }).IsUnique(); });
        modelBuilder.Entity<ReturnRequest>(e => { e.HasIndex(x => x.OrderId).IsUnique(); });
        modelBuilder.Entity<LoyaltyAccount>(e => { e.HasKey(x => x.UserId); e.Property(x => x.UserId).ValueGeneratedNever(); });
        modelBuilder.Entity<LoyaltyTransaction>(e => { e.HasIndex(x => new { x.UserId, x.CreatedAt }); });

        // User
        modelBuilder.Entity<User>(entity =>
        {
            entity.HasIndex(u => u.PhoneNumber).IsUnique();
            entity.HasIndex(u => u.FaydaIdNumber).IsUnique();
        });

        modelBuilder.Entity<WholesalePriceTier>(e => {
            e.HasIndex(x => new { x.ProductId, x.MinimumQuantity }).IsUnique();
            e.Property(x => x.UnitPriceEtb).HasColumnType("numeric(18,4)");
        });
        modelBuilder.Entity<WholesaleRequest>(e => {
            e.HasIndex(x => x.RequestNumber).IsUnique();
            e.HasIndex(x => new { x.SellerId, x.Status });
            e.Property(x => x.RequestedUnitPriceEtb).HasColumnType("numeric(18,4)");
            e.Property(x => x.QuotedUnitPriceEtb).HasColumnType("numeric(18,4)");
        });
        modelBuilder.Entity<AffiliateProfile>(e => {
            e.HasIndex(x => x.UserId).IsUnique();
            e.HasIndex(x => x.ReferralCode).IsUnique();
        });
        modelBuilder.Entity<AffiliateReferral>(e => {
            e.HasIndex(x => new { x.AffiliateUserId, x.VisitorKeyHash }).IsUnique();
            e.HasIndex(x => x.ReferredUserId).IsUnique();
        });
        modelBuilder.Entity<AffiliateCommission>(e => {
            e.HasIndex(x => x.OrderId).IsUnique();
            e.Property(x => x.EligibleAmountEtb).HasColumnType("numeric(18,4)");
            e.Property(x => x.RatePercent).HasColumnType("numeric(7,4)");
            e.Property(x => x.CommissionEtb).HasColumnType("numeric(18,4)");
        });

        // Product
        modelBuilder.Entity<Product>(entity =>
        {
            entity.HasIndex(p => p.SellerId);
            entity.HasIndex(p => p.Category);
            entity.HasIndex(p => p.IsActive);

            entity.Property(p => p.Name)
                .HasMaxLength(200)
                .IsRequired();

            entity.Property(p => p.PriceEtb)
                .HasColumnType("numeric(18,4)");

            entity.Property(p => p.Location)
                .HasMaxLength(150)
                .IsRequired();

            entity.Property(p => p.Category)
                .HasMaxLength(100)
                .IsRequired();

            entity.Property(p => p.Emoji)
                .HasMaxLength(20)
                .IsRequired();
        });

        // Order
        modelBuilder.Entity<Order>(entity =>
        {
            entity.HasIndex(o => o.OrderNumber).IsUnique();
            entity.HasIndex(o => o.BuyerId);
            entity.HasIndex(o => o.SellerId);

            entity.Property(o => o.TotalAmountEtb)
                .HasColumnType("numeric(18,4)");

            entity.Property(o => o.DeliveryFeeEtb)
                .HasColumnType("numeric(18,4)");

            entity.Property(o => o.RowVersion)
                .IsConcurrencyToken();
        });

        // PaymentTransaction
        modelBuilder.Entity<PaymentTransaction>(entity =>
        {
            entity.HasIndex(p => p.IdempotencyKey).IsUnique();
            entity.HasIndex(p => p.OrderId);

            entity.Property(p => p.AmountEtb)
                .HasColumnType("numeric(18,4)");
        });

        // EscrowAccount
        modelBuilder.Entity<EscrowAccount>(entity =>
        {
            entity.HasIndex(e => e.OrderId).IsUnique();

            entity.Property(e => e.HeldAmountEtb)
                .HasColumnType("numeric(18,4)");

            entity.Property(e => e.DisputedAmountEtb)
                .HasColumnType("numeric(18,4)");

            entity.Property(e => e.ReleasedAmountEtb)
                .HasColumnType("numeric(18,4)");

            entity.Property(e => e.RefundedAmountEtb)
                .HasColumnType("numeric(18,4)");

            entity.Property(e => e.RowVersion)
                .IsConcurrencyToken();
        });

        // Ledger
        modelBuilder.Entity<LedgerTransaction>(entity =>
        {
            entity.HasIndex(lt => lt.OrderId);

            entity.HasMany(lt => lt.Entries)
                .WithOne()
                .HasForeignKey(le => le.LedgerTransactionId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<LedgerEntry>(entity =>
        {
            entity.Property(le => le.AmountEtb)
                .HasColumnType("numeric(18,4)");
        });

        // Inspection
        modelBuilder.Entity<InspectionWindow>(entity =>
        {
            entity.HasIndex(iw => iw.OrderId).IsUnique();
        });

        // Dispute
        modelBuilder.Entity<Dispute>(entity =>
        {
            entity.HasIndex(d => d.OrderId).IsUnique();
        });

        // Delivery
        modelBuilder.Entity<Delivery>(entity =>
        {
            entity.HasIndex(d => d.DeliveryId).IsUnique();
            entity.HasIndex(d => d.OrderId).IsUnique();

            entity.Property(d => d.FeeEtb)
                .HasColumnType("numeric(18,4)");

            entity.Property(d => d.RowVersion)
                .IsConcurrencyToken();
        });

        // OTP
        modelBuilder.Entity<OtpAttemptTracker>(entity =>
        {
            entity.HasIndex(o => o.DeliveryId).IsUnique();
        });

        // Audit
        modelBuilder.Entity<AuditLog>(entity =>
        {
            entity.HasIndex(a => new { a.EntityType, a.EntityId });
            entity.HasIndex(a => a.CreatedAt);
        });

        // Idempotency
        modelBuilder.Entity<IdempotencyRecord>(entity =>
        {
            entity.HasIndex(i => i.Key).IsUnique();
        });
    }
}