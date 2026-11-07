using Merkato.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Merkato.Infrastructure.Persistence;

public class MerkatoDbContext : DbContext
{
    public MerkatoDbContext(DbContextOptions<MerkatoDbContext> options) : base(options)
    {
    }

    public DbSet<User> Users => Set<User>();
    public DbSet<UserAddress> UserAddresses => Set<UserAddress>();
    public DbSet<WishlistItem> WishlistItems => Set<WishlistItem>();
    public DbSet<PriceAlert> PriceAlerts => Set<PriceAlert>();
    public DbSet<UserNotification> UserNotifications => Set<UserNotification>();
    public DbSet<UserNotificationPreference> UserNotificationPreferences => Set<UserNotificationPreference>();
    public DbSet<PromotionCoupon> PromotionCoupons => Set<PromotionCoupon>();
    public DbSet<CouponRedemption> CouponRedemptions => Set<CouponRedemption>();
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
    public DbSet<SourcingRequest> SourcingRequests => Set<SourcingRequest>();
    public DbSet<SourcingOffer> SourcingOffers => Set<SourcingOffer>();
    public DbSet<SocialCommercePost> SocialCommercePosts => Set<SocialCommercePost>();
    public DbSet<SocialCommerceClick> SocialCommerceClicks => Set<SocialCommerceClick>();
    public DbSet<CreatorCommission> CreatorCommissions => Set<CreatorCommission>();
    public DbSet<AffiliateProfile> AffiliateProfiles => Set<AffiliateProfile>();
    public DbSet<AffiliateReferral> AffiliateReferrals => Set<AffiliateReferral>();
    public DbSet<AffiliateCommission> AffiliateCommissions => Set<AffiliateCommission>();
    public DbSet<MarketplaceFeeSettings> MarketplaceFeeSettings => Set<MarketplaceFeeSettings>();
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<PaymentTransaction> PaymentTransactions => Set<PaymentTransaction>();
    public DbSet<PaymentReceipt> PaymentReceipts => Set<PaymentReceipt>();
    public DbSet<SellerPaymentAccount> SellerPaymentAccounts => Set<SellerPaymentAccount>();
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

        // Account addresses are scoped to the owning user.
modelBuilder.Entity<UserAddress>(e =>
{
    e.HasIndex(x => x.UserId).IsUnique().HasFilter("\"IsDefault\" = TRUE");
    e.Property(x => x.Label).HasMaxLength(40).IsRequired();
    e.Property(x => x.RecipientName).HasMaxLength(120).IsRequired();
    e.Property(x => x.PhoneNumber).HasMaxLength(20).IsRequired();
    e.Property(x => x.City).HasMaxLength(100).IsRequired();
    e.Property(x => x.SubCity).HasMaxLength(100).IsRequired();
    e.Property(x => x.Woreda).HasMaxLength(100);
    e.Property(x => x.StreetAddress).HasMaxLength(300);
    e.Property(x => x.DeliveryInstructions).HasMaxLength(500);
    e.HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
});
// Buyer engagement: persisted, user-scoped records with unique active relationships.
        modelBuilder.Entity<WishlistItem>(e => { e.HasIndex(x => new { x.BuyerId, x.ProductId }).IsUnique(); e.HasIndex(x => x.BuyerId); e.HasOne<User>().WithMany().HasForeignKey(x => x.BuyerId).OnDelete(DeleteBehavior.Cascade); e.HasOne<Product>().WithMany().HasForeignKey(x => x.ProductId).OnDelete(DeleteBehavior.Cascade); });
        modelBuilder.Entity<PriceAlert>(e => { e.HasIndex(x => new { x.BuyerId, x.ProductId, x.IsActive }); e.Property(x => x.TargetPriceEtb).HasColumnType("numeric(18,4)"); e.HasOne<User>().WithMany().HasForeignKey(x => x.BuyerId).OnDelete(DeleteBehavior.Cascade); e.HasOne<Product>().WithMany().HasForeignKey(x => x.ProductId).OnDelete(DeleteBehavior.Cascade); });
        modelBuilder.Entity<UserNotification>(e => { e.HasIndex(x => new { x.UserId, x.IsRead, x.CreatedAt }); e.Property(x => x.Title).HasMaxLength(160).IsRequired(); e.Property(x => x.Message).HasMaxLength(1000).IsRequired(); e.Property(x => x.Type).HasMaxLength(40).IsRequired(); e.HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade); });

       modelBuilder.Entity<UserNotificationPreference>(e =>
        {
            e.HasKey(x => x.UserId);
            e.Property(x => x.UserId).ValueGeneratedNever();
            e.Property(x => x.LanguageCode).HasMaxLength(5).IsRequired();
            e.HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        });
        // Coupon inventory and redemption constraints.
        modelBuilder.Entity<PromotionCoupon>(e => {
            e.HasIndex(x => x.Code).IsUnique(); e.HasIndex(x => x.SourceReferralId).IsUnique();
            e.Property(x => x.Code).HasMaxLength(40).IsRequired();
            e.Property(x => x.Title).HasMaxLength(120).IsRequired();
            e.Property(x => x.DiscountType).HasMaxLength(12).IsRequired();
            e.Property(x => x.DiscountValue).HasColumnType("numeric(18,4)");
            e.Property(x => x.MaximumDiscountEtb).HasColumnType("numeric(18,4)");
            e.Property(x => x.MinimumOrderEtb).HasColumnType("numeric(18,4)");
            e.HasIndex(x => new { x.IsActive, x.StartsAt, x.ExpiresAt });
        });
        modelBuilder.Entity<CouponRedemption>(e => {
            e.HasIndex(x => new { x.CouponId, x.BuyerId });
            e.HasIndex(x => x.OrderId).IsUnique();
            e.Property(x => x.DiscountEtb).HasColumnType("numeric(18,4)");
            e.HasOne<PromotionCoupon>().WithMany().HasForeignKey(x => x.CouponId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<User>().WithMany().HasForeignKey(x => x.BuyerId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<Order>().WithMany().HasForeignKey(x => x.OrderId).OnDelete(DeleteBehavior.Restrict);
        });

        // Extended commerce features
        modelBuilder.Entity<ProductListingDetail>(e => { e.HasKey(x => x.ProductId); e.Property(x => x.ProductId).ValueGeneratedNever(); e.HasIndex(x => x.StockQuantity); });
        modelBuilder.Entity<ProductReview>(e => { e.HasIndex(x => new { x.ProductId, x.BuyerId }).IsUnique(); e.HasIndex(x => x.OrderId); });
        modelBuilder.Entity<ProductPriceSnapshot>(e => { e.HasIndex(x => new { x.ProductId, x.RecordedAt }); e.Property(x => x.PriceEtb).HasColumnType("numeric(18,4)"); });
        modelBuilder.Entity<GroupBuy>(e => { e.HasIndex(x => new { x.Status, x.ExpiresAt }); e.Property(x => x.RegularUnitPriceEtb).HasColumnType("numeric(18,4)"); e.Property(x => x.DiscountUnitPriceEtb).HasColumnType("numeric(18,4)"); });
        modelBuilder.Entity<GroupBuyMember>(e => { e.HasIndex(x => new { x.GroupBuyId, x.BuyerId }).IsUnique(); });
        modelBuilder.Entity<ReturnRequest>(e => { e.HasIndex(x => x.OrderId).IsUnique(); });
        modelBuilder.Entity<LoyaltyAccount>(e => { e.HasKey(x => x.UserId); e.Property(x => x.UserId).ValueGeneratedNever(); });
        modelBuilder.Entity<LoyaltyTransaction>(e => { e.HasIndex(x => new { x.UserId, x.CreatedAt }); });

        modelBuilder.Entity<MarketplaceFeeSettings>(e => {
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).ValueGeneratedNever();
            e.Property(x => x.MerchantCommissionRatePercent).HasColumnType("numeric(7,4)");
            e.Property(x => x.BuyerServiceFeeRatePercent).HasColumnType("numeric(7,4)");
        });

        // User
        modelBuilder.Entity<User>(entity =>
        {
            entity.HasIndex(u => u.PhoneNumber).IsUnique();
            entity.HasIndex(u => u.Email).IsUnique().HasFilter("\"Email\" IS NOT NULL");
            entity.Property(u => u.Email).HasMaxLength(320);
            entity.Property(u => u.ProfilePhotoContentType).HasMaxLength(80);
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
        modelBuilder.Entity<SourcingRequest>(e => { e.HasIndex(x => x.RequestNumber).IsUnique(); e.HasIndex(x => new { x.BuyerId, x.CreatedAt }); e.HasIndex(x => new { x.Status, x.ExpiresAt }); e.Property(x => x.RequestNumber).HasMaxLength(24).IsRequired(); e.Property(x => x.Title).HasMaxLength(160).IsRequired(); e.Property(x => x.Description).HasMaxLength(2000).IsRequired(); e.Property(x => x.Category).HasMaxLength(100).IsRequired(); e.Property(x => x.City).HasMaxLength(100).IsRequired(); e.Property(x => x.Status).HasMaxLength(24).IsRequired(); e.Property(x => x.TargetUnitPriceEtb).HasColumnType("numeric(18,4)"); });
        modelBuilder.Entity<SourcingOffer>(e => { e.HasIndex(x => x.ProductId); e.HasIndex(x => new { x.SourcingRequestId, x.SellerId }).IsUnique(); e.HasIndex(x => new { x.SellerId, x.Status }); e.Property(x => x.UnitPriceEtb).HasColumnType("numeric(18,4)"); e.Property(x => x.Note).HasMaxLength(1000); e.Property(x => x.Status).HasMaxLength(24).IsRequired(); });
        modelBuilder.Entity<SocialCommercePost>(e => { e.HasIndex(x => new { x.Status, x.PublishedAt }); e.HasIndex(x => new { x.CreatorId, x.CreatedAt }); e.Property(x => x.Caption).HasMaxLength(1200).IsRequired(); e.Property(x => x.MediaUrl).HasMaxLength(2048).IsRequired(); e.Property(x => x.MediaType).HasMaxLength(8).IsRequired(); e.Property(x => x.Status).HasMaxLength(16).IsRequired(); e.Property(x => x.ModerationNote).HasMaxLength(1000); });
        modelBuilder.Entity<SocialCommerceClick>(e => { e.HasIndex(x => new { x.PostId, x.VisitorHash }).IsUnique(); e.Property(x => x.VisitorHash).HasMaxLength(64).IsRequired(); });
        modelBuilder.Entity<CreatorCommission>(e => { e.HasIndex(x => x.OrderId).IsUnique(); e.HasIndex(x => new { x.CreatorId, x.CreatedAt }); e.Property(x => x.EligibleAmountEtb).HasColumnType("numeric(18,4)"); e.Property(x => x.RatePercent).HasColumnType("numeric(7,4)"); e.Property(x => x.CommissionEtb).HasColumnType("numeric(18,4)"); e.Property(x => x.Status).HasMaxLength(32).IsRequired(); e.Property(x => x.PayoutReference).HasMaxLength(120); });
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
            entity.HasIndex(o => o.ProductId);
            entity.HasIndex(o => o.SourcingRequestId).IsUnique();
            entity.HasIndex(o => new { o.InventoryReservationState, o.InventoryReservationExpiresAt });

            entity.Property(o => o.TotalAmountEtb)
                .HasColumnType("numeric(18,4)");

            entity.Property(o => o.DeliveryFeeEtb).HasColumnType("numeric(18,4)");
            entity.Property(o => o.ProductSubtotalEtb).HasColumnType("numeric(18,4)");
            entity.Property(o => o.DiscountEtb).HasColumnType("numeric(18,4)");
            entity.Property(o => o.MerchantCommissionRatePercent).HasColumnType("numeric(7,4)");
            entity.Property(o => o.MerchantCommissionEtb).HasColumnType("numeric(18,4)");
            entity.Property(o => o.BuyerServiceFeeRatePercent).HasColumnType("numeric(7,4)");
            entity.Property(o => o.BuyerServiceFeeEtb).HasColumnType("numeric(18,4)");

            entity.Property(o => o.PaymentMode).HasMaxLength(24).IsRequired();

            entity.Property(o => o.RowVersion)
                .IsConcurrencyToken();
        });

        // PaymentTransaction
        modelBuilder.Entity<PaymentTransaction>(entity =>
        {
            entity.HasIndex(p => p.IdempotencyKey).IsUnique();
            entity.HasIndex(p => p.OrderId);
            entity.HasIndex(p => p.RailTransactionId).IsUnique()
                .HasFilter("\"RailTransactionId\" IS NOT NULL");

            entity.Property(p => p.AmountEtb)
                .HasColumnType("numeric(18,4)");
        });

        // Seller-owned payout destinations; each seller can maintain one active destination per rail.
        modelBuilder.Entity<SellerPaymentAccount>(entity =>
        {
            entity.HasIndex(x => new { x.SellerId, x.PaymentRail }).IsUnique();
            entity.Property(x => x.AccountHolderName).HasMaxLength(120).IsRequired();
            entity.Property(x => x.AccountIdentifier).HasMaxLength(120).IsRequired();
            entity.Property(x => x.BankName).HasMaxLength(120);
            entity.Property(x => x.Instructions).HasMaxLength(500);
            entity.HasOne<User>().WithMany().HasForeignKey(x => x.SellerId).OnDelete(DeleteBehavior.Restrict);
        });

        // Private payment evidence; transaction references are globally unique when supplied.
        modelBuilder.Entity<PaymentReceipt>(entity =>
        {
            entity.HasIndex(x => x.OrderId);
            entity.HasIndex(x => x.Sha256).IsUnique();
            entity.HasIndex(x => new { x.PaymentTransactionId, x.CreatedAt });
            entity.HasIndex(x => new { x.Status, x.CreatedAt });
            entity.HasIndex(x => x.TransactionReference)
                .IsUnique()
                .HasFilter("\"TransactionReference\" IS NOT NULL");
            entity.Property(x => x.FileName).HasMaxLength(255).IsRequired();
            entity.Property(x => x.ContentType).HasMaxLength(100).IsRequired();
            entity.Property(x => x.Sha256).HasMaxLength(64).IsRequired();
            entity.Property(x => x.TransactionReference).HasMaxLength(150);
            entity.Property(x => x.Status).HasMaxLength(30).IsRequired();
            entity.HasOne<PaymentTransaction>().WithMany().HasForeignKey(x => x.PaymentTransactionId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<Order>().WithMany().HasForeignKey(x => x.OrderId).OnDelete(DeleteBehavior.Restrict);
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