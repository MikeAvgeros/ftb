namespace Trading.Bot.Extensions;

public static class NumericExtensions
{
    public static IEnumerable<double> CalcCma(this double[] sequence)
    {
        if (sequence is null)
        {
            yield break;
        }

        double total = 0;

        var count = 0;

        var length = sequence.Length;

        for (var i = 0; i < length; i++)
        {
            count++;

            total += sequence[i];

            yield return total / count;
        }
    }

    public static IEnumerable<double> CalcSma(this double[] sequence, int window)
    {
        if (sequence is null)
        {
            yield break;
        }

        var length = sequence.Length;

        if (length <= window)
        {
            yield return 0.0;
        }

        var queue = new Queue<double>(window);

        for (var i = 0; i < length; i++)
        {
            if (queue.Count == window)
            {
                queue.Dequeue();
            }

            queue.Enqueue(sequence[i]);

            yield return queue.Average();
        }
    }

    public static IEnumerable<double> CalcEma(this double[] sequence, int window)
    {
        if (sequence is null)
        {
            yield break;
        }

        var length = sequence.Length;

        if (length <= window)
        {
            yield return 0.0;
        }

        var alpha = 2.0 / (window + 1);

        var result = 0.0;

        for (var i = 0; i < length; i++)
        {
            result = i == 0
                ? sequence[i]
                : alpha * sequence[i] + (1 - alpha) * result;

            yield return result;
        }
    }

    public static IEnumerable<double> CalcTema(this double[] sequence, int window)
    {
        var ema1 = sequence.CalcEma(window).ToArray();

        var ema2 = ema1.CalcEma(window).ToArray();

        var ema3 = ema2.CalcEma(window).ToArray();

        var length = sequence.Length;

        var tema = new double[length];

        for (var i = 0; i < length; i++)
        {
            tema[i] = 3.0 * ema1[i] - 3 * ema2[i] + ema3[i];
        }

        return tema;
    }

    public static IEnumerable<double> CalcRma(this double[] sequence, int window)
    {
        if (sequence is null)
        {
            yield break;
        }

        var length = sequence.Length;

        if (length <= window)
        {
            yield return 0.0;
        }

        var alpha = 1.0 / window;

        var result = 0.0;

        for (var i = 0; i < length; i++)
        {
            result = i == 0
                ? sequence[i]
                : alpha * sequence[i] + (1 - alpha) * result;

            yield return result;
        }
    }

    public static IEnumerable<double> CalcTrendLine(this double[] sequence)
    {
        if (sequence is null)
        {
            yield break;
        }

        var length = sequence.Length;

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
            yield return slope * i + intercept;
        }
    }

    public static IEnumerable<double> CalcRolStdDev(this double[] sequence, int window)
    {
        if (sequence is null)
        {
            yield break;
        }

        var length = sequence.Length;

        if (length <= window)
        {
            yield return 0.0;
        }

        var queue = new Queue<double>(window);

        for (var i = 0; i < length; i++)
        {
            if (queue.Count == window)
            {
                queue.Dequeue();
            }

            queue.Enqueue(sequence[i]);

            yield return queue.CalcStdDev();
        }
    }

    public static double CalcStdDev(this IEnumerable<double> sequence)
    {
        if (sequence is null)
        {
            return 0.0;
        }

        var list = sequence.ToArray();

        var length = list.Length;

        if (length <= 1)
        {
            return 0.0;
        }

        var average = list.Average();

        double sumSq = 0;

        for (var i = 0; i < length; i++)
        {
            var value = list[i];
            sumSq += (value - average) * (value - average);
        }

        return Math.Sqrt(sumSq / length).NaN2Zero();
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

        var returns = new double[length];

        returns[0] = Math.Log(sequence[0]);

        for (var i = 1; i < length; i++)
        {
            returns[i] = Math.Log(sequence[i] / sequence[i - 1]);
        }

        return returns;
    }

    public static double Round(this double value, int decimalPoints)
    {
        var precision = int.Parse("1".PadRight(decimalPoints, '0'));

        return Math.Floor(value * precision) / precision;
    }

    private static double NaN2Zero(this double value)
        => double.IsNaN(value)
            ? 0.0
            : value;
}