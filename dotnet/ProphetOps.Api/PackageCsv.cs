namespace ProphetOps.Api;

public record PackageCsvRow(
    int Line,
    string Package,
    string Destination,
    int Price,
    string? Code,
    string? Duration,
    string? Inclusions,
    int Slots,
    string Status);

public record PackageCsvResult(
    IReadOnlyList<PackageCsvRow> Rows,
    IReadOnlyList<CsvProblem> Problems,
    IReadOnlyList<CsvProblem> Warnings);

/// Reads the agency's package catalog out of a spreadsheet export.
///
/// Same bargain as BookingCsv: the hard parts of an import are in the file itself, so the file
/// is read here, apart from the endpoint, where each rule is cheap to pin down with a test.
/// Nothing is dropped in silence — a row either parses or is named with the line it sits on.
public static class PackageCsv
{
    public const int MaxRows = 2000;

    private static readonly string[] RequiredColumns = { "package", "destination", "price" };

    private static readonly string[] Statuses = { "Normal", "Low", "Critical" };

    public static PackageCsvResult Parse(string? content)
    {
        var rows = new List<PackageCsvRow>();
        var problems = new List<CsvProblem>();
        var warnings = new List<CsvProblem>();

        var records = Csv.Records(content);
        if (records.Count == 0)
        {
            problems.Add(new CsvProblem(1, "The file is empty."));
            return new PackageCsvResult(rows, problems, warnings);
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
                problems.Add(new CsvProblem(header.Line, $"The file has no '{column}' column."));
            return new PackageCsvResult(rows, problems, warnings);
        }

        var body = records.Skip(1).ToList();
        if (body.Count == 0)
        {
            problems.Add(new CsvProblem(header.Line, "The file has column headings but no packages under them."));
            return new PackageCsvResult(rows, problems, warnings);
        }

        if (body.Count > MaxRows)
        {
            problems.Add(new CsvProblem(header.Line,
                $"This file has {body.Count} rows and {MaxRows} is the most that can be imported at once. Split it and import the parts separately."));
            return new PackageCsvResult(rows, problems, warnings);
        }

        var usedCodes = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var usedNames = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        foreach (var record in body)
        {
            var fields = record.Fields;
            var line = record.Line;

            string Value(string name) =>
                columns.TryGetValue(name, out var index) && index < fields.Count ? fields[index].Trim() : "";

            if (fields.Count > header.Fields.Count)
            {
                problems.Add(new CsvProblem(line,
                    $"This row has {fields.Count} values but the headings only cover {header.Fields.Count}. Check for a comma inside a field that is not wrapped in quotes."));
                continue;
            }

            var reasons = new List<string>();

            var package = Value("package");
            if (package.Length == 0)
                reasons.Add("The package name is missing.");
            else if (usedNames.TryGetValue(package, out var earlierName))
                reasons.Add($"The package '{package}' is already on line {earlierName} of this file.");

            var destination = Value("destination");
            if (destination.Length == 0) reasons.Add("The destination is missing.");

            var priceText = Value("price");
            var price = 0;
            if (priceText.Length == 0)
                reasons.Add("The price is missing.");
            else if (!Csv.TryMoney(priceText, out var amount))
                reasons.Add($"'{priceText}' is not an amount.");
            else if (amount < 0)
                reasons.Add($"The price '{priceText}' is negative.");
            else if (amount > int.MaxValue)
                reasons.Add($"The price '{priceText}' is larger than this system can hold.");
            else
                price = (int)Math.Round(amount, MidpointRounding.AwayFromZero);

            var slotsText = Value("slots");
            var slots = 0;
            if (slotsText.Length > 0)
            {
                if (!Csv.TryCount(slotsText, out slots))
                    reasons.Add($"'{slotsText}' is not a whole number of slots.");
                else if (slots < 0)
                    reasons.Add("The slot count must be zero or more.");
            }

            var statusText = Value("status");
            var status = Statuses[0];
            if (statusText.Length > 0)
            {
                var match = Statuses.FirstOrDefault(s => s.Equals(statusText, StringComparison.OrdinalIgnoreCase));
                if (match is null) reasons.Add($"'{statusText}' is not a package status. Use {Csv.Or(Statuses)}.");
                else status = match;
            }

            var code = Value("code");
            if (code.Length > 0 && usedCodes.TryGetValue(code, out var earlierCode))
                reasons.Add($"The code '{code}' is already used on line {earlierCode} of this file.");

            if (reasons.Count > 0)
            {
                problems.Add(new CsvProblem(line, string.Join(" ", reasons)));
                continue;
            }

            usedNames[package] = line;
            if (code.Length > 0) usedCodes[code] = line;

            rows.Add(new PackageCsvRow(
                line,
                package,
                destination,
                price,
                Csv.Optional(code),
                Csv.Optional(Value("duration")),
                Csv.Optional(Value("inclusions")),
                slots,
                status));
        }

        return new PackageCsvResult(rows, problems, warnings);
    }
}
