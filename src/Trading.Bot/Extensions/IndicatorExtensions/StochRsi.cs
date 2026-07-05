namespace Trading.Bot.Extensions.IndicatorExtensions;

public static partial class Indicator
{
    private static StochasticResult[] CalcStochRsi(this Candle[] candles, int rsiWindow = 14, int stochWindow = 14, int smoothK = 3, int smoothD = 3)
    {
        var rsiResult = candles.CalcRsi(rsiWindow);
        
        var length = rsiResult.Length;
        
        var result = new StochasticResult[length];

        if (length == 0)
        {
            return result;
        }
        
        Span<double> rsi = length <= MaxStackAlloc ? stackalloc double[length] : new double[length];
        
        Span<int> maxDeque = length <= MaxStackAlloc ? stackalloc int[length] : new int[length];
        
        Span<int> minDeque = length <= MaxStackAlloc ? stackalloc int[length] : new int[length];

        for (var i = 0; i < length; i++)
        {
            rsi[i] = rsiResult[i].Rsi;
        }

        var maxHead = 0; var maxTail = 0;
        
        var minHead = 0; var minTail = 0;
        
        var rsiWarmupIndex = rsiWindow - 1; 

        for (var i = 0; i < length; i++)
        {
            result[i] = new StochasticResult { Candle = candles[i] };
            
            var value = rsi[i];
            
            if (i < rsiWarmupIndex)
            {
                result[i].KOscillator = 0.0;
                
                continue;
            }
            
            while (maxTail > maxHead && rsi[maxDeque[maxTail - 1]] <= value)
            {
                maxTail--;
            }
            
            maxDeque[maxTail++] = i;
            
            while (minTail > minHead && rsi[minDeque[minTail - 1]] >= value)
            {
                minTail--;
            }
            
            minDeque[minTail++] = i;
            
            while (maxDeque[maxHead] <= i - stochWindow) { maxHead++; }
            
            while (minDeque[minHead] <= i - stochWindow) { minHead++; }
            
            if (i < rsiWarmupIndex + stochWindow - 1)
            {
                result[i].KOscillator = 0.0;
                
                continue;
            }

            var highestRsi = rsi[maxDeque[maxHead]];
            
            var lowestRsi = rsi[minDeque[minHead]];

            result[i].KOscillator = highestRsi - lowestRsi != 0
                ? 100.0 * (value - lowestRsi) / (highestRsi - lowestRsi)
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
                var kWarmupBoundary = rsiWarmupIndex + (stochWindow - 1) + (smoothK - 1);
                
                result[i].KOscillator = i < kWarmupBoundary ? 0.0 : smaK[i];
            }
        }
        
        Span<double> oscillators = length <= MaxStackAlloc ? stackalloc double[length] : new double[length];
        
        for (var i = 0; i < length; i++)
        {
            oscillators[i] = result[i].KOscillator;
        }

        var smaD = oscillators.CalcSma(smoothD);
        
        var totalSystemWarmup = rsiWarmupIndex + (stochWindow - 1) + (smoothK - 1) + (smoothD - 1);

        for (var i = 0; i < length; i++)
        {
            if (i < totalSystemWarmup)
            {
                result[i].KOscillator = 0.0;
                
                result[i].DOscillator = 0.0;
            }
            else
            {
                result[i].DOscillator = smaD[i];
            }
        }

        return result;
    }
}