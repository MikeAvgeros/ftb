namespace Trading.Bot.Extensions.IndicatorExtensions;

public static partial class Indicator
{
    public static PairsIndicatorResult[] CalcHedgeZScore(this Candle[] pairA, Candle[] pairB, int window = 50,
        decimal maxSpread = 0.0004m, int tradeRisk = 10, decimal baseUnits = 5000m)
    {
        if (pairA.Length != pairB.Length) throw new ArgumentException("Pairs must have the same length.");

        var pairAPrices = pairA.Select(c => (double)c.Mid_C).ToArray();

        var pairBPrices = pairB.Select(c => (double)c.Mid_C).ToArray();

        var totalVolume = pairA.Select((c, idx) => (double)(c.Volume + pairB[idx].Volume)).ToArray();

        var length = pairA.Length;

        var result = new PairsIndicatorResult[length];

        for (var i = 0; i < length; i++)
        {
            result[i] ??= new PairsIndicatorResult();

            result[i].CandleA = pairA[i];

            result[i].CandleB = pairB[i];

            if (i < window) continue;

            if (pairA[i].Spread > maxSpread || pairB[i].Spread > maxSpread) continue;

            var pairAHistory = pairAPrices.Take(i + 1).TakeLast(window).ToArray();

            var pairBHistory = pairBPrices.Take(i + 1).TakeLast(window).ToArray();

            var beta = pairAHistory.Winsorize().CalcBeta(pairBHistory.Winsorize());

            var spreadHistory = new double[window];

            for (var y = 0; y < window; y++)
            {
                spreadHistory[y] = pairAHistory[y] - beta * pairBHistory[y];
            }

            var zScore = spreadHistory.CalcWinsorizedZScore();

            result[i].ZScore = zScore;

            var reversionSignal = zScore switch
            {
                < -EntryZ => Signal.Buy,
                > EntryZ => Signal.Sell,
                _ => Signal.None
            };

            var volumeHistory = totalVolume.Take(i + 1).TakeLast(window).ToArray();

            var regime = ClassifySpreadRegime(zScore, spreadHistory, volumeHistory);

            result[i].Regime = regime;

            result[i].ReversionSignal = reversionSignal;

            result[i].Signal = ResolveRegimeSignal(regime, reversionSignal);

            result[i].TakeProfit = Math.Abs(zScore) < ExitZ;

            result[i].StopLoss = Math.Abs(zScore) > StopZ;
            
            result[i].Beta = (decimal)Math.Clamp(beta, 0.8, 1.2);

            result[i].UnitsA = baseUnits;

            result[i].UnitsB = Math.Round(baseUnits * result[i].Beta, 0);
        }

        return result;
    }
}
