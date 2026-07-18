namespace Trading.Bot.Extensions.IndicatorExtensions;

public static partial class Indicator
{
    public static IndicatorResult[] CalcIntradayMeanReversion(this Candle[] candles, int window = 20,
        double stdDev = 2, int rsiWindow = 14, double rsiLower = 30, double rsiUpper = 70, int stochWindow = 14,
        double stochLower = 20, double stochUpper = 80, int atrWindow = 14, double atrMultiplier = 1.5,
        double volatilityRegimeMax = 1.4, decimal maxSpread = 0.0004m, decimal minGain = 0.0006m, decimal riskReward = 1m)
    {
        var length = candles.Length;

        var result = new IndicatorResult[length];

        if (length == 0)
        {
            return result;
        }

        var bollingerBands = candles.CalcBollingerBands(window, stdDev);

        var rsiResult = candles.CalcRsi(rsiWindow);

        var stochastic = candles.CalcStochastic(stochWindow, 3);

        var atrResult = candles.CalcAtr(atrWindow);

        Span<double> atrValues = length <= MaxStackAlloc ? stackalloc double[length] : new double[length];

        for (var i = 0; i < length; i++)
        {
            atrValues[i] = atrResult[i].Atr;
        }

        var atrBaseline = atrValues.CalcSma(atrWindow * 2);

        var warmup = Math.Max(window, Math.Max(rsiWindow, Math.Max(stochWindow + 6, atrWindow * 2))) + 1;

        for (var i = 0; i < length; i++)
        {
            result[i] ??= new IndicatorResult();

            result[i].Candle = candles[i];

            if (i < warmup) continue;

            var lowerBand = (decimal)bollingerBands[i].LowerBand;

            var upperBand = (decimal)bollingerBands[i].UpperBand;

            var prevLowerBand = (decimal)bollingerBands[i - 1].LowerBand;

            var prevUpperBand = (decimal)bollingerBands[i - 1].UpperBand;
            
            var reversalUp = candles[i - 1].Mid_C < prevLowerBand && candles[i].Mid_C >= lowerBand;

            var reversalDown = candles[i - 1].Mid_C > prevUpperBand && candles[i].Mid_C <= upperBand;
            
            var atrBaseValue = atrBaseline[i];

            var isRangingRegime = atrBaseValue > 0 && atrResult[i].Atr / atrBaseValue <= volatilityRegimeMax;

            result[i].Gain = (decimal)atrResult[i].Atr * (decimal)atrMultiplier;

            result[i].Signal = (reversalUp, reversalDown) switch
            {
                (true, _) when rsiResult[i].Rsi < rsiLower && stochastic[i].KOscillator < stochLower &&
                               isRangingRegime && candles[i].Spread <= maxSpread && result[i].Gain >= minGain => Signal.Buy,
                (_, true) when rsiResult[i].Rsi > rsiUpper && stochastic[i].KOscillator > stochUpper &&
                               isRangingRegime && candles[i].Spread <= maxSpread && result[i].Gain >= minGain => Signal.Sell,
                _ => Signal.None
            };

            result[i].TakeProfit = candles[i].CalcTakeProfit(result[i], riskReward);

            result[i].StopLoss = candles[i].CalcStopLoss(result[i]);

            result[i].Loss = Math.Abs(candles[i].Mid_C - result[i].StopLoss);
        }

        return result;
    }
}
