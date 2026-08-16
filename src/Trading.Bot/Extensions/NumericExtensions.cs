namespace Trading.Bot.Extensions;

public static class NumericExtensions
{
    public static double[] CalcCma(this ReadOnlySpan<double> sequence)
    {
        var length = sequence.Length;

        var result = new double[length];
        
        if (length == 0)
        {
            return result;
        }

        double sum = 0;

        for (var i = 0; i < length; i++)
        {
            sum += sequence[i];
            
            result[i] = sum / (i + 1); 
        }
        
        return result;
    }

    public static double[] CalcSma(this ReadOnlySpan<double> sequence, int window)
    {
        var length = sequence.Length;

        var result = new double[length];
        
        if (length == 0)
        {
            return result;
        }

        double sum = 0;

        for (var i = 0; i < length; i++)
        {
            sum += sequence[i];

            if (i >= window)
            {
                sum -= sequence[i - window];
            }

            var count = Math.Min(i + 1, window);

            result[i] = sum / count;
        }

        return result;
    }

    public static double[] CalcEma(this ReadOnlySpan<double> sequence, int window)
    {
        var length = sequence.Length;

        var result = new double[length];

        if (length == 0)
        {
            return result;
        }

        var alpha = 2.0 / (window + 1);

        result[0] = sequence[0];

        for (var i = 1; i < length; i++)
        {
            result[i] = alpha * sequence[i] + (1 - alpha) * result[i - 1];
        }

        return result;
    }

    public static double[] CalcTema(this ReadOnlySpan<double> sequence, int window)
    {
        var ema1 = sequence.CalcEma(window);

        var ema2 = ema1.CalcEma(window);

        var ema3 = ema2.CalcEma(window);

        var length = sequence.Length;

        var result = new double[length];
        
        if (length == 0)
        {
            return result;
        }

        for (var i = 0; i < length; i++)
        {
            result[i] = 3.0 * ema1[i] - 3 * ema2[i] + ema3[i];
        }

        return result;
    }

    public static double[] CalcRma(this ReadOnlySpan<double> sequence, int window)
    {
        var length = sequence.Length;

        var result = new double[length];

        if (length == 0)
        {
            return result;
        }

        var alpha = 1.0 / window;

        result[0] = sequence[0];

        for (var i = 1; i < length; i++)
        {
            result[i] = alpha * sequence[i] + (1 - alpha) * result[i - 1];
        }

        return result;
    }

    public static double[] CalcTrendLine(this ReadOnlySpan<double> sequence)
    {
        var length = sequence.Length;
        
        var result = new double[length];
        
        if (length == 0)
        {
            return result;
        }

        double sumX = 0;

        double sumY = 0;

        double sumXy = 0;

        double sumX2 = 0;

        for (var i = 0; i < length; i++)
        {
            sumX += i;

            sumY += sequence[i];

            sumXy += i * sequence[i];

            sumX2 += i * i;
        }

        var slope = (length * sumXy - sumX * sumY) / (length * sumX2 - sumX * sumX);

        var intercept = (sumY - slope * sumX) / length;

        for (var i = 0; i < length; i++)
        {
            result[i] = slope * i + intercept;
        }
        
        return result;
    }

    public static double[] CalcRolStdDev(this ReadOnlySpan<double> sequence, int window)
    {
        var length = sequence.Length;

        var result = new double[length];
        
        if (length == 0)
        {
            return result;
        }

        double sum = 0;

        double sumSq = 0;

        for (var i = 0; i < length; i++)
        {
            var value = sequence[i];

            sum += value;

            sumSq += value * value;

            if (i >= window)
            {
                var outgoing = sequence[i - window];

                sum -= outgoing;

                sumSq -= outgoing * outgoing;
            }

            var count = Math.Min(i + 1, window);

            var mean = sum / count;

            var variance = Math.Max(sumSq / count - mean * mean, 0.0);

            result[i] = Math.Sqrt(variance);
        }

        return result;
    }

    private static double CalcStdDev(this ReadOnlySpan<double> sequence)
    {
        var length = sequence.Length;

        if (length == 0)
        {
            return 0.0;
        }
        
        double sum = 0;
        
        for (var i = 0; i < length; i++)
        {
            sum += sequence[i];
        }
        var average = sum / length;
        
        double sumSq = 0;
        
        for (var i = 0; i < length; i++)
        {
            var diff = sequence[i] - average;
            
            sumSq += diff * diff;
        }
        
        return Math.Sqrt(sumSq / length);
    }
    
    public static double CalcBeta(this double[] sequenceA, double[] sequenceB)
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
    
    public static double CalcCorrelation(this double[] sequenceA, double[] sequenceB)
    {
        var mx = sequenceA.Average();
        
        var my = sequenceB.Average();

        double num = 0;
        double dx = 0;
        double dy = 0;
        
        var length = sequenceA.Length;

        for (var i = 0; i < length; i++)
        {
            var vx = sequenceA[i] - mx;
            var vy = sequenceB[i] - my;

            num += vx * vy;
            dx += vx * vx;
            dy += vy * vy;
        }

        return num / Math.Sqrt(dx * dy);
    }
    
    public static (double Beta, double Variance) CalcKalmanBeta(
        this double valueA, double valueB, double prevBeta,
        double prevVariance, double q = 1e-6, double r = 1e-4)
    {
        var varPred = prevVariance + q;

        var innovation = valueA - prevBeta * valueB;
        
        var innovationVar = valueB * valueB * varPred + r;

        var k = varPred * valueB / innovationVar;

        return new ValueTuple<double, double>(
            prevBeta + k * innovation,
            (1 - k * valueB) * varPred
        );
    }

    public static double CalcZScore(this double[] sequence)
    {
        var currentValue = sequence.Last();

        var average = sequence.Average();

        var std = sequence.CalcStdDev();

        return std == 0 ? 0.0 : (currentValue - average) / std;
    }

    public static double[] CalcLogReturns(this double[] sequence)
    {
        var length = sequence.Length;

        var returns = new double[length - 1];

        for (var i = 1; i < length; i++)
        {
            returns[i - 1] = Math.Log(sequence[i] / sequence[i - 1]);
        }

        return returns;
    }
    
    public static double[] Winsorize(this double[] sequence, double numStdDev = 3.0)
    {
        var mean = sequence.Average();

        var std = sequence.CalcStdDev();

        if (std == 0) return sequence;

        var lower = mean - numStdDev * std;

        var upper = mean + numStdDev * std;

        var result = new double[sequence.Length];

        for (var i = 0; i < sequence.Length; i++)
        {
            result[i] = Math.Clamp(sequence[i], lower, upper);
        }

        return result;
    }
    
    public static double CalcWinsorizedZScore(this double[] sequence, double clipStdDev = 3.0)
    {
        var winsorized = sequence.Winsorize(clipStdDev);

        var zScore = winsorized.CalcZScore();

        return Math.Clamp(zScore, -clipStdDev, clipStdDev);
    }

    private static (double[] Mean, double[] StdDev) CalcRunningStats(this double[] sequence)
    {
        var length = sequence.Length;

        var mean = new double[length];

        var std = new double[length];

        double runningMean = 0;

        double m2 = 0;

        for (var i = 0; i < length; i++)
        {
            var n = i + 1;

            var delta = sequence[i] - runningMean;

            runningMean += delta / n;

            m2 += delta * (sequence[i] - runningMean);

            mean[i] = runningMean;

            std[i] = Math.Sqrt(Math.Max(m2 / n, 0));
        }

        return (mean, std);
    }
    
    public static double[] CalcExpandingZScore(this double[] sequence, double clipStdDev = 3.0)
    {
        var (mean, std) = sequence.CalcRunningStats();

        var length = sequence.Length;

        var result = new double[length];

        for (var i = 0; i < length; i++)
        {
            result[i] = std[i] == 0 ? 0.0 : Math.Clamp((sequence[i] - mean[i]) / std[i], -clipStdDev, clipStdDev);
        }

        return result;
    }
    
    public static double CalcEqualWeightedZScore(this IReadOnlyCollection<double> zScores)
    {
        return zScores.Count == 0 ? 0.0 : zScores.Average();
    }
}