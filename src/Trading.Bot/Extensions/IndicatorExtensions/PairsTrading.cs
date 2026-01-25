namespace Trading.Bot.Extensions.IndicatorExtensions;

public static partial class Indicator
{
    private const double EntryZ = 2.0;
    private const double ExitZ = 0.25;
    private const double StopZ = 3.5;

    public static PairsIndicatorResult[] CalcPairsTrading(this Candle[] pairA, Candle[] pairB, int window = 200, 
        int tradeRisk = 10, decimal maxSpread = 0.0004m)
    {
        if (pairA.Length != pairB.Length) throw new ArgumentException("Pairs must have the same length.");
        
        var logPricesA = pairA.Select(c => Math.Log((double)c.Mid_C)).ToArray();
        
        var logPricesB = pairB.Select(c => Math.Log((double)c.Mid_C)).ToArray();
        
        var length = pairA.Length;

        var result = new PairsIndicatorResult[length];

        for (var i = 0; i < length; i++)
        {
            result[i] ??= new PairsIndicatorResult();
            
            result[i].CandleA = pairA[i];
            
            result[i].CandleB = pairB[i];
            
            if (i < window) continue;

            if (pairA[i].Spread > maxSpread || pairB[i].Spread > maxSpread) continue;

            var pricesA = logPricesA.Take(i).TakeLast(window).ToArray();

            var pricesB = logPricesB.Take(i).TakeLast(window).ToArray();

            var hedgeRatio = CalcHedgeRatio(pricesA, pricesB);

            var spreads = pricesA.Select((p, y) => 
                CalcSpread(p, pricesB[y], hedgeRatio)).ToArray();

            var zScore = CalcZScore(spreads,spreads.Last());

            result[i].Signal = zScore switch
            {
                >= EntryZ => Signal.Sell,
                <= -EntryZ => Signal.Buy,
                _ => Signal.None
            };

            result[i].TakeProfit = Math.Abs(zScore) <= ExitZ;
            
            result[i].StopLoss = Math.Abs(zScore) >= StopZ;

            if (result[i].Signal is Signal.None) continue;
            
            var spreadStd = spreads.CalcStdDev();

            var worstSpreadMove = Math.Abs(StopZ - EntryZ) * spreadStd;

            var unitsA = tradeRisk / ((double)pairA.Last().Mid_C * worstSpreadMove);

            var unitsB = unitsA * hedgeRatio;
            
            result[i].UnitsA = (decimal)unitsA;
            
            result[i].UnitsB = (decimal)unitsB;
        }

        return result;
    }

    private static double CalcHedgeRatio(double[] sequenceA, double[] sequenceB)
    {
        var averageA = sequenceA.Average();
        
        var averageB = sequenceB.Average();

        double numerator = 0;
        
        double denominator = 0;
        
        var length = sequenceA.Length;

        for (var i = 0; i < length; i++)
        {
            numerator += (sequenceA[i] - averageA) * (sequenceB[i] - averageB);

            denominator += (sequenceB[i] - averageB) * (sequenceB[i] - averageB);
        }

        return denominator == 0 ? 1.0 : numerator / denominator;
    }

    private static double CalcSpread(double valueA, double valueB, double hedgeRatio)
    {
        return valueA - hedgeRatio * valueB;
    }

    private static double CalcZScore(double[] sequence, double currentValue)
    {
        var average = sequence.Average();

        var std = sequence.CalcStdDev();

        return std == 0 ? 0.0 : (currentValue - average) / std;
    }
}
