namespace Trading.Bot.Extensions.IndicatorExtensions;

public static partial class Indicator
{
    public static PairsIndicatorResult[] CalcKalmanFilteredReturnSpread(this Candle[] pairA, Candle[] pairB,
        int window = 50, decimal maxSpread = 0.0004m, decimal baseUnits = 5000m)
    {
        if (pairA.Length != pairB.Length) throw new ArgumentException("Pairs must have the same length.");

        var pricesA = pairA.Select(c => (double)c.Mid_C).ToArray();

        var pricesB = pairB.Select(c => (double)c.Mid_C).ToArray();

        var returnsA = pricesA.CalcLogReturns();

        var returnsB = pricesB.CalcLogReturns();

        var returnsLength = returnsA.Length;
        
        var betaSeries = new double[returnsLength];

        var spreadSeries = new double[returnsLength];

        double beta = 0.9, variance = 0.1;

        for (var y = 0; y < returnsLength; y++)
        {
            var kalman = returnsA[y].CalcKalmanBeta(returnsB[y], beta, variance);

            beta = kalman.Beta;

            variance = kalman.Variance;

            betaSeries[y] = beta;

            spreadSeries[y] = returnsA[y] - beta * returnsB[y];
        }

        var length = pairA.Length;

        var result = new PairsIndicatorResult[length];

        for (var i = 0; i < length; i++)
        {
            result[i] ??= new PairsIndicatorResult();

            result[i].CandleA = pairA[i];

            result[i].CandleB = pairB[i];

            if (i < window) continue;

            if (pairA[i].Spread > maxSpread || pairB[i].Spread > maxSpread) continue;

            var returnAHistory = returnsA.Take(i).TakeLast(window).ToArray();

            var returnBHistory = returnsB.Take(i).TakeLast(window).ToArray();

            var correlation = returnAHistory.CalcCorrelation(returnBHistory);

            var spreadHistory = spreadSeries.Take(i).TakeLast(window).ToArray().Winsorize();

            var zScore = spreadHistory.CalcWinsorizedZScore();

            result[i].ZScore = zScore;

            result[i].Signal = zScore switch
            {
                < -EntryZ when correlation > 0.6 => Signal.Buy,
                > EntryZ when correlation > 0.6 => Signal.Sell,
                _ => Signal.None
            };

            result[i].TakeProfit = Math.Abs(zScore) < ExitZ;

            result[i].StopLoss = Math.Abs(zScore) > StopZ;

            result[i].Beta = (decimal)Math.Clamp(betaSeries[i - 1], 0.8, 1.2);

            result[i].UnitsA = baseUnits;

            result[i].UnitsB = Math.Round(baseUnits * result[i].Beta, 0);
        }

        return result;
    }
}
