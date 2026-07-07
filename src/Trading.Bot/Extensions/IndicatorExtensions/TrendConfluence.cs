namespace Trading.Bot.Extensions.IndicatorExtensions;

public static partial class Indicator
{
    public static IndicatorResult[] CalcTrendConfluence(this Candle[] candles, int emaFast = 9, int emaMed = 21,
        int emaSlow = 50, int atrAvgWindow = 50, double rsiOversold = 20, double rsiOverbought = 80,
        double minAtrRatio = 0.8, decimal maxSpread = 0.0004m, decimal minGain = 0.0006m, decimal riskReward = 1m)
    {
        var macd = candles.CalcMacd();

        var atr = candles.CalcAtr();

        var stochRsi = candles.CalcStochRsi();

        var prices = candles.Select(c => (double)c.Mid_C).ToArray();

        var fastEma = prices.CalcEma(emaFast);

        var medEma = prices.CalcEma(emaMed);

        var slowEma = prices.CalcEma(emaSlow);

        var atrValues = atr.Select(a => a.Atr).ToArray();

        var avgAtr = atrValues.AsSpan().CalcSma(atrAvgWindow);

        var length = candles.Length;

        var result = new IndicatorResult[length];

        var warmup = Math.Max(emaSlow, atrAvgWindow);

        for (var i = 0; i < length; i++)
        {
            result[i] ??= new IndicatorResult();

            result[i].Candle = candles[i];

            if (i < warmup)
            {
                result[i].Signal = Signal.None;

                continue;
            }
            
            var bullishTrend = fastEma[i] > medEma[i] && medEma[i] > slowEma[i];

            var bearishTrend = fastEma[i] < medEma[i] && medEma[i] < slowEma[i];
            
            var macdBullish = macd[i].Histogram > 0 && macd[i].Histogram > macd[i - 1].Histogram;

            var macdBearish = macd[i].Histogram < 0 && macd[i].Histogram < macd[i - 1].Histogram;

            var pulledBackUp = stochRsi[i].KOscillator > rsiOversold && stochRsi[i - 1].KOscillator <= rsiOversold;

            var pulledBackDown = stochRsi[i].KOscillator < rsiOverbought && stochRsi[i - 1].KOscillator >= rsiOverbought;

            var sufficientVolatility = atr[i].Atr >= avgAtr[i] * minAtrRatio;
            
            result[i].Gain = Math.Abs(candles[i].Mid_C - (decimal)slowEma[i]);

            result[i].Signal = candles[i] switch
            {
                var candle when bullishTrend && macdBullish && pulledBackUp && sufficientVolatility &&
                                candle.Direction == 1 && candle.BodyPercentage > 40 &&
                                candle.Spread <= maxSpread && result[i].Gain >= minGain => Signal.Buy,
                var candle when bearishTrend && macdBearish && pulledBackDown && sufficientVolatility &&
                                candle.Direction == -1 && candle.BodyPercentage > 40 &&
                                candle.Spread <= maxSpread && result[i].Gain >= minGain => Signal.Sell,
                _ => Signal.None
            };

            result[i].TakeProfit = candles[i].CalcTakeProfit(result[i], riskReward);

            result[i].StopLoss = candles[i].CalcStopLoss(result[i]);

            result[i].Loss = Math.Abs(candles[i].Mid_C - result[i].StopLoss);
        }

        return result;
    }
}
