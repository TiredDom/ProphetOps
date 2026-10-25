using System.Globalization;
using System.Text;

namespace ProphetOps.Forecasting;

/// A run of consecutive monthly totals, oldest first.
public sealed record MonthlySeries(DateOnly FirstMonth, IReadOnlyList<double> Values)
{
    public DateOnly LastMonth => FirstMonth.AddMonths(Values.Count - 1);

    public DateOnly MonthAt(int index) => FirstMonth.AddMonths(index);
}

/// Reads monthly totals from a two-column CSV file (month, value) so the accuracy test can run on
/// figures that were never entered into the application, such as an agency's past totals. Rows may
/// come in any order, but the months must be consecutive with none repeated: a missing month is
/// reported, never filled in, because a guessed month would change the result.
public static class MonthlySeriesCsv
{
    private static readonly string[] MonthFormats =
    [
        "yyyy-MM", "yyyy-M", "yyyy-MM-dd", "yyyy/MM", "yyyy/MM/dd", "MMM yyyy", "MMMM yyyy", "MMM-yyyy", "MM/yyyy",
    ];

    public static MonthlySeries Parse(string text)
    {
        var rows = new List<(DateOnly Month, double Value, int Line)>();
        var problems = new List<string>();
        var lines = text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
        bool firstContentLine = true;

        for (int i = 0; i < lines.Length; i++)
        {
            int lineNumber = i + 1;
            string line = lines[i].Trim().TrimStart('﻿');
            if (line.Length == 0 || line.StartsWith('#')) continue;

            var fields = SplitFields(line);
            bool isFirst = firstContentLine;
            firstContentLine = false;

            if (fields.Count < 2)
            {
                problems.Add($"Line {lineNumber}: expected a month and a value separated by a comma.");
                continue;
            }

            bool monthOk = TryReadMonth(fields[0], out var month);
            bool valueOk = TryReadValue(fields[1], out var value);

            // A first line that is neither a month nor a number is the header, such as "month,value".
            if (isFirst && !monthOk && !valueOk) continue;

            // "2024-01,1,234" would otherwise be read as 1: a number with commas has to be quoted.
            if (fields.Skip(2).Any(f => f.Trim().Length > 0))
            {
                problems.Add($"Line {lineNumber}: more than two columns. Put a number with commas in quotes, like \"1,234\", or leave the commas out.");
                continue;
            }

            if (!monthOk) problems.Add($"Line {lineNumber}: \"{fields[0]}\" is not a month. Use a form like 2024-01.");
            if (!valueOk) problems.Add($"Line {lineNumber}: \"{fields[1]}\" is not a number of zero or more.");
            if (monthOk && valueOk) rows.Add((month, value, lineNumber));
        }

        if (problems.Count == 0 && rows.Count == 0)
            problems.Add("The file has no months in it.");

        foreach (var repeated in rows.GroupBy(r => r.Month).Where(g => g.Count() > 1))
        {
            problems.Add($"{repeated.Key:yyyy-MM} appears more than once (lines {string.Join(", ", repeated.Select(r => r.Line))}).");
        }

        var ordered = rows.GroupBy(r => r.Month).Select(g => g.First()).OrderBy(r => r.Month).ToList();
        for (int i = 1; i < ordered.Count; i++)
        {
            for (var missing = ordered[i - 1].Month.AddMonths(1); missing < ordered[i].Month; missing = missing.AddMonths(1))
            {
                problems.Add($"No row for {missing:yyyy-MM}. Add it, with 0 if the month really had none.");
            }
        }

        if (problems.Count > 0)
            throw new FormatException(string.Join(Environment.NewLine, problems.Take(20)) +
                (problems.Count > 20 ? $"{Environment.NewLine}…and {problems.Count - 20} more." : ""));

        return new MonthlySeries(ordered[0].Month, ordered.Select(r => r.Value).ToList());
    }

    private static bool TryReadMonth(string field, out DateOnly month)
    {
        month = default;
        if (!DateOnly.TryParseExact(field.Trim(), MonthFormats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
            return false;
        month = new DateOnly(date.Year, date.Month, 1);
        return true;
    }

    /// Accepts plain numbers and the usual written forms: "₱1,234.50", "PHP 1234", "1 234".
    private static bool TryReadValue(string field, out double value)
    {
        var cleaned = field.Trim()
            .Replace("₱", "", StringComparison.Ordinal)
            .Replace("PHP", "", StringComparison.OrdinalIgnoreCase)
            .Replace(",", "", StringComparison.Ordinal)
            .Replace(" ", "", StringComparison.Ordinal);
        return double.TryParse(cleaned, NumberStyles.Float, CultureInfo.InvariantCulture, out value)
            && double.IsFinite(value)
            && value >= 0;
    }

    /// Comma- or tab-separated fields; a field in double quotes may contain the separator.
    private static List<string> SplitFields(string line)
    {
        char separator = line.Contains('\t') && !line.Contains(',') ? '\t' : ',';
        var fields = new List<string>();
        var current = new StringBuilder();
        bool quoted = false;

        for (int i = 0; i < line.Length; i++)
        {
            char c = line[i];
            if (c == '"')
            {
                if (quoted && i + 1 < line.Length && line[i + 1] == '"') { current.Append('"'); i++; }
                else quoted = !quoted;
            }
            else if (c == separator && !quoted)
            {
                fields.Add(current.ToString());
                current.Clear();
            }
            else
            {
                current.Append(c);
            }
        }

        fields.Add(current.ToString());
        return fields;
    }
}
