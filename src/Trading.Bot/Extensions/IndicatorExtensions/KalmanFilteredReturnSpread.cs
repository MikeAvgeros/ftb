namespace Trading.Bot.Extensions.IndicatorExtensions;

public static partial class Indicator
{
    public static PairsIndicatorResult[] CalcKalmanFilteredReturnSpread(this Candle[] pairA, Candle[] pairB,
        int window = 50, decimal maxSpread = 0.0004m)
    {
        if (pairA.Length != pairB.Length) throw new ArgumentException("Pairs must have the same length.");

        var pricesA = pairA.Select(c => (double)c.Mid_C).ToArray();

        var pricesB = pairB.Select(c => (double)c.Mid_C).ToArray();

        var returnsA = pricesA.CalcLogReturns();

        var returnsB = pricesB.CalcLogReturns();

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

            var spreadHistory = new double[window];

            double beta = 0.9, variance = 0.1;

            for (var y = 0; y < window; y++)
            {
                var kalman = returnAHistory[y].CalcKalmanBeta(returnBHistory[y], beta, variance);
                spreadHistory[y] = returnAHistory[y] - kalman.Beta * returnBHistory[y];
                beta = kalman.Beta;
                variance = kalman.Variance;
            }

            var zScore = spreadHistory.CalcZScore();

            result[i].Signal = zScore switch
            {
                < -EntryZ when correlation > 0.6 => Signal.Buy,
                > EntryZ when correlation > 0.6 => Signal.Sell,
                _ => Signal.None
            };

            result[i].TakeProfit = Math.Abs(zScore) < ExitZ;

            result[i].StopLoss = Math.Abs(zScore) > StopZ;
            
            result[i].Beta = (decimal)beta;
        }

        return result;
    }
}
