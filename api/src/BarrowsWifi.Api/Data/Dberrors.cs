using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace BarrowsWifi.Api.Data;

public static class DbErrors
{
    /// <summary>
    /// True when SQL Server refused the write because it would duplicate a unique index or constraint
    /// (error 2601 or 2627). Used where a repeat is expected and harmless.
    /// </summary>
    public static bool IsUniqueViolation(this DbUpdateException ex) =>
        ex.InnerException is SqlException { Number: 2601 or 2627 };
}