namespace Trading.Bot.Extensions.IndicatorExtensions;

public static partial class Indicator
{
    private const double EntryZ = 2.0;
    private const double ExitZ = 0.25;
    private const double StopZ = 3.5;

    public static PairsIndicatorResult CalcPairsTrading(this Candle[] pairA, Candle[] pairB, int window = 200, int tradeRisk = 10)
    {
        var pricesA = pairA.Select(c => (double)c.Mid_C).ToArray();

        var pricesB = pairB.Select(c => (double)c.Mid_C).ToArray();

        var hedgeRatio = pricesA.CalcHedgeRatio(pricesB);

        var spreads = pricesA.Select((p, i) => p.CalcSpread(pricesB[i], hedgeRatio));

        var spreadHistory = spreads.TakeLast(window).ToArray();

        var zScore = spreadHistory.CalcZScore(spreads.Last());

        var spreadStd = spreadHistory.CalcStdDev();

        var worstSpreadMove = Math.Abs(StopZ - EntryZ) * spreadStd;

        var unitsA = Math.Floor(tradeRisk / worstSpreadMove);

        var unitsB = Math.Floor(unitsA * hedgeRatio);

        var result = new PairsIndicatorResult
        {
            Signal = zScore switch
            {
                > EntryZ => Signal.Sell,
                < -EntryZ => Signal.Buy,
                _ => Signal.None
            },
            Exit = Math.Abs(zScore) < ExitZ || Math.Abs(zScore) > StopZ,
            UnitsA = (decimal)unitsA,
            UnitsB = (decimal)unitsB
        };

        return result;
    }
}
