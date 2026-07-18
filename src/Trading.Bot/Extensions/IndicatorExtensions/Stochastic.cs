namespace Trading.Bot.Extensions.IndicatorExtensions;

public static partial class Indicator
{
    private static StochasticResult[] CalcStochastic(this Candle[] candles, int window = 14, int smoothK = 1, int smoothD = 3)
    {
        var length = candles.Length;
        
        var result = new StochasticResult[length];
        
        if (length == 0)
        {
            return result;
        }
        
        Span<int> maxDeque = length <= MaxStackAlloc ? stackalloc int[length] : new int[length];
        
        Span<int> minDeque = length <= MaxStackAlloc ? stackalloc int[length] : new int[length];

        var maxHead = 0; var maxTail = 0;
        
        var minHead = 0; var minTail = 0;

        for (var i = 0; i < length; i++)
        {
            result[i] = new StochasticResult { Candle = candles[i] };

            var currentHigh = (double)candles[i].Mid_H;
            
            var currentLow = (double)candles[i].Mid_L;
            
            var currentClose = (double)candles[i].Mid_C;
            
            while (maxTail > maxHead && (double)candles[maxDeque[maxTail - 1]].Mid_H <= currentHigh)
            {
                maxTail--;
            }
            
            maxDeque[maxTail++] = i;
            
            while (minTail > minHead && (double)candles[minDeque[minTail - 1]].Mid_L >= currentLow)
            {
                minTail--;
            }
            
            minDeque[minTail++] = i;
            
            while (maxDeque[maxHead] <= i - window) { maxHead++; }
            
            while (minDeque[minHead] <= i - window) { minHead++; }
            
            if (i < window - 1)
            {
                result[i].KOscillator = double.NaN;
                continue;
            }

            var highestPrice = (double)candles[maxDeque[maxHead]].Mid_H;
            
            var lowestPrice = (double)candles[minDeque[minHead]].Mid_L;

            result[i].KOscillator = highestPrice - lowestPrice != 0
                ? 100.0 * (currentClose - lowestPrice) / (highestPrice - lowestPrice)
                : 50.0;
        }
        
        if (smoothK > 1)
        {
            Span<double> kOscillators = length <= MaxStackAlloc ? stackalloc double[length] : new double[length];
            
            for (var i = 0; i < length; i++)
            {
                kOscillators[i] = result[i].KOscillator;
            }

            var smaK = kOscillators.CalcSma(smoothK);

            for (var i = 0; i < length; i++)
            {
                result[i].KOscillator = i < window - 1 + (smoothK - 1) ? 0.0 : smaK[i];
            }
        }
        
        Span<double> oscillators = length <= MaxStackAlloc ? stackalloc double[length] : new double[length];
        
        for (var i = 0; i < length; i++)
        {
            oscillators[i] = result[i].KOscillator;
        }

        var smaD = oscillators.CalcSma(smoothD);

        var kWarmup = (window - 1) + (smoothK > 1 ? (smoothK - 1) : 0);

        var totalWarmup = kWarmup + (smoothD - 1);

        for (var i = 0; i < length; i++)
        {
            if (i < kWarmup)
            {
                result[i].KOscillator = 0.0;
            }

            result[i].DOscillator = i < totalWarmup ? 0.0 : smaD[i];
        }

        return result;
    }
}