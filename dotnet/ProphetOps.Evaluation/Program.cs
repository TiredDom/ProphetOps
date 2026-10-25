// Tests the Holt-Winters forecast on monthly totals from a CSV file, offline: the same method and
// tuning the application uses, run as a rolling-origin test. Nothing is sent anywhere and nothing is
// written to the application's database, so an agency's own figures can be tested before, or
// without, entering them into ProphetOps.
//
//   dotnet run --project ProphetOps.Evaluation -- agency-revenue.csv --out results
using System.Globalization;
using System.Text;
using ProphetOps.Forecasting;

const string Usage = """
    Usage: ProphetOps.Evaluation <monthly.csv> [--horizon 6] [--season 12] [--out <folder>]

    The CSV has one row per month: month,value (for example 2024-01,152000). A header row is
    allowed and rows may be in any order, but every month between the first and the last must be
    there. Write 0 for a month that really had nothing.

      --horizon   months to forecast from each starting month (1 to 12, default 6)
      --season    months in one seasonal cycle (default 12)
      --out       folder for forecast-test-points.csv and forecast-test-summary.csv
    """;

string? file = null;
string? outFolder = null;
int horizon = 6;
int season = 12;

for (int i = 0; i < args.Length; i++)
{
    switch (args[i])
    {
        case "-h" or "--help":
            Console.WriteLine(Usage);
            return 0;
        case "--horizon" when i + 1 < args.Length && int.TryParse(args[i + 1], out var h) && h is >= 1 and <= 12:
            horizon = h;
            i++;
            break;
        case "--season" when i + 1 < args.Length && int.TryParse(args[i + 1], out var s) && s >= 2:
            season = s;
            i++;
            break;
        case "--out" when i + 1 < args.Length:
            outFolder = args[++i];
            break;
        default:
            if (args[i].StartsWith('-') || file is not null)
            {
                Console.Error.WriteLine($"Not understood: {args[i]}");
                Console.Error.WriteLine(Usage);
                return 2;
            }
            file = args[i];
            break;
    }
}

if (file is null)
{
    Console.Error.WriteLine(Usage);
    return 2;
}

MonthlySeries series;
try
{
    series = MonthlySeriesCsv.Parse(File.ReadAllText(file));
}
catch (FileNotFoundException)
{
    Console.Error.WriteLine($"No file at {file}.");
    return 2;
}
catch (FormatException problem)
{
    Console.Error.WriteLine($"{file} could not be read:");
    Console.Error.WriteLine(problem.Message);
    return 2;
}

var culture = CultureInfo.InvariantCulture;
string MonthName(DateOnly month) => month.ToString("MMMM yyyy", culture);
string Key(DateOnly month) => month.ToString("yyyy-MM", culture);

BacktestResult result = RollingOriginEvaluation.Evaluate(series.Values, season, horizon);

Console.WriteLine("ProphetOps forecast accuracy test");
Console.WriteLine($"File:    {Path.GetFileName(file)}");
Console.WriteLine($"Months:  {series.Values.Count}, {MonthName(series.FirstMonth)} to {MonthName(series.LastMonth)}");

if (!result.Ok)
{
    Console.WriteLine();
    Console.WriteLine($"{result.Reason} This file has {result.MonthsAvailable} months; at least {result.MonthsRequired} are needed.");
    return 1;
}

Console.WriteLine($"Method:  Holt-Winters as ProphetOps runs it, forecasting {horizon} months ahead from each month");
Console.WriteLine($"         from {MonthName(series.MonthAt(result.FirstOrigin - 1))} on, each time using only the months before.");
Console.WriteLine();

string Percent(double? value) => value is { } v ? v.ToString("0.0", culture) + "%" : "n/a";
// Pesos read best whole; small counts such as bookings a month keep their decimals.
string Amount(double value) => value.ToString(Math.Abs(value) >= 100 ? "#,0" : "0.##", culture);
string Hits(int inside, int forecasts) => $"{inside} of {forecasts} ({(double)inside / forecasts:P0})";

var table = new List<string[]>
{
    new[] { "Months ahead", "Forecasts", "Holt-Winters miss", "Same month last year", "Inside 80% range" },
};
foreach (var h in result.ByHorizon)
    table.Add([h.Step.ToString(culture), h.Forecasts.ToString(culture), Percent(h.Mape), Percent(h.SeasonalNaiveMape), Hits(h.InsideRange, h.Forecasts)]);
var all = result.Overall!;
table.Add(["All", all.Forecasts.ToString(culture), Percent(all.Mape), Percent(all.SeasonalNaiveMape), Hits(all.InsideRange, all.Forecasts)]);

var widths = Enumerable.Range(0, table[0].Length).Select(c => table.Max(row => row[c].Length)).ToArray();
foreach (var row in table)
    Console.WriteLine(string.Join("  ", row.Select((cell, c) => c == 0 ? cell.PadRight(widths[c]) : cell.PadLeft(widths[c]))));

Console.WriteLine();
Console.WriteLine("Miss is the mean absolute percentage error over months with something recorded.");
if (all.SkillVsSeasonalNaive is { } skill)
{
    var comparison = skill >= 0
        ? $"Holt-Winters missed {skill:P0} less than repeating the same month last year"
        : $"Repeating the same month last year missed {-skill / (1 + skill):P0} less than Holt-Winters";
    Console.WriteLine($"{comparison} (average miss {Amount(all.Mae)} against {Amount(all.SeasonalNaiveMae)}).");
}

if (outFolder is not null)
{
    Directory.CreateDirectory(outFolder);
    string Number(double value) => Math.Round(value, 2, MidpointRounding.AwayFromZero).ToString("0.##", culture);

    var points = new StringBuilder("made_after,month,months_ahead,recorded,forecast,lower_80,upper_80,same_month_last_year,inside_range\r\n");
    foreach (var p in result.Points)
    {
        points.Append(string.Join(",",
            Key(series.MonthAt(p.Origin - 1)), Key(series.MonthAt(p.Target)), p.Step.ToString(culture),
            Number(p.Actual), Number(p.Forecast), Number(p.Lower), Number(p.Upper), Number(p.SeasonalNaive),
            p.InsideRange ? "yes" : "no")).Append("\r\n");
    }

    var summary = new StringBuilder("months_ahead,forecasts,mape,mae,same_month_last_year_mape,same_month_last_year_mae,inside_range,coverage\r\n");
    string Optional(double? value) => value is { } v ? Number(v) : "";
    foreach (var h in result.ByHorizon)
    {
        summary.Append(string.Join(",", h.Step.ToString(culture), h.Forecasts.ToString(culture), Optional(h.Mape), Number(h.Mae),
            Optional(h.SeasonalNaiveMape), Number(h.SeasonalNaiveMae), h.InsideRange.ToString(culture), Number(h.Coverage * 100))).Append("\r\n");
    }
    summary.Append(string.Join(",", "all", all.Forecasts.ToString(culture), Optional(all.Mape), Number(all.Mae),
        Optional(all.SeasonalNaiveMape), Number(all.SeasonalNaiveMae), all.InsideRange.ToString(culture), Number(all.Coverage * 100))).Append("\r\n");

    var bom = new UTF8Encoding(encoderShouldEmitUTF8Identifier: true);
    File.WriteAllText(Path.Combine(outFolder, "forecast-test-points.csv"), points.ToString(), bom);
    File.WriteAllText(Path.Combine(outFolder, "forecast-test-summary.csv"), summary.ToString(), bom);
    Console.WriteLine();
    Console.WriteLine($"Saved forecast-test-points.csv and forecast-test-summary.csv in {Path.GetFullPath(outFolder)}.");
}

return 0;
