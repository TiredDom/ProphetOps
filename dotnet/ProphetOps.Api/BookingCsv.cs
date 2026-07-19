using System.Globalization;
using System.Text;

namespace ProphetOps.Api;

public record BookingCsvRow(
    int Line,
    DateOnly Date,
    string Client,
    string Package,
    string Destination,
    int Passengers,
    int Revenue,
    string? Code,
    string PaymentStatus,
    string BookingStatus,
    string? Staff,
    string? Notes);

public record BookingCsvProblem(int Line, string Reason);

public record BookingCsvResult(
    IReadOnlyList<BookingCsvRow> Rows,
    IReadOnlyList<BookingCsvProblem> Problems,
    IReadOnlyList<BookingCsvProblem> Warnings);

/// Reads the agency's booking history out of a spreadsheet export.
///
/// Kept apart from the endpoint because every hard part of an import is in the file itself: a
/// sheet that writes dates its own way, peso signs typed into a number column, a client name
/// with a comma in it. Those are cheap to pin down with tests and expensive to meet halfway
/// through an import. Nothing is dropped in silence — a row either parses or is named with the
/// line it sits on.
public static class BookingCsv
{
    public const int MaxRows = 5000;

    private static readonly string[] RequiredColumns =
        { "date", "client", "package", "destination", "passengers", "revenue" };

    private static readonly string[] PaymentStatuses = { "Paid", "Partially Paid", "Pending" };

    private static readonly string[] BookingStatuses = { "Confirmed", "Reserved", "Pending" };

    private static readonly string[] WrittenDateFormats = { "d MMM yyyy", "d MMMM yyyy" };

    public static BookingCsvResult Parse(string? content)
    {
        var rows = new List<BookingCsvRow>();
        var problems = new List<BookingCsvProblem>();
        var warnings = new List<BookingCsvProblem>();

        var records = Split(WithoutBom(content));
        if (records.Count == 0)
        {
            problems.Add(new BookingCsvProblem(1, "The file is empty."));
            return new BookingCsvResult(rows, problems, warnings);
        }

        var header = records[0];
        var columns = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < header.Fields.Count; i++)
        {
            var name = header.Fields[i].Trim();
            if (name.Length > 0) columns.TryAdd(name, i);
        }

        var missing = RequiredColumns.Where(c => !columns.ContainsKey(c)).ToList();
        if (missing.Count > 0)
        {
            foreach (var column in missing)
                problems.Add(new BookingCsvProblem(header.Line, $"The file has no '{column}' column."));
            return new BookingCsvResult(rows, problems, warnings);
        }

        var body = records.Skip(1).ToList();
        if (body.Count == 0)
        {
            problems.Add(new BookingCsvProblem(header.Line, "The file has column headings but no bookings under them."));
            return new BookingCsvResult(rows, problems, warnings);
        }

        if (body.Count > MaxRows)
        {
            problems.Add(new BookingCsvProblem(header.Line,
                $"This file has {body.Count} rows and {MaxRows} is the most that can be imported at once. Split it and import the parts separately."));
            return new BookingCsvResult(rows, problems, warnings);
        }

        var used = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        foreach (var record in body)
        {
            var fields = record.Fields;
            var line = record.Line;

            string Value(string name) =>
                columns.TryGetValue(name, out var index) && index < fields.Count ? fields[index].Trim() : "";

            if (fields.Count > header.Fields.Count)
            {
                problems.Add(new BookingCsvProblem(line,
                    $"This row has {fields.Count} values but the headings only cover {header.Fields.Count}. Check for a comma inside a field that is not wrapped in quotes."));
                continue;
            }

            var reasons = new List<string>();
            string? warning = null;

            var dateText = Value("date");
            var date = default(DateOnly);
            if (dateText.Length == 0)
            {
                reasons.Add("The date is missing.");
            }
            else if (!TryDate(dateText, out date, out var eitherWayRound))
            {
                reasons.Add($"'{dateText}' is not a date. Write it as 2024-03-05, 05/03/2024, or 5 Mar 2024.");
            }
            else if (eitherWayRound)
            {
                warning = $"'{dateText}' could be read either way round. It was taken day first, as {date:yyyy-MM-dd}.";
            }

            var client = Value("client");
            if (client.Length == 0) reasons.Add("The client is missing.");

            var package = Value("package");
            if (package.Length == 0) reasons.Add("The package is missing.");

            var destination = Value("destination");
            if (destination.Length == 0) reasons.Add("The destination is missing.");

            var passengersText = Value("passengers");
            var passengers = 0;
            if (passengersText.Length == 0)
                reasons.Add("The passenger count is missing.");
            else if (!TryCount(passengersText, out passengers))
                reasons.Add($"'{passengersText}' is not a whole number of passengers.");
            else if (passengers < 1)
                reasons.Add("The passenger count must be at least 1.");

            var revenueText = Value("revenue");
            var revenue = 0;
            if (revenueText.Length == 0)
                reasons.Add("The revenue is missing.");
            else if (!TryMoney(revenueText, out var amount))
                reasons.Add($"'{revenueText}' is not an amount.");
            else if (amount < 0)
                reasons.Add($"The revenue '{revenueText}' is negative.");
            else if (amount > int.MaxValue)
                reasons.Add($"The revenue '{revenueText}' is larger than this system can hold.");
            else
                revenue = (int)Math.Round(amount, MidpointRounding.AwayFromZero);

            var code = Value("code");
            if (code.Length > 0 && used.TryGetValue(code, out var earlier))
                reasons.Add($"The reference '{code}' is already used on line {earlier} of this file.");

            var payment = Value("payment");
            var paymentStatus = PaymentStatuses[0];
            if (payment.Length > 0)
            {
                var match = PaymentStatuses.FirstOrDefault(s => s.Equals(payment, StringComparison.OrdinalIgnoreCase));
                if (match is null) reasons.Add($"'{payment}' is not a payment status. Use {Or(PaymentStatuses)}.");
                else paymentStatus = match;
            }

            var status = Value("status");
            var bookingStatus = BookingStatuses[0];
            if (status.Length > 0)
            {
                var match = BookingStatuses.FirstOrDefault(s => s.Equals(status, StringComparison.OrdinalIgnoreCase));
                if (match is null) reasons.Add($"'{status}' is not a booking status. Use {Or(BookingStatuses)}.");
                else bookingStatus = match;
            }

            if (reasons.Count > 0)
            {
                problems.Add(new BookingCsvProblem(line, string.Join(" ", reasons)));
                continue;
            }

            if (code.Length > 0) used[code] = line;
            if (warning is not null) warnings.Add(new BookingCsvProblem(line, warning));

            rows.Add(new BookingCsvRow(
                line,
                date,
                client,
                package,
                destination,
                passengers,
                revenue,
                code.Length > 0 ? code : null,
                paymentStatus,
                bookingStatus,
                Optional(Value("staff")),
                Optional(Value("notes"))));
        }

        return new BookingCsvResult(rows, problems, warnings);
    }

    private static string? Optional(string value) => value.Length == 0 ? null : value;

    private static string WithoutBom(string? content)
    {
        if (string.IsNullOrEmpty(content)) return "";
        return content[0] == '\uFEFF' ? content[1..] : content;
    }

    private static string Or(string[] values) =>
        string.Join(", ", values[..^1]) + ", or " + values[^1];

    /// Line numbers are the physical line the record starts on, so a reported problem is the
    /// line the person opens in their spreadsheet, even when a quoted field spans several.
    private static List<(int Line, List<string> Fields)> Split(string content)
    {
        var records = new List<(int, List<string>)>();
        var fields = new List<string>();
        var field = new StringBuilder();
        var quoted = false;
        var line = 1;
        var start = 1;
        var i = 0;

        while (i < content.Length)
        {
            var c = content[i];

            if (quoted)
            {
                if (c == '"' && i + 1 < content.Length && content[i + 1] == '"')
                {
                    field.Append('"');
                    i += 2;
                    continue;
                }

                if (c == '"')
                {
                    quoted = false;
                    i++;
                    continue;
                }

                if (c == '\n') line++;
                field.Append(c);
                i++;
                continue;
            }

            if (c == '"')
            {
                quoted = true;
                i++;
                continue;
            }

            if (c == ',')
            {
                fields.Add(field.ToString());
                field.Clear();
                i++;
                continue;
            }

            if (c == '\r' || c == '\n')
            {
                if (c == '\r' && i + 1 < content.Length && content[i + 1] == '\n') i++;
                i++;

                fields.Add(field.ToString());
                field.Clear();
                if (!IsBlank(fields)) records.Add((start, fields));
                fields = new List<string>();
                line++;
                start = line;
                continue;
            }

            field.Append(c);
            i++;
        }

        if (field.Length > 0 || fields.Count > 0)
        {
            fields.Add(field.ToString());
            if (!IsBlank(fields)) records.Add((start, fields));
        }

        return records;
    }

    private static bool IsBlank(List<string> fields) =>
        fields.Count == 1 && string.IsNullOrWhiteSpace(fields[0]);

    private static bool TryDate(string text, out DateOnly date, out bool eitherWayRound)
    {
        eitherWayRound = false;
        text = text.Trim();

        if (DateOnly.TryParseExact(text, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out date))
            return true;

        var parts = text.Split('/');
        if (parts.Length == 3 && parts[2].Length == 4
            && Number(parts[0], out var first) && Number(parts[1], out var second) && Number(parts[2], out var year))
        {
            var dayFirst = Real(year, second, first);
            var monthFirst = Real(year, first, second);

            // Below the thirteenth both readings are legal dates, so nothing in the file can
            // settle it. The agency writes day first, so that is what wins, and the row is
            // flagged rather than quietly assumed.
            if (dayFirst)
            {
                eitherWayRound = monthFirst && first != second;
                date = new DateOnly(year, second, first);
                return true;
            }

            if (monthFirst)
            {
                date = new DateOnly(year, first, second);
                return true;
            }

            return false;
        }

        return DateOnly.TryParseExact(text, WrittenDateFormats, CultureInfo.InvariantCulture, DateTimeStyles.None, out date);
    }

    private static bool Number(string text, out int value) =>
        int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out value);

    private static bool Real(int year, int month, int day) =>
        year >= 1 && year <= 9999 && month >= 1 && month <= 12 && day >= 1 && day <= DateTime.DaysInMonth(year, month);

    private static bool TryMoney(string text, out decimal amount)
    {
        var cleaned = new StringBuilder(text.Length);
        foreach (var c in text.Replace("PHP", "", StringComparison.OrdinalIgnoreCase))
        {
            if (c is '₱' or 'P' or 'p' or ',' or '_' || char.IsWhiteSpace(c)) continue;
            cleaned.Append(c);
        }

        return decimal.TryParse(cleaned.ToString(), NumberStyles.Number, CultureInfo.InvariantCulture, out amount);
    }

    private static bool TryCount(string text, out int count)
    {
        var cleaned = new StringBuilder(text.Length);
        foreach (var c in text)
        {
            if (c == ',' || char.IsWhiteSpace(c)) continue;
            cleaned.Append(c);
        }

        return int.TryParse(cleaned.ToString(), NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out count);
    }
}
