using System.Globalization;

namespace ProphetOps.Forecasting;

public record TrajectoryStep(string MonthLabel, double Value, double Lower, double Upper);

public record TrajectoryNote(string Kind, string Text);

public record TrajectoryInput
{
    public required IReadOnlyList<TrajectoryStep> Steps { get; init; }
    public required string Direction { get; init; }
    public required double ChangePercent { get; init; }
    public required string LastRecordedLabel { get; init; }
    public required double LastRecordedValue { get; init; }
    public required double Mape { get; init; }
    public required int Accuracy { get; init; }
    public required double Mae { get; init; }
    public required double SeasonalNaiveMae { get; init; }
}

/// Turns a finished forecast into plain-language statements an owner can act on.
///
/// Every rule is a plain comparison over figures the forecaster already produced; nothing here
/// models anything. Rules that find nothing worth saying stay silent, so a quiet forecast
/// produces a short read rather than padding.
public static class TrajectoryInsights
{
    private const double PeakAboveAverage = 0.08;
    private const double PeakStandsOut = 0.02;
    private const double MeaningfulChange = 1.0;
    private const double WideningRatio = 1.4;
    private const double ReliableMape = 6.0;
    private const double UnreliableMape = 12.0;

    public static IReadOnlyList<TrajectoryNote> Build(TrajectoryInput input)
    {
        var notes = new List<TrajectoryNote>();
        var steps = input.Steps;
        if (steps.Count == 0 || !Usable(input)) return notes;

        var peak = steps.MaxBy(s => s.Value)!;
        var trough = steps.MinBy(s => s.Value)!;
        var average = steps.Average(s => s.Value);
        var horizon = steps.Count;

        // Ordered by what the owner has to decide, not by how the rules were written: what is
        // happening, what to do about it, what is imminent, then the analytical detail.
        // On a level series the highest month is whichever one sorted first, not a peak. Naming
        // it would state an accident of ordering as a finding.
        var peakStandsOut = average > 0 && (peak.Value - average) / average >= PeakStandsOut;

        notes.Add(new TrajectoryNote("direction", Headline(input, peak, horizon, peakStandsOut)));

        if (average > 0 && (peak.Value - average) / average >= PeakAboveAverage)
        {
            var above = (peak.Value - average) / average * 100;
            notes.Add(new TrajectoryNote(
                "capacity",
                $"{peak.MonthLabel} runs {Percent(above)} above the {horizon}-month average, "
                + "so it is the month to secure seats and staffing for first."));
        }

        var nextMonth = NextMonthNote(input, steps[0]);
        if (nextMonth is not null) notes.Add(nextMonth);

        var streak = StreakNote(steps);
        if (streak is not null) notes.Add(streak);

        if (steps.Count >= 3 && trough.MonthLabel != peak.MonthLabel)
        {
            notes.Add(new TrajectoryNote(
                "trough",
                $"The quietest month is {trough.MonthLabel} at {Money(trough.Value)}, "
                + $"{Money(peak.Value - trough.Value)} below {peak.MonthLabel}."));
        }

        var overlap = OverlapNote(steps);
        if (overlap is not null) notes.Add(overlap);

        var widening = WideningNote(steps);
        if (widening is not null) notes.Add(widening);

        var benchmark = BenchmarkNote(input);
        if (benchmark is not null) notes.Add(benchmark);

        notes.Add(new TrajectoryNote("reliability", ReliabilityText(input)));

        return notes;
    }

    private static string Headline(TrajectoryInput input, TrajectoryStep peak, int horizon, bool peakStandsOut)
    {
        var movement = input.Direction switch
        {
            "up" => $"trending upward, projected {Percent(input.ChangePercent, signed: true)} over the next {horizon} months",
            "down" => $"trending downward, projected {Percent(input.ChangePercent, signed: true)} over the next {horizon} months",
            _ => $"holding steady, projected to stay roughly flat over the next {horizon} months",
        };

        return peakStandsOut
            ? $"Demand is {movement}, peaking in {peak.MonthLabel} at {Money(peak.Value)}."
            : $"Demand is {movement}, with no single month standing out.";
    }

    private static TrajectoryNote? NextMonthNote(TrajectoryInput input, TrajectoryStep first)
    {
        if (input.LastRecordedValue <= 0 || string.IsNullOrWhiteSpace(input.LastRecordedLabel)) return null;

        var change = (first.Value - input.LastRecordedValue) / input.LastRecordedValue * 100;
        if (Math.Abs(change) < MeaningfulChange)
        {
            return new TrajectoryNote(
                "next",
                $"{first.MonthLabel} lands close to {input.LastRecordedLabel}, the last month on record.");
        }

        var word = change > 0 ? "above" : "below";
        return new TrajectoryNote(
            "next",
            $"{first.MonthLabel} comes in {Percent(change)} {word} {input.LastRecordedLabel}, the last month on record.");
    }

    private static TrajectoryNote? StreakNote(IReadOnlyList<TrajectoryStep> steps)
    {
        var rising = 0;
        while (rising + 1 < steps.Count && steps[rising + 1].Value > steps[rising].Value) rising++;
        if (rising < 2) return null;

        var turns = rising + 1 < steps.Count ? steps[rising + 1].MonthLabel : null;
        var climb = $"Revenue climbs for {rising} months running, through {steps[rising].MonthLabel}";
        return new TrajectoryNote("momentum", turns is null ? climb + "." : $"{climb}, before easing in {turns}.");
    }

    private static TrajectoryNote? OverlapNote(IReadOnlyList<TrajectoryStep> steps)
    {
        for (int i = 0; i + 1 < steps.Count; i++)
        {
            var a = steps[i];
            var b = steps[i + 1];
            var gap = Math.Abs(b.Value - a.Value);
            var narrower = Math.Min(a.Upper - a.Value, b.Value - b.Lower);

            if (narrower > 0 && gap < narrower)
            {
                return new TrajectoryNote(
                    "overlap",
                    $"{a.MonthLabel} and {b.MonthLabel} are too close to separate with confidence — "
                    + "plan them as one stretch rather than ranking them.");
            }
        }

        return null;
    }

    private static TrajectoryNote? WideningNote(IReadOnlyList<TrajectoryStep> steps)
    {
        if (steps.Count < 3) return null;

        var first = steps[0].Upper - steps[0].Lower;
        var last = steps[^1].Upper - steps[^1].Lower;
        if (first <= 0 || last / first < WideningRatio) return null;

        return new TrajectoryNote(
            "spread",
            $"The range widens the further out you look — {steps[^1].MonthLabel} spans "
            + $"{Math.Round(last / first, 1).ToString(CultureInfo.InvariantCulture)} times as much as {steps[0].MonthLabel}. "
            + "Treat the near months as figures to plan against and the far ones as direction.");
    }

    private static TrajectoryNote? BenchmarkNote(TrajectoryInput input)
    {
        if (input.Mae <= 0 || input.SeasonalNaiveMae <= 0) return null;
        if (input.SeasonalNaiveMae <= input.Mae) return null;

        var better = (input.SeasonalNaiveMae - input.Mae) / input.SeasonalNaiveMae * 100;
        if (better < 5) return null;

        return new TrajectoryNote(
            "benchmark",
            $"On recorded months this forecast missed by {Money(input.Mae)} on average, "
            + $"{Percent(better)} closer than assuming each month repeats the same month last year.");
    }

    private static string ReliabilityText(TrajectoryInput input)
    {
        var typical = Percent(input.Mape);

        if (input.Mape >= UnreliableMape)
        {
            return $"Typical monthly error is {typical}, wide enough that these figures are best read "
                + "as a range rather than a target.";
        }

        if (input.Mape <= ReliableMape)
        {
            return $"Typical monthly error is {typical}, so figures have stayed close on recorded months.";
        }

        return $"Typical monthly error is {typical} — close enough to plan around, loose enough to leave room.";
    }

    // A statement built from an unusable figure is worse than no statement, so the whole set is
    // withheld rather than printing "NaN" or an unlabelled month into a sentence.
    private static bool Usable(TrajectoryInput input) =>
        double.IsFinite(input.Mape)
        && double.IsFinite(input.Mae)
        && double.IsFinite(input.SeasonalNaiveMae)
        && double.IsFinite(input.ChangePercent)
        && double.IsFinite(input.LastRecordedValue)
        && input.Steps.All(s =>
            double.IsFinite(s.Value)
            && double.IsFinite(s.Lower)
            && double.IsFinite(s.Upper)
            && !string.IsNullOrWhiteSpace(s.MonthLabel));

    private static string Money(double value)
    {
        var rounded = Math.Round(value);
        var sign = rounded < 0 ? "-" : "";
        return sign + "₱" + Math.Abs(rounded).ToString("N0", CultureInfo.InvariantCulture);
    }

    private static string Percent(double value, bool signed = false)
    {
        var rounded = Math.Round(signed ? value : Math.Abs(value), 1);
        var sign = signed && rounded > 0 ? "+" : "";
        return sign + rounded.ToString("0.#", CultureInfo.InvariantCulture) + "%";
    }
}
