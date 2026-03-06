using System.Text;
using MarketDataIngestionService.Parser;

namespace MarketDataIngestionService.Tests;

public sealed class CotahistParserTests
{
    [Fact]
    public void ParseStream_ParsesValidDetailRecord()
    {
        var parser = new CotahistParser();
        using var stream = BuildStream(
            BuildDetailLine(
                tradeDate: "20260305",
                bdiCode: "02",
                symbol: "PETR4",
                marketType: "010",
                open: 35.12m,
                high: 36.45m,
                low: 34.98m,
                close: 35.90m,
                volume: 1234567.89m));

        var result = parser.ParseStream(stream).Single();

        Assert.Equal("PETR4", result.Symbol);
        Assert.Equal(new DateOnly(2026, 3, 5), result.TradeDate);
        Assert.Equal(35.12m, result.Open);
        Assert.Equal(36.45m, result.High);
        Assert.Equal(34.98m, result.Low);
        Assert.Equal(35.90m, result.Close);
        Assert.Equal(1234567.89m, result.Volume);
    }

    [Fact]
    public void ParseStream_IgnoresRecordsOutsideAllowedFilters()
    {
        var parser = new CotahistParser();
        using var stream = BuildStream(
            BuildDetailLine(tradeDate: "20260305", bdiCode: "02", symbol: "PETR4", marketType: "010", open: 10m, high: 11m, low: 9m, close: 10.5m, volume: 100m),
            BuildDetailLine(tradeDate: "20260305", bdiCode: "10", symbol: "VALE3", marketType: "010", open: 20m, high: 21m, low: 19m, close: 20.5m, volume: 200m),
            BuildDetailLine(tradeDate: "20260305", bdiCode: "02", symbol: "ITUB4", marketType: "030", open: 30m, high: 31m, low: 29m, close: 30.5m, volume: 300m));

        var result = parser.ParseStream(stream).ToList();

        Assert.Single(result);
        Assert.Equal("PETR4", result[0].Symbol);
    }

    private static MemoryStream BuildStream(params string[] lines)
    {
        var content = string.Join(Environment.NewLine, lines);
        return new MemoryStream(Encoding.Latin1.GetBytes(content));
    }

    private static string BuildDetailLine(
        string tradeDate,
        string bdiCode,
        string symbol,
        string marketType,
        decimal open,
        decimal high,
        decimal low,
        decimal close,
        decimal volume)
    {
        var chars = Enumerable.Repeat(' ', 245).ToArray();

        Write(chars, 1, 2, "01");
        Write(chars, 3, 10, tradeDate);
        Write(chars, 11, 12, bdiCode);
        Write(chars, 13, 24, symbol.PadRight(12));
        Write(chars, 25, 27, marketType);
        Write(chars, 57, 69, ToImplied2(open, 13));
        Write(chars, 70, 82, ToImplied2(high, 13));
        Write(chars, 83, 95, ToImplied2(low, 13));
        Write(chars, 109, 121, ToImplied2(close, 13));
        Write(chars, 171, 188, ToImplied2(volume, 18));

        return new string(chars);
    }

    private static string ToImplied2(decimal value, int width)
        => ((long)(value * 100m)).ToString().PadLeft(width, '0');

    private static void Write(char[] buffer, int start1Based, int end1Based, string value)
    {
        for (var i = 0; i < value.Length && start1Based - 1 + i <= end1Based - 1; i++)
        {
            buffer[start1Based - 1 + i] = value[i];
        }
    }
}
