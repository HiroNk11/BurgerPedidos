using System.Data;
using Gastronomia.Application;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
namespace Gastronomia.Infrastructure;

internal static class SqlTransactions
{
    internal static bool IsRetryable(Exception error)
    {
        // EF SQL Server puede envolver el deadlock en InvalidOperationException.
        for (Exception? current = error; current is not null; current = current.InnerException)
            if (current is DbUpdateConcurrencyException || current is SqlException { Number: 1205 or 2601 or 2627 }) return true;
        return false;
    }
    internal static async Task<T> RunAsync<T>(IDbContextFactory<GastronomiaDbContext> factory, Func<GastronomiaDbContext, Task<T>> action, CancellationToken ct)
    {
        for (var attempt = 0; ; attempt++)
        {
            await using var db = await factory.CreateDbContextAsync(ct);
            await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
            try { var result = await action(db); await tx.CommitAsync(ct); return result; }
            catch (Exception ex) when (IsRetryable(ex))
            {
                await tx.RollbackAsync(ct);
                if (attempt >= 2) throw new ConflictException("Los datos cambiaron concurrentemente. Volvé a consultar el estado.");
                await Task.Delay(30 * (attempt + 1), ct);
            }
        }
    }
}
