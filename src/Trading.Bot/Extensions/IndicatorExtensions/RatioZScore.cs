namespace Trading.Bot.Extensions.IndicatorExtensions;

public static partial class Indicator
{
    public static PairsIndicatorResult[] CalcRatioZScore(this Candle[] pairA, Candle[] pairB,
        int window = 50, decimal maxSpread = 0.0004m, decimal baseUnits = 5000m)
    {
        if (pairA.Length != pairB.Length) throw new ArgumentException("Pairs must have the same length.");

        var length = pairA.Length;

        var ratios = new double[length];

        for (var i = 0; i < length; i++)
        {
            ratios[i] = (double)(pairA[i].Mid_C / pairB[i].Mid_C);
        }

        var result = new PairsIndicatorResult[length];

        for (var i = 0; i < length; i++)
        {
            result[i] ??= new PairsIndicatorResult();

            result[i].CandleA = pairA[i];

            result[i].CandleB = pairB[i];

            if (i < window) continue;

            if (pairA[i].Spread > maxSpread || pairB[i].Spread > maxSpread) continue;

            var ratioHistory = ratios.Take(i + 1).TakeLast(window).ToArray().Winsorize();

            var zScore = ratioHistory.CalcWinsorizedZScore();

            result[i].ZScore = zScore;

            result[i].Signal = zScore switch
            {
                < -EntryZ => Signal.Buy,
                > EntryZ => Signal.Sell,
                _ => Signal.None
            };

            result[i].TakeProfit = Math.Abs(zScore) < ExitZ;

            result[i].StopLoss = Math.Abs(zScore) > StopZ;

            var averageRatio = ratioHistory.Average();

            result[i].Beta = averageRatio > 0 ? (decimal)averageRatio : 1m;

            result[i].UnitsA = baseUnits;

            result[i].UnitsB = Math.Round(baseUnits * result[i].Beta, 0);
        }

        return result;
    }
}
