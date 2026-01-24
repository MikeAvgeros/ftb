namespace Trading.Bot.Extensions.IndicatorExtensions;

public static partial class Indicator
{
    private const double EntryZ = 2.0;
    private const double ExitZ = 0.25;
    private const double StopZ = 3.5;

    public static PairsIndicatorResult CalcPairsTrading(this Candle[] pairA, Candle[] pairB, int window = 200, int tradeRisk = 10)
    {
        var pricesA = pairA.TakeLast(window).Select(c => Math.Log((double)c.Mid_C)).ToArray();

        var pricesB = pairB.TakeLast(window).Select(c => Math.Log((double)c.Mid_C)).ToArray();

        var hedgeRatio = pricesA.CalcHedgeRatio(pricesB);

        var spreads = pricesA.Select((p, i) => p.CalcSpread(pricesB[i], hedgeRatio)).ToArray();

        var zScore = spreads.CalcZScore(spreads.Last());

        var spreadStd = spreads.CalcStdDev();

        var worstSpreadMove = Math.Abs(StopZ - EntryZ) * spreadStd;

        var unitsA = tradeRisk / ((double)pairA.Last().Mid_C * worstSpreadMove);

        var unitsB = unitsA * hedgeRatio;

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
