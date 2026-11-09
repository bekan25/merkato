using Merkato.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Merkato.Infrastructure.Migrations;

[DbContext(typeof(MerkatoDbContext))]
[Migration("20261109170000_AddMultiProviderDeliveryCore")]
public sealed class AddMultiProviderDeliveryCore : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "DeliveryProviders",
            columns: table => new
            {
                Id = table.Column<long>(type: "bigint", nullable: false).Annotation("Npgsql:ValueGenerationStrategy", Npgsql.EntityFrameworkCore.PostgreSQL.Metadata.NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                Code = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                DisplayName = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                ProviderKind = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                IsEnabled = table.Column<bool>(type: "boolean", nullable: false),
                SupportsQuotes = table.Column<bool>(type: "boolean", nullable: false),
                SupportsBooking = table.Column<bool>(type: "boolean", nullable: false),
                SupportsTracking = table.Column<bool>(type: "boolean", nullable: false),
                CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table => table.PrimaryKey("PK_DeliveryProviders", x => x.Id));

        migrationBuilder.CreateTable(
            name: "DeliveryPartnerProfiles",
            columns: table => new
            {
                Id = table.Column<long>(type: "bigint", nullable: false).Annotation("Npgsql:ValueGenerationStrategy", Npgsql.EntityFrameworkCore.PostgreSQL.Metadata.NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                UserId = table.Column<long>(type: "bigint", nullable: false),
                ApprovalStatus = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                ServiceAreasJson = table.Column<string>(type: "jsonb", nullable: false),
                TransportMode = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                VehicleDescription = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                CapacityKg = table.Column<decimal>(type: "numeric(10,2)", nullable: true),
                ReviewerUserId = table.Column<long>(type: "bigint", nullable: true),
                ReviewNote = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                AppliedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                ReviewedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_DeliveryPartnerProfiles", x => x.Id);
                table.ForeignKey("FK_DeliveryPartnerProfiles_Users_UserId", x => x.UserId, "Users", "Id", onDelete: ReferentialAction.Cascade);
                table.ForeignKey("FK_DeliveryPartnerProfiles_Users_ReviewerUserId", x => x.ReviewerUserId, "Users", "Id", onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "DeliveryAssignments",
            columns: table => new
            {
                Id = table.Column<long>(type: "bigint", nullable: false).Annotation("Npgsql:ValueGenerationStrategy", Npgsql.EntityFrameworkCore.PostgreSQL.Metadata.NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                DeliveryId = table.Column<long>(type: "bigint", nullable: false),
                ProviderId = table.Column<long>(type: "bigint", nullable: false),
                PartnerUserId = table.Column<long>(type: "bigint", nullable: true),
                AssignedByUserId = table.Column<long>(type: "bigint", nullable: false),
                Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                AgreedFeeEtb = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                Currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                ExternalBookingReference = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                IdempotencyKey = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                AssignedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                AcceptedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                CompletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_DeliveryAssignments", x => x.Id);
                table.ForeignKey("FK_DeliveryAssignments_Deliveries_DeliveryId", x => x.DeliveryId, "Deliveries", "Id", onDelete: ReferentialAction.Cascade);
                table.ForeignKey("FK_DeliveryAssignments_DeliveryProviders_ProviderId", x => x.ProviderId, "DeliveryProviders", "Id", onDelete: ReferentialAction.Restrict);
                table.ForeignKey("FK_DeliveryAssignments_Users_PartnerUserId", x => x.PartnerUserId, "Users", "Id", onDelete: ReferentialAction.Restrict);
                table.ForeignKey("FK_DeliveryAssignments_Users_AssignedByUserId", x => x.AssignedByUserId, "Users", "Id", onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "DeliveryTrackingEvents",
            columns: table => new
            {
                Id = table.Column<long>(type: "bigint", nullable: false).Annotation("Npgsql:ValueGenerationStrategy", Npgsql.EntityFrameworkCore.PostgreSQL.Metadata.NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                DeliveryId = table.Column<long>(type: "bigint", nullable: false),
                AssignmentId = table.Column<long>(type: "bigint", nullable: true),
                Source = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                EventKey = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                Status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                OccurredAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                ReceivedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_DeliveryTrackingEvents", x => x.Id);
                table.ForeignKey("FK_DeliveryTrackingEvents_Deliveries_DeliveryId", x => x.DeliveryId, "Deliveries", "Id", onDelete: ReferentialAction.Cascade);
                table.ForeignKey("FK_DeliveryTrackingEvents_DeliveryAssignments_AssignmentId", x => x.AssignmentId, "DeliveryAssignments", "Id", onDelete: ReferentialAction.SetNull);
            });

        migrationBuilder.CreateIndex("IX_DeliveryProviders_Code", "DeliveryProviders", "Code", unique: true);
        migrationBuilder.CreateIndex("IX_DeliveryPartnerProfiles_UserId", "DeliveryPartnerProfiles", "UserId", unique: true);
        migrationBuilder.CreateIndex("IX_DeliveryPartnerProfiles_ApprovalStatus", "DeliveryPartnerProfiles", "ApprovalStatus");
        migrationBuilder.CreateIndex("IX_DeliveryPartnerProfiles_ReviewerUserId", "DeliveryPartnerProfiles", "ReviewerUserId");
        migrationBuilder.CreateIndex("IX_DeliveryAssignments_DeliveryId_Status", "DeliveryAssignments", new[] { "DeliveryId", "Status" });
        migrationBuilder.CreateIndex("IX_DeliveryAssignments_OneActivePerDelivery", "DeliveryAssignments", "DeliveryId", unique: true, filter: "\"Status\" IN ('OFFERED','ACCEPTED')");
        migrationBuilder.CreateIndex("IX_DeliveryAssignments_ProviderId", "DeliveryAssignments", "ProviderId");
        migrationBuilder.CreateIndex("IX_DeliveryAssignments_PartnerUserId_Status", "DeliveryAssignments", new[] { "PartnerUserId", "Status" });
        migrationBuilder.CreateIndex("IX_DeliveryAssignments_AssignedByUserId", "DeliveryAssignments", "AssignedByUserId");
        migrationBuilder.CreateIndex("IX_DeliveryAssignments_IdempotencyKey", "DeliveryAssignments", "IdempotencyKey", unique: true);
        migrationBuilder.CreateIndex("IX_DeliveryTrackingEvents_DeliveryId_OccurredAt", "DeliveryTrackingEvents", new[] { "DeliveryId", "OccurredAt" });
        migrationBuilder.CreateIndex("IX_DeliveryTrackingEvents_AssignmentId", "DeliveryTrackingEvents", "AssignmentId");
        migrationBuilder.CreateIndex("IX_DeliveryTrackingEvents_Source_EventKey", "DeliveryTrackingEvents", new[] { "Source", "EventKey" }, unique: true);

        migrationBuilder.Sql("""
            INSERT INTO "DeliveryProviders"
                ("Code", "DisplayName", "ProviderKind", "IsEnabled", "SupportsQuotes", "SupportsBooking", "SupportsTracking", "CreatedAt")
            VALUES
                ('SELLER', 'Seller''s own delivery', 'SELLER', TRUE, FALSE, FALSE, FALSE, CURRENT_TIMESTAMP),
                ('MERKATO_INDIVIDUAL', 'MERKATO individual courier', 'INDIVIDUAL', TRUE, FALSE, FALSE, FALSE, CURRENT_TIMESTAMP),
                ('KLIK', 'Klik Delivery', 'COMPANY', FALSE, FALSE, FALSE, FALSE, CURRENT_TIMESTAMP),
                ('OTHER_COURIER', 'Other courier company', 'COMPANY', FALSE, FALSE, FALSE, FALSE, CURRENT_TIMESTAMP);
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable("DeliveryTrackingEvents");
        migrationBuilder.DropTable("DeliveryAssignments");
        migrationBuilder.DropTable("DeliveryPartnerProfiles");
        migrationBuilder.DropTable("DeliveryProviders");
    }
}
