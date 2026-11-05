using Merkato.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Merkato.Infrastructure.Migrations;

[DbContext(typeof(MerkatoDbContext))]
[Migration("20261105210000_HardenUserAuthentication")]
public sealed class HardenUserAuthentication : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<int>(
            name: "FailedLoginAttempts",
            table: "Users",
            type: "integer",
            nullable: false,
            defaultValue: 0);

        migrationBuilder.AddColumn<DateTime>(
            name: "LoginLockedUntil",
            table: "Users",
            type: "timestamp with time zone",
            nullable: true);

        migrationBuilder.AddColumn<bool>(
            name: "IsSuspended",
            table: "Users",
            type: "boolean",
            nullable: false,
            defaultValue: false);

        migrationBuilder.AddColumn<int>(
            name: "TokenVersion",
            table: "Users",
            type: "integer",
            nullable: false,
            defaultValue: 0);

        migrationBuilder.Sql("""
            DO $$
            BEGIN
                IF EXISTS (
                    WITH normalized AS (
                        SELECT CASE
                            WHEN regexp_replace("PhoneNumber", '[\s()-]', '', 'g') ~ '^0[97][0-9]{8}$'
                                THEN '+251' || substr(regexp_replace("PhoneNumber", '[\s()-]', '', 'g'), 2)
                            WHEN regexp_replace("PhoneNumber", '[\s()-]', '', 'g') ~ '^251[97][0-9]{8}$'
                                THEN '+' || regexp_replace("PhoneNumber", '[\s()-]', '', 'g')
                            ELSE regexp_replace("PhoneNumber", '[\s()-]', '', 'g')
                        END AS canonical_phone
                        FROM "Users"
                    )
                    SELECT canonical_phone
                    FROM normalized
                    GROUP BY canonical_phone
                    HAVING COUNT(*) > 1
                ) THEN
                    RAISE EXCEPTION 'Phone normalization would create duplicate user phone numbers. Resolve duplicate accounts before applying this migration.';
                END IF;
            END $$;

            UPDATE "Users"
            SET "PhoneNumber" = CASE
                WHEN regexp_replace("PhoneNumber", '[\s()-]', '', 'g') ~ '^0[97][0-9]{8}$'
                    THEN '+251' || substr(regexp_replace("PhoneNumber", '[\s()-]', '', 'g'), 2)
                WHEN regexp_replace("PhoneNumber", '[\s()-]', '', 'g') ~ '^251[97][0-9]{8}$'
                    THEN '+' || regexp_replace("PhoneNumber", '[\s()-]', '', 'g')
                ELSE regexp_replace("PhoneNumber", '[\s()-]', '', 'g')
            END;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(name: "FailedLoginAttempts", table: "Users");
        migrationBuilder.DropColumn(name: "LoginLockedUntil", table: "Users");
        migrationBuilder.DropColumn(name: "IsSuspended", table: "Users");
        migrationBuilder.DropColumn(name: "TokenVersion", table: "Users");
    }
}
