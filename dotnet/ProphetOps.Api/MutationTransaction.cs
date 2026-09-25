using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using ProphetOps.Data;

namespace ProphetOps.Api;

public sealed class MutationTransaction(AppDbContext db, MaintenanceGate gate, ILogger<MutationTransaction> logger) : IAsyncActionFilter
{
    private readonly List<Action> _afterCommit = new();
    private readonly List<Action> _afterRollback = new();

    public void AfterCommit(Action action) => _afterCommit.Add(action);
    public void AfterRollback(Action action) => _afterRollback.Add(action);

    public static ConflictObjectResult Stale() => new(new
    {
        code = "stale_revision", message = "This record has changed. Reload it before saving again.",
    });

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var request = context.HttpContext.Request;
        if (HttpMethods.IsGet(request.Method) || HttpMethods.IsHead(request.Method))
        {
            await next();
            return;
        }

        ActionExecutedContext? executed = null;
        var committed = false;
        IDisposable? maintenanceLease = null;
        try
        {
            var admission = gate.TryEnterMutation();
            if (!admission.Allowed)
            {
                context.HttpContext.Response.Headers.RetryAfter = MaintenanceGate.RetryAfterSeconds.ToString();
                context.Result = MaintenanceGate.RetryableResult();
                return;
            }
            maintenanceLease = admission.Lease;

            var connection = (SqliteConnection)db.Database.GetDbConnection();
            connection.DefaultTimeout = 5;
            await db.Database.OpenConnectionAsync(request.HttpContext.RequestAborted);
            // Take the writer lock before reading stock or active owners, across processes too.
            await using var transaction = connection.BeginTransaction(deferred: false);
            await using var enlisted = await db.Database.UseTransactionAsync(transaction, request.HttpContext.RequestAborted);

            var principal = context.HttpContext.User;
            var id = principal.FindFirstValue(ClaimTypes.NameIdentifier);
            var version = principal.FindFirstValue(StaffCookieEvents.SessionVersionClaim);
            var actor = int.TryParse(id, out var userId)
                ? await db.Users.AsNoTracking().SingleOrDefaultAsync(u => u.Id == userId, request.HttpContext.RequestAborted)
                : null;
            if (actor is null || actor.Status != "Active" || !int.TryParse(version, out var sessionVersion)
                || actor.SessionVersion != sessionVersion || actor.Role != principal.FindFirstValue(ClaimTypes.Role)
                || actor.Email != principal.FindFirstValue(ClaimTypes.Email))
            {
                await context.HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
                context.Result = new UnauthorizedResult();
                return;
            }

            executed = await next();
            if (executed.Exception is not null && !executed.ExceptionHandled)
            {
                if (IsConflict(executed.Exception))
                {
                    executed.ExceptionHandled = true;
                    executed.Result = WriteConflict();
                }
                return;
            }
            if ((executed.Result as IStatusCodeActionResult)?.StatusCode is >= 400) return;
            await transaction.CommitAsync(request.HttpContext.RequestAborted);
            committed = true;
        }
        catch (Exception exception) when (IsConflict(exception))
        {
            if (executed is null) context.Result = WriteConflict();
            else executed.Result = WriteConflict();
        }
        finally
        {
            maintenanceLease?.Dispose();
            foreach (var cleanup in committed ? _afterCommit : _afterRollback)
            {
                try { cleanup(); }
                catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
                {
                    logger.LogWarning(exception, "Package image cleanup could not finish.");
                }
            }
        }
    }

    private static bool IsConflict(Exception? exception) => exception is DbUpdateConcurrencyException
        or MutationConflictException || exception is SqliteException { SqliteErrorCode: 5 or 6 }
        or SqliteException { SqliteExtendedErrorCode: 1555 or 2067 }
        || exception is DbUpdateException { InnerException: not null } update && IsConflict(update.InnerException);

    private static ConflictObjectResult WriteConflict() => new(new
    {
        code = "write_conflict", message = "The change could not be saved. Reload the record and check its current state before trying again.",
    });
}

public sealed class MutationConflictException : Exception;
