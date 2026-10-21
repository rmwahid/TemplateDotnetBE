using MySqlConnector;
using Npgsql;

namespace Infrastructure.Utils;

/// <summary>
/// Recognises provider-specific database errors without leaking the driver type into
/// services. Both drivers are referenced by Infrastructure, so this stays in one place.
/// </summary>
public static class DbError
{
    private const int MySqlDuplicateEntry = 1062;

    public static bool TryGetUniqueViolation(
        Exception exception,
        out string? constraintName)
    {
        for (Exception? current = exception; current is not null; current = current.InnerException)
        {
            switch (current)
            {
                case PostgresException pg
                    when pg.SqlState == PostgresErrorCodes.UniqueViolation:
                    constraintName = pg.ConstraintName;
                    return true;

                case MySqlException my when my.Number == MySqlDuplicateEntry:
                    constraintName = ExtractMySqlKeyName(my.Message);
                    return true;
            }
        }

        constraintName = null;
        return false;
    }

    private static string? ExtractMySqlKeyName(string message)
    {
        const string marker = "for key '";
        var start = message.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
        if (start < 0)
        {
            return null;
        }

        start += marker.Length;
        var end = message.IndexOf('\'', start);
        return end < 0 ? null : message[start..end];
    }
}
