namespace Trading.Bot.Extensions.IndicatorExtensions;

public static partial class Indicator
{
    public static PairsIndicatorResult[] CalcEqualWeightedZScore(this Candle[] pairA, Candle[] pairB,
        int window = 50, decimal maxSpread = 0.0004m, decimal baseUnits = 5000m)
    {
        if (pairA.Length != pairB.Length) throw new ArgumentException("Pairs must have the same length.");

        var maDistance = pairA.CalcMaDistanceZScore(pairB, window, maxSpread, baseUnits);

        var returnSpread = pairA.CalcReturnSpreadZScore(pairB, window, maxSpread, baseUnits);

        var hedge = pairA.CalcHedgeZScore(pairB, window, maxSpread, baseUnits: baseUnits);

        var ratio = pairA.CalcRatioZScore(pairB, window, maxSpread, baseUnits);

        var kalman = pairA.CalcKalmanFilteredReturnSpread(pairB, window, maxSpread, baseUnits);

        var length = pairA.Length;

        var result = new PairsIndicatorResult[length];

        for (var i = 0; i < length; i++)
        {
            result[i] = new PairsIndicatorResult
            {
                CandleA = pairA[i],
                CandleB = pairB[i]
            };
        }
        
        var validIndices = new List<int>();

        var rawComposite = new List<double>();

        for (var i = window; i < length; i++)
        {
            if (pairA[i].Spread > maxSpread || pairB[i].Spread > maxSpread) continue;

            var equalWeighted = new[]
            {
                maDistance[i].ZScore, returnSpread[i].ZScore, hedge[i].ZScore, ratio[i].ZScore, kalman[i].ZScore
            }.CalcEqualWeightedZScore();

            validIndices.Add(i);

            rawComposite.Add(equalWeighted);
        }

        var expandingZScores = rawComposite.ToArray().CalcExpandingZScore();

        for (var v = 0; v < validIndices.Count; v++)
        {
            var i = validIndices[v];

            var zScore = expandingZScores[v];

            result[i].ZScore = zScore;

            result[i].Signal = zScore switch
            {
                < -EntryZ => Signal.Buy,
                > EntryZ => Signal.Sell,
                _ => Signal.None
            };

            result[i].TakeProfit = Math.Abs(zScore) < ExitZ;

            result[i].StopLoss = Math.Abs(zScore) > StopZ;

            var beta = new[] { maDistance[i].Beta, returnSpread[i].Beta, hedge[i].Beta, kalman[i].Beta }.Average();

            result[i].Beta = Math.Clamp(beta, 0.8m, 1.2m);

            result[i].UnitsA = baseUnits;

            result[i].UnitsB = Math.Round(baseUnits * result[i].Beta, 0);
        }

        return result;
    }
}
