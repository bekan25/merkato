using System.Xml.Linq;
using Microsoft.AspNetCore.DataProtection.Repositories;
using Npgsql;

namespace Merkato.Infrastructure.Persistence;

/// <summary>
/// Stores the ASP.NET Core Data Protection XML key ring in PostgreSQL.
/// The caller configures certificate encryption before keys are persisted.
/// </summary>
public sealed class PostgresXmlRepository : IXmlRepository
{
    private readonly string _connectionString;

    public PostgresXmlRepository(string connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new ArgumentException("A PostgreSQL connection string is required.", nameof(connectionString));

        _connectionString = connectionString;
    }

    public IReadOnlyCollection<XElement> GetAllElements()
    {
        var elements = new List<XElement>();
        using var connection = new NpgsqlConnection(_connectionString);
        connection.Open();

        using var command = new NpgsqlCommand("""SELECT "Xml" FROM "DataProtectionKeys" ORDER BY "FriendlyName";""", connection);
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            var xml = reader.GetString(0);
            elements.Add(XElement.Parse(xml, LoadOptions.None));
        }

        return elements;
    }

    public void StoreElement(XElement element, string friendlyName)
    {
        ArgumentNullException.ThrowIfNull(element);
        if (string.IsNullOrWhiteSpace(friendlyName))
            throw new ArgumentException("A key friendly name is required.", nameof(friendlyName));

        using var connection = new NpgsqlConnection(_connectionString);
        connection.Open();

        using var command = new NpgsqlCommand("""
            INSERT INTO "DataProtectionKeys" ("FriendlyName", "Xml")
            VALUES (@friendlyName, @xml)
            ON CONFLICT ("FriendlyName")
            DO UPDATE SET "Xml" = EXCLUDED."Xml";
            """, connection);

        command.Parameters.AddWithValue("friendlyName", friendlyName);
        command.Parameters.AddWithValue("xml", element.ToString(SaveOptions.DisableFormatting));
        command.ExecuteNonQuery();
    }
}
