using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ProphetOps.Data;
using ProphetOps.Domain;

namespace ProphetOps.Api.Controllers;

[ApiController]
[Route("api/expenses")]
[Authorize(Policy = "Expenses")]
public class ExpensesController : ControllerBase
{
    private readonly AppDbContext _db;

    public ExpensesController(AppDbContext db) => _db = db;

    [HttpGet]
    public IActionResult Index()
    {
        var expenses = _db.Expenses
            .OrderByDescending(x => x.ExpenseDate)
            .ThenByDescending(x => x.Id)
            .ToList()
            .Select(Dto);

        return Ok(expenses);
    }

    [HttpPost]
    public IActionResult Store([FromBody] ExpenseRequest request)
    {
        var errors = Validate(request);
        if (errors.Count > 0) return BadRequest(errors);
        if (_db.Expenses.Any(x => x.Code == request.Id))
            return BadRequest(new Dictionary<string, string> { ["id"] = "Expense reference already exists." });

        var expense = new Expense();
        Apply(expense, request);
        _db.Expenses.Add(expense);
        _db.SaveChanges();

        return Ok(Dto(expense));
    }

    [HttpPut("{code}")]
    public IActionResult Update(string code, [FromBody] ExpenseRequest request)
    {
        var expense = _db.Expenses.SingleOrDefault(x => x.Code == code);
        if (expense is null) return NotFound();

        var errors = Validate(request);
        if (errors.Count > 0) return BadRequest(errors);

        Apply(expense, request);
        _db.SaveChanges();

        return Ok(Dto(expense));
    }

    private void Apply(Expense expense, ExpenseRequest request)
    {
        expense.Code = request.Id ?? expense.Code;
        expense.ExpenseDate = DateOnly.Parse(request.Date!);
        expense.Category = request.Category ?? "";
        expense.Amount = request.Amount;
        expense.RelatedPackage = request.RelatedPackage ?? "";
        expense.PaymentStatus = request.PaymentStatus ?? "Pending";
        expense.Notes = request.Notes;
    }

    private static Dictionary<string, string> Validate(ExpenseRequest request)
    {
        var errors = new Dictionary<string, string>();
        if (string.IsNullOrWhiteSpace(request.Date) || !DateOnly.TryParse(request.Date, out _))
            errors["date"] = "Choose the expense date.";
        if (string.IsNullOrWhiteSpace(request.Category))
            errors["category"] = "Choose the expense category.";
        if (request.Amount < 0)
            errors["amount"] = "Expense amount must be zero or more.";
        return errors;
    }

    [HttpPost("{code}/void")]
    public IActionResult Void(string code, [FromBody] VoidRequest request)
    {
        var expense = _db.Expenses.SingleOrDefault(x => x.Code == code);
        if (expense is null) return NotFound();
        if (expense.IsVoided) return BadRequest(new Dictionary<string, string> { ["reason"] = "This expense is already voided." });

        if (string.IsNullOrWhiteSpace(request.Reason))
            return BadRequest(new Dictionary<string, string> { ["reason"] = "Say why this is being voided." });

        expense.VoidedAt = DateTime.UtcNow;
        expense.VoidedBy = User.FindFirst(ClaimTypes.Email)?.Value;
        expense.VoidReason = request.Reason.Trim();

        AuditLog.Record(_db, User, AuditLog.Voided, "Expense", expense.Code, expense.VoidReason);
        _db.SaveChanges();

        return Ok(Dto(expense));
    }

    [HttpPost("{code}/restore")]
    public IActionResult Restore(string code)
    {
        var expense = _db.Expenses.SingleOrDefault(x => x.Code == code);
        if (expense is null) return NotFound();
        if (!expense.IsVoided) return BadRequest(new Dictionary<string, string> { ["reason"] = "This expense is not voided." });

        expense.VoidedAt = null;
        expense.VoidedBy = null;
        expense.VoidReason = null;

        AuditLog.Record(_db, User, AuditLog.Restored, "Expense", expense.Code);
        _db.SaveChanges();

        return Ok(Dto(expense));
    }

    private static object Dto(Expense e) => new
    {
        id = e.Code,
        backendId = e.Id,
        date = e.ExpenseDate.ToString("yyyy-MM-dd"),
        category = e.Category,
        amount = e.Amount,
        relatedPackage = e.RelatedPackage,
        paymentStatus = e.PaymentStatus,
        notes = e.Notes,
        voided = e.VoidedAt != null,
        voidReason = e.VoidReason,
    };
}

public record ExpenseRequest(string? Id, string? Date, string? Category, int Amount, string? RelatedPackage, string? PaymentStatus, string? Notes);
