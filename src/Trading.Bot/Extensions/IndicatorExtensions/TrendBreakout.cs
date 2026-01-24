namespace Trading.Bot.Extensions.IndicatorExtensions;

public static partial class Indicator
{
    public static IndicatorResult[] CalcTrendBreakout(this Candle[] candles,
        int window = 20, decimal maxSpread = 0.0004m, decimal riskReward = 1.5m)
    {
        var donchianChannel = candles.CalcDonchianChannel(window);

        var atr = candles.CalcAtr();

        var length = candles.Length;

        var result = new IndicatorResult[length];

        for (var i = 0; i < length; i++)
        {
            result[i] ??= new IndicatorResult();

            result[i].Candle = candles[i];

            result[i].Signal = Signal.None;

            if (i < window) continue;

            if (candles[i].Spread > maxSpread) continue;

            var bullishBreakout =
                candles[i].Mid_C > donchianChannel[i].UpperBand &&
                donchianChannel[i].MidBand > donchianChannel[i - 5].MidBand &&
                candles.TakeLast(5).All(c => c.Mid_C > donchianChannel[i].MidBand);

            var bearishBreakout =
                candles[i].Mid_C < donchianChannel[i].LowerBand &&
                donchianChannel[i].MidBand < donchianChannel[i - 5].MidBand &&
                candles.TakeLast(5).All(c => c.Mid_C < donchianChannel[i].MidBand);

            var rangingMarket =
                (double)(donchianChannel[i].UpperBand - donchianChannel[i].LowerBand) < atr[i].Atr * 1.25 && // small channel width
                (double)Math.Abs(donchianChannel[i].MidBand - donchianChannel[i - 10].MidBand) < atr[i].Atr * 0.25; //midline is flat

            if (bullishBreakout && !rangingMarket)
            {
                result[i].Signal = Signal.Buy;
            }

            if (bearishBreakout && !rangingMarket)
            {
                result[i].Signal = Signal.Sell;
            }

            result[i].Gain = result[i].Signal switch
            {
                Signal.Buy => Math.Abs(candles[i].Mid_C - donchianChannel[i].LowerBand),
                Signal.Sell => Math.Abs(candles[i].Mid_C - donchianChannel[i].UpperBand),
                _ => candles[i].Mid_C
            };

            result[i].TakeProfit = candles[i].CalcTakeProfit(result[i], riskReward);

            result[i].StopLoss = candles[i].CalcStopLoss(result[i]);

            result[i].Loss = Math.Abs(candles[i].Mid_C - result[i].StopLoss);
        }

        return result;
    }
}