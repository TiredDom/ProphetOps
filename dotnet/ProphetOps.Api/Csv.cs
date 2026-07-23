using System.Globalization;
using System.Text;

namespace ProphetOps.Api;

public record CsvProblem(int Line, string Reason);

/// The mechanics every spreadsheet import shares: quoted fields, doubled quotes, a byte order
/// mark, lines ending either way, blank lines, and amounts typed with peso signs and commas.
/// Each import decides what its columns mean; none of them re-learns how a CSV is written.
internal static class Csv
{
    /// Line numbers are the physical line the record starts on, so a reported problem is the
    /// line the person opens in their spreadsheet, even when a quoted field spans several.
    public static List<(int Line, List<string> Fields)> Records(string? content)
    {
        content = WithoutBom(content);

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

    public static bool TryMoney(string text, out decimal amount)
    {
        var cleaned = new StringBuilder(text.Length);
        foreach (var c in text.Replace("PHP", "", StringComparison.OrdinalIgnoreCase))
        {
            if (c is '₱' or 'P' or 'p' or ',' or '_' || char.IsWhiteSpace(c)) continue;
            cleaned.Append(c);
        }

        return decimal.TryParse(cleaned.ToString(), NumberStyles.Number, CultureInfo.InvariantCulture, out amount);
    }

    public static bool TryCount(string text, out int count)
    {
        var cleaned = new StringBuilder(text.Length);
        foreach (var c in text)
        {
            if (c == ',' || char.IsWhiteSpace(c)) continue;
            cleaned.Append(c);
        }

        return int.TryParse(cleaned.ToString(), NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out count);
    }

    public static string? Optional(string value) => value.Length == 0 ? null : value;

    public static string Or(string[] values) =>
        string.Join(", ", values[..^1]) + ", or " + values[^1];

    private static string WithoutBom(string? content)
    {
        if (string.IsNullOrEmpty(content)) return "";
        return content[0] == '\uFEFF' ? content[1..] : content;
    }

    private static bool IsBlank(List<string> fields) =>
        fields.Count == 1 && string.IsNullOrWhiteSpace(fields[0]);
}
