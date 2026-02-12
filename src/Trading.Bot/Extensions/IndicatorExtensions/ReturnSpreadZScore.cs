namespace Trading.Bot.Extensions.IndicatorExtensions;

public static partial class Indicator
{
    public static PairsIndicatorResult[] CalcReturnSpreadZScore(this Candle[] pairA, Candle[] pairB,
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
            
            var beta = returnAHistory.CalcBeta(returnBHistory);

            var spreadHistory = new double[window];

            for (var y = 0; y < window; y++)
            {
                spreadHistory[y] = returnAHistory[y] - beta * returnBHistory[y];
            }

            var zScore = spreadHistory.CalcZScore();

            result[i].Signal = zScore switch
            {
                < -EntryZ => Signal.Buy,
                > EntryZ => Signal.Sell,
                _ => Signal.None
            };

            result[i].TakeProfit = Math.Abs(zScore) < ExitZ;

            result[i].StopLoss = Math.Abs(zScore) > StopZ;
            
            result[i].Beta = (decimal)Math.Clamp(beta, 0.8, 1.2);
        }

        return result;
    }
}
