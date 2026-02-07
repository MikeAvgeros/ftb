namespace Trading.Bot.Extensions.IndicatorExtensions;

public static partial class Indicator
{
    public static PairsIndicatorResult[] CalcHedgeZScore(this Candle[] pairA, Candle[] pairB, int window = 50,
        decimal maxSpread = 0.0004m, int tradeRisk = 10)
    {
        if (pairA.Length != pairB.Length) throw new ArgumentException("Pairs must have the same length.");

        var pairAPrices = pairA.Select(c => (double)c.Mid_C).ToArray();

        var pairBPrices = pairB.Select(c => (double)c.Mid_C).ToArray();

        var length = pairA.Length;

        var result = new PairsIndicatorResult[length];

        for (var i = 0; i < length; i++)
        {
            result[i] ??= new PairsIndicatorResult();

            result[i].CandleA = pairA[i];

            result[i].CandleB = pairB[i];

            if (i < window) continue;

            if (pairA[i].Spread > maxSpread || pairB[i].Spread > maxSpread) continue;

            var pairAHistory = pairAPrices.Take(i).TakeLast(window).ToArray();

            var pairBHistory = pairBPrices.Take(i).TakeLast(window).ToArray();

            var beta = Math.Clamp(pairAHistory.CalcBeta(pairBHistory), 0.5, 1.2);

            var spreadHistory = new double[window];

            for (var y = 0; y < window; y++)
            {
                spreadHistory[y] = pairAHistory[y] - beta * pairBHistory[y];
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
            
            result[i].Beta = (decimal)beta;
        }

        return result;
    }
}
