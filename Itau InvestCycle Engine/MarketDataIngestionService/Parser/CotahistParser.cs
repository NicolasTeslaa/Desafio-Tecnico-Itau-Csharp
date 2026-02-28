using ClassLibrary.Contracts.DTOs;
using System.Globalization;
using System.Text;

namespace MarketDataIngestionService.Parser;

public sealed class CotahistParser
{
    private const int DetailRecordLength = 245;
    private static readonly HashSet<string> AllowedBdiCodes = ["02", "96"];
    private static readonly HashSet<string> AllowedMarketTypes = ["010", "020"];

    private static readonly Encoding FileEncoding =
        Encoding.GetEncoding("ISO-8859-1");

    static CotahistParser()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
    }

    public IEnumerable<CotahistPriceRecord> ParseStream(Stream stream)
    {
        using var reader = new StreamReader(stream, FileEncoding, detectEncodingFromByteOrderMarks: true, bufferSize: 1024 * 64, leaveOpen: true);

        string? line;
        while ((line = reader.ReadLine()) is not null)
        {
            if (string.IsNullOrWhiteSpace(line)) continue;

            if (line.Length != DetailRecordLength) continue;

            var tipreg = Sub(line, 1, 2);
            if (tipreg != "01") continue;

            var codBdi = Sub(line, 11, 12).Trim();
            if (!AllowedBdiCodes.Contains(codBdi)) continue;

            var marketType = Sub(line, 25, 27).Trim();
            if (!AllowedMarketTypes.Contains(marketType)) continue;

            var tradeDate = ParseDate(Sub(line, 3, 10));
            var symbol = Sub(line, 13, 24).Trim();

            var open = ParseImplied2(Sub(line, 57, 69));
            var high = ParseImplied2(Sub(line, 70, 82));
            var low = ParseImplied2(Sub(line, 83, 95));
            var close = ParseImplied2(Sub(line, 109, 121));
            var vol = ParseImplied2(Sub(line, 171, 188));

            if (string.IsNullOrWhiteSpace(symbol)) continue;

            yield return new CotahistPriceRecord(
                Symbol: symbol,
                TradeDate: tradeDate,
                Open: open,
                High: high,
                Low: low,
                Close: close,
                Volume: vol
            );
        }
    }

    private static string Sub(string line, int start1Based, int end1Based)
    {
        int start = start1Based - 1;
        int len = end1Based - start1Based + 1;

        if (start < 0) return "";
        if (line.Length < start + len)
        {
            if (line.Length <= start) return "";
            return line.Substring(start);
        }

        return line.Substring(start, len);
    }

    private static DateOnly ParseDate(string yyyymmdd)
    {
        yyyymmdd = yyyymmdd.Trim();
        var dt = DateTime.ParseExact(yyyymmdd, "yyyyMMdd", CultureInfo.InvariantCulture);
        return DateOnly.FromDateTime(dt);
    }

    private static decimal ParseImplied2(string numeric)
    {
        numeric = numeric.Trim();
        if (numeric.Length == 0) return 0m;

        for (int i = 0; i < numeric.Length; i++)
            if (numeric[i] < '0' || numeric[i] > '9')
                throw new FormatException($"Invalid numeric field: '{numeric}'");

        if (!long.TryParse(numeric, NumberStyles.None, CultureInfo.InvariantCulture, out var raw))
            throw new FormatException($"Invalid numeric field: '{numeric}'");

        return raw / 100m;
    }
}
