using System.Xml.Linq;
using Microsoft.AspNetCore.DataProtection.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Npgsql;
using ProphetOps.Data;

namespace ProphetOps.Api;

public sealed class PostgresXmlRepository : IXmlRepository
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<PostgresXmlRepository> _logger;

    public PostgresXmlRepository(IServiceScopeFactory scopeFactory, ILogger<PostgresXmlRepository> logger)
    {
        _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public IReadOnlyCollection<XElement> GetAllElements()
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var entities = db.DataProtectionKeys.AsNoTracking().ToList();
            var elements = new List<XElement>(entities.Count);
            foreach (var entity in entities)
            {
                if (!string.IsNullOrWhiteSpace(entity.Xml))
                {
                    elements.Add(XElement.Parse(entity.Xml));
                }
            }
            return elements.AsReadOnly();
        }
        catch (Exception ex)
        {
            var (errorType, sqlState) = ExtractErrorInfo(ex);
            if (sqlState != null)
            {
                _logger.LogError("Failed to read Data Protection keys from PostgreSQL database. Error: {ErrorType}, SqlState: {SqlState}.", errorType, sqlState);
            }
            else
            {
                _logger.LogError("Failed to read Data Protection keys from PostgreSQL database. Error: {ErrorType}.", errorType);
            }

            var details = sqlState != null ? $"{errorType} (SqlState: {sqlState})" : errorType;
            throw new InvalidOperationException($"Failed to read Data Protection keys from PostgreSQL database: {details}.");
        }
    }

    public void StoreElement(XElement element, string friendlyName)
    {
        ArgumentNullException.ThrowIfNull(element);
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var entity = new DataProtectionKey
            {
                FriendlyName = friendlyName,
                Xml = element.ToString(SaveOptions.DisableFormatting),
            };
            db.DataProtectionKeys.Add(entity);
            db.SaveChanges();
        }
        catch (Exception ex)
        {
            var (errorType, sqlState) = ExtractErrorInfo(ex);
            if (sqlState != null)
            {
                _logger.LogError("Failed to persist Data Protection key '{FriendlyName}' to PostgreSQL database. Error: {ErrorType}, SqlState: {SqlState}.", friendlyName, errorType, sqlState);
            }
            else
            {
                _logger.LogError("Failed to persist Data Protection key '{FriendlyName}' to PostgreSQL database. Error: {ErrorType}.", friendlyName, errorType);
            }

            var details = sqlState != null ? $"{errorType} (SqlState: {sqlState})" : errorType;
            throw new InvalidOperationException($"Failed to persist Data Protection key '{friendlyName}' to PostgreSQL database: {details}.");
        }
    }

    private static (string ErrorType, string? SqlState) ExtractErrorInfo(Exception ex)
    {
        var current = ex;
        string? sqlState = null;
        while (current != null)
        {
            if (current is PostgresException pgEx)
            {
                sqlState = pgEx.SqlState;
                break;
            }
            if (current.InnerException == null) break;
            current = current.InnerException;
        }
        return (current?.GetType().Name ?? ex.GetType().Name, sqlState);
    }
}
