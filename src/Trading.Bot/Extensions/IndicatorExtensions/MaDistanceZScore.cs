namespace Trading.Bot.Extensions.IndicatorExtensions;

public static partial class Indicator
{
    public static PairsIndicatorResult[] CalcMaDistanceZScore(this Candle[] pairA, Candle[] pairB,
        int window = 50, decimal maxSpread = 0.0004m)
    {
        if (pairA.Length != pairB.Length) throw new ArgumentException("Pairs must have the same length.");

        var logA = pairA.Select(c => (double)c.Mid_C).ToArray();

        var logB = pairB.Select(c => (double)c.Mid_C).ToArray();

        var length = pairA.Length;

        var result = new PairsIndicatorResult[length];

        for (var i = 0; i < length; i++)
        {
            result[i] ??= new PairsIndicatorResult();

            result[i].CandleA = pairA[i];

            result[i].CandleB = pairB[i];

            if (i < window) continue;

            if (pairA[i].Spread > maxSpread || pairB[i].Spread > maxSpread) continue;

            var logAHistory = logA.Take(i).TakeLast(window).ToArray();

            var logBHistory = logB.Take(i).TakeLast(window).ToArray();

            var da = logAHistory.Select(v => v - logAHistory.Average()).ToArray();

            var db = logBHistory.Select(v => v - logBHistory.Average()).ToArray();

            var diff = da.Zip(db, (a, b) => a - b).ToArray();

            var mean = diff.Average();

            var std = diff.CalcStdDev();

            var zScore = (diff.Last() - mean) / std;

            result[i].Signal = zScore switch
            {
                < -EntryZ => Signal.Buy,
                > EntryZ => Signal.Sell,
                _ => Signal.None
            };

            result[i].TakeProfit = Math.Abs(zScore) < ExitZ;

            result[i].StopLoss = Math.Abs(zScore) > StopZ;
        }

        return result;
    }
}
