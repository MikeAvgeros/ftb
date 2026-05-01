namespace Trading.Bot.Extensions.IndicatorExtensions;

public static partial class Indicator
{
    public static IndicatorResult[] CalcZScorePullBack(this Candle[] candles, int zScoreWindow = 20, 
        int trendWindow = 50, decimal maxSpread = 0.0004m, decimal minGain = 0.0006m, decimal riskReward = 1)
    {
        var prices = candles.Select(c => (double)c.Mid_C).ToArray();
        
        var emaResult = prices.CalcEma(trendWindow).ToArray();
        
        var length = candles.Length;

        var result = new IndicatorResult[length];

        for (var i = 0; i < length; i++)
        {
            result[i] ??= new IndicatorResult();

            result[i].Candle = candles[i];
            
            if (i < zScoreWindow) continue;

            var trend = prices[i] > emaResult[i] ? 1 : prices[i] < emaResult[i] ? -1 : 0;
            
            var emaDiff = emaResult[i] - emaResult[i - trendWindow / 10];

            var isTrendingUp = emaDiff > 0.0001;
            
            var isTrendingDown = emaDiff < -0.0001;
            
            var pricesHistory = prices.Take(i).TakeLast(zScoreWindow).ToArray();
            
            var zScore = pricesHistory.CalcZScore();
            
            result[i].Gain = Math.Abs(candles[i].Mid_C - (decimal)emaResult[i]);
            
            result[i].Signal = zScore switch
            {
                < -2 when trend > 0 && isTrendingUp && 
                candles[i].Spread <= maxSpread &&
                result[i].Gain >= minGain => Signal.Buy,
                > 2 when trend < 0 && isTrendingDown && 
                candles[i].Spread <= maxSpread &&
                result[i].Gain >= minGain => Signal.Sell,
                _ => Signal.None
            };
            
            result[i].TakeProfit = candles[i].CalcTakeProfit(result[i], riskReward);

            result[i].StopLoss = candles[i].CalcStopLoss(result[i]);

            result[i].Loss = Math.Abs(candles[i].Mid_C - result[i].StopLoss);
        }
        
        return result;
    }
}