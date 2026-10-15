using ProphetOps.Forecasting;
using Xunit;

namespace ProphetOps.Forecasting.Tests;

public class MonthlySeriesCsvTests
{
    [Fact]
    public void Reads_months_and_values_in_any_order_after_a_header()
    {
        MonthlySeries series = MonthlySeriesCsv.Parse("month,value\n2024-03,300\n2024-01,100\n2024-02,200\n");

        Assert.Equal(new DateOnly(2024, 1, 1), series.FirstMonth);
        Assert.Equal(new DateOnly(2024, 3, 1), series.LastMonth);
        Assert.Equal(new double[] { 100, 200, 300 }, series.Values);
    }

    [Fact]
    public void Reads_the_usual_written_forms_of_months_and_amounts()
    {
        MonthlySeries series = MonthlySeriesCsv.Parse(
            "﻿Month,Revenue (PHP)\r\n" +
            "Jan 2024,\"₱1,234.50\"\r\n" +
            "2024-02-15,PHP 2000\r\n" +
            "# a comment line\r\n" +
            "March 2024,0\r\n");

        Assert.Equal(new double[] { 1234.50, 2000, 0 }, series.Values);
        Assert.Equal(new DateOnly(2024, 1, 1), series.FirstMonth);
    }

    [Fact]
    public void Reads_tab_separated_rows()
    {
        MonthlySeries series = MonthlySeriesCsv.Parse("2025-11\t5\n2025-12\t7\n");

        Assert.Equal(new double[] { 5, 7 }, series.Values);
        Assert.Equal(new DateOnly(2025, 11, 1), series.FirstMonth);
    }

    [Fact]
    public void A_missing_month_is_reported_not_filled_in()
    {
        var error = Assert.Throws<FormatException>(() => MonthlySeriesCsv.Parse("2024-01,1\n2024-02,2\n2024-05,5\n"));

        Assert.Contains("No row for 2024-03", error.Message);
        Assert.Contains("No row for 2024-04", error.Message);
    }

    [Fact]
    public void A_month_given_twice_is_reported_with_its_lines()
    {
        var error = Assert.Throws<FormatException>(() => MonthlySeriesCsv.Parse("2024-01,1\n2024-02,2\n2024-02,3\n"));

        Assert.Contains("2024-02 appears more than once (lines 2, 3)", error.Message);
    }

    [Fact]
    public void An_unquoted_number_with_commas_is_refused_instead_of_misread()
    {
        var error = Assert.Throws<FormatException>(() => MonthlySeriesCsv.Parse("2024-01,1,234\n"));

        Assert.Contains("Line 1: more than two columns", error.Message);
    }

    [Theory]
    [InlineData("2024-01,-5\n", "\"-5\" is not a number of zero or more")]
    [InlineData("2024-01,abc\n", "\"abc\" is not a number of zero or more")]
    [InlineData("month,value\n13-2024,5\n", "\"13-2024\" is not a month")]
    [InlineData("2024-01\n", "expected a month and a value")]
    [InlineData("month,value\n", "no months in it")]
    public void Unreadable_rows_are_named(string csv, string expected)
    {
        var error = Assert.Throws<FormatException>(() => MonthlySeriesCsv.Parse(csv));

        Assert.Contains(expected, error.Message);
    }
}
