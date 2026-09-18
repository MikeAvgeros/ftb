using Trading.Bot.Extensions;
using Trading.Bot.Models.Enums;

namespace Trading.Bot.Tests.Extensions;

public class NumericExtensionsTests
{
    [Fact]
    public void CalcCma_ReturnsCumulativeMovingAverage()
    {
        double[] sequence = [2, 4, 6];

        var result = sequence.CalcCma();

        Assert.Equal([2, 3, 4], result);
    }

    [Fact]
    public void CalcCma_EmptySequence_ReturnsEmptyArray()
    {
        double[] sequence = [];

        var result = sequence.CalcCma();

        Assert.Empty(result);
    }

    [Fact]
    public void CalcSma_ReturnsRollingAverageBoundedByWindow()
    {
        double[] sequence = [1, 2, 3, 4, 5];

        var result = sequence.CalcSma(2);

        Assert.Equal([1, 1.5, 2.5, 3.5, 4.5], result);
    }

    [Fact]
    public void CalcSma_EmptySequence_ReturnsEmptyArray()
    {
        double[] sequence = [];

        var result = sequence.CalcSma(3);

        Assert.Empty(result);
    }

    [Fact]
    public void CalcEma_ConstantSequence_ReturnsConstantValues()
    {
        double[] sequence = [5, 5, 5, 5];

        var result = sequence.CalcEma(3);

        Assert.Equal([5, 5, 5, 5], result);
    }

    [Fact]
    public void CalcEma_ComputesExponentialWeighting()
    {
        double[] sequence = [1, 2, 3];

        var result = sequence.CalcEma(2);

        Assert.Equal(1.0, result[0], 6);
        Assert.Equal(5.0 / 3.0, result[1], 6);
        Assert.Equal(23.0 / 9.0, result[2], 6);
    }

    [Fact]
    public void CalcTema_ConstantSequence_ReturnsConstantValues()
    {
        double[] sequence = [5, 5, 5, 5];

        var result = sequence.CalcTema(2);

        Assert.Equal([5, 5, 5, 5], result);
    }

    [Fact]
    public void CalcRma_ConstantSequence_ReturnsConstantValues()
    {
        double[] sequence = [4, 4, 4, 4];

        var result = sequence.CalcRma(3);

        Assert.Equal([4, 4, 4, 4], result);
    }

    [Fact]
    public void CalcRma_FirstValueEqualsFirstSequenceValue()
    {
        double[] sequence = [10, 20, 30];

        var result = sequence.CalcRma(2);

        Assert.Equal(10, result[0]);
    }

    [Fact]
    public void CalcTrendLine_LinearSequence_ReturnsSameValues()
    {
        double[] sequence = [1, 2, 3, 4, 5];

        var result = sequence.CalcTrendLine();

        Assert.Equal([1, 2, 3, 4, 5], result, EqualityComparer());
    }

    [Fact]
    public void CalcTrendLine_EmptySequence_ReturnsEmptyArray()
    {
        double[] sequence = [];

        var result = sequence.CalcTrendLine();

        Assert.Empty(result);
    }

    [Fact]
    public void CalcRolStdDev_ComputesRollingStandardDeviation()
    {
        double[] sequence = [1, 2, 3, 4];

        var result = sequence.CalcRolStdDev(2);

        Assert.Equal([0, 0.5, 0.5, 0.5], result, EqualityComparer());
    }

    [Fact]
    public void CalcBeta_ReturnsSlopeOfLinearRelationship()
    {
        double[] sequenceA = [1, 2, 3, 4, 5];
        double[] sequenceB = [2, 4, 6, 8, 10];

        var result = sequenceA.CalcBeta(sequenceB);

        Assert.Equal(0.5, result, 9);
    }

    [Fact]
    public void CalcBeta_ZeroDenominator_ReturnsOne()
    {
        double[] sequenceA = [1, 2, 3];
        double[] sequenceB = [5, 5, 5];

        var result = sequenceA.CalcBeta(sequenceB);

        Assert.Equal(1.0, result);
    }

    [Fact]
    public void CalcCorrelation_PerfectPositiveCorrelation_ReturnsOne()
    {
        double[] sequenceA = [1, 2, 3, 4, 5];
        double[] sequenceB = [2, 4, 6, 8, 10];

        var result = sequenceA.CalcCorrelation(sequenceB);

        Assert.Equal(1.0, result, 9);
    }

    [Fact]
    public void CalcCorrelation_PerfectNegativeCorrelation_ReturnsNegativeOne()
    {
        double[] sequenceA = [1, 2, 3, 4, 5];
        double[] sequenceB = [10, 8, 6, 4, 2];

        var result = sequenceA.CalcCorrelation(sequenceB);

        Assert.Equal(-1.0, result, 9);
    }

    [Fact]
    public void CalcKalmanBeta_ZeroObservation_KeepsPreviousBetaAndInflatesVariance()
    {
        var (beta, variance) = 5.0.CalcKalmanBeta(0.0, prevBeta: 2.0, prevVariance: 0.5);

        Assert.Equal(2.0, beta, 9);
        Assert.Equal(0.500001, variance, 9);
    }

    [Fact]
    public void CalcKalmanBeta_NoProcessOrObservationNoise_ExactlyMatchesRatio()
    {
        var (beta, variance) = 6.0.CalcKalmanBeta(3.0, prevBeta: 1.0, prevVariance: 2.0, q: 0.0, r: 0.0);

        Assert.Equal(2.0, beta, 9);
        Assert.Equal(0.0, variance, 9);
    }

    [Fact]
    public void CalcZScore_ComputesStandardDeviationsFromMean()
    {
        double[] sequence = [1, 2, 3, 4, 5];

        var result = sequence.CalcZScore();

        Assert.Equal(Math.Sqrt(2), result, 9);
    }

    [Fact]
    public void CalcZScore_ZeroStandardDeviation_ReturnsZero()
    {
        double[] sequence = [3, 3, 3];

        var result = sequence.CalcZScore();

        Assert.Equal(0.0, result);
    }

    [Fact]
    public void CalcLogReturns_ComputesLogOfRatios()
    {
        double[] sequence = [1, 2, 4];

        var result = sequence.CalcLogReturns();

        Assert.Equal([Math.Log(2), Math.Log(2)], result, EqualityComparer());
    }

    [Fact]
    public void Winsorize_ClampsValuesOutsideStdDevBounds()
    {
        double[] sequence = [0, 0, 0, 0, 10];

        var result = sequence.Winsorize(1.0);

        Assert.Equal([0, 0, 0, 0, 6], result, EqualityComparer());
    }

    [Fact]
    public void Winsorize_ZeroStandardDeviation_ReturnsOriginalSequence()
    {
        double[] sequence = [5, 5, 5];

        var result = sequence.Winsorize();

        Assert.Same(sequence, result);
    }

    [Fact]
    public void CalcWinsorizedZScore_ClampsClippedZScore()
    {
        double[] sequence = [0, 0, 0, 0, 10];

        var result = sequence.CalcWinsorizedZScore(1.0);

        Assert.Equal(1.0, result, 9);
    }

    [Fact]
    public void CalcExpandingZScore_ComputesRunningZScores()
    {
        double[] sequence = [1, 2, 3, 4, 5];

        var result = sequence.CalcExpandingZScore();

        Assert.Equal(0.0, result[0], 6);
        Assert.Equal(1.0, result[1], 6);
        Assert.Equal(Math.Sqrt(1.5), result[2], 6);
        Assert.Equal(Math.Sqrt(1.8), result[3], 6);
        Assert.Equal(Math.Sqrt(2.0), result[4], 6);
    }

    [Fact]
    public void CalcExpandingZScore_ClampsToClipStdDev()
    {
        double[] sequence = [1, 1, 1, 1, 100];

        var result = sequence.CalcExpandingZScore(1.0);

        Assert.Equal(1.0, result[4], 9);
    }

    [Fact]
    public void CalcEqualWeightedZScore_ReturnsAverageOfScores()
    {
        double[] zScores = [1, 2, 3];

        var result = zScores.CalcEqualWeightedZScore();

        Assert.Equal(2.0, result);
    }

    [Fact]
    public void CalcEqualWeightedZScore_EmptyCollection_ReturnsZero()
    {
        var result = Array.Empty<double>().CalcEqualWeightedZScore();

        Assert.Equal(0.0, result);
    }

    [Fact]
    public void ClassifySpreadRegime_ZScoreBelowEntryThreshold_ReturnsNone()
    {
        var result = 1.0.ClassifySpreadRegime(
            spreadHistory: [],
            volumeHistory: [],
            entryZ: 2.0,
            momentumShortWindowDivisor: 5,
            momentumZThreshold: 1.0,
            volumeZThreshold: 1.0,
            lowVolumeZThreshold: -0.5,
            volatilitySpikeRatio: 1.5);

        Assert.Equal(SpreadRegime.None, result);
    }

    [Fact]
    public void ClassifySpreadRegime_TooLittleHistory_ReturnsAmbiguous()
    {
        var result = 3.0.ClassifySpreadRegime(
            spreadHistory: [1, 2],
            volumeHistory: [],
            entryZ: 2.0,
            momentumShortWindowDivisor: 5,
            momentumZThreshold: 1.0,
            volumeZThreshold: 1.0,
            lowVolumeZThreshold: -0.5,
            volatilitySpikeRatio: 1.5);

        Assert.Equal(SpreadRegime.Ambiguous, result);
    }

    [Fact]
    public void ClassifySpreadRegime_NoTrendOrVolumeSignal_ReturnsAmbiguous()
    {
        var result = 3.0.ClassifySpreadRegime(
            spreadHistory: [5, 5, 5, 5, 5],
            volumeHistory: [5, 5, 5, 5, 5],
            entryZ: 2.0,
            momentumShortWindowDivisor: 5,
            momentumZThreshold: 1.0,
            volumeZThreshold: 1.0,
            lowVolumeZThreshold: -0.5,
            volatilitySpikeRatio: 1.5);

        Assert.Equal(SpreadRegime.Ambiguous, result);
    }

    [Fact]
    public void ClassifySpreadRegime_StrongTrendMatchingZScoreSign_ReturnsMomentumContinuation()
    {
        double[] spreadHistory = [1, 2, 3, 4, 5, 6, 7, 8, 9, 10];

        var result = 3.0.ClassifySpreadRegime(
            spreadHistory,
            volumeHistory: [100, 100, 100],
            entryZ: 2.0,
            momentumShortWindowDivisor: 5,
            momentumZThreshold: 1.0,
            volumeZThreshold: 1.0,
            lowVolumeZThreshold: -0.5,
            volatilitySpikeRatio: 1.5);

        Assert.Equal(SpreadRegime.MomentumContinuation, result);
    }

    [Fact]
    public void ClassifySpreadRegime_HighVolumeZScore_ReturnsAbnormalVolumeContinuation()
    {
        var result = 3.0.ClassifySpreadRegime(
            spreadHistory: [5, 5, 5, 5, 5],
            volumeHistory: [1, 1, 1, 1, 10],
            entryZ: 2.0,
            momentumShortWindowDivisor: 5,
            momentumZThreshold: 1.0,
            volumeZThreshold: 1.0,
            lowVolumeZThreshold: -0.5,
            volatilitySpikeRatio: 1.5);

        Assert.Equal(SpreadRegime.AbnormalVolumeContinuation, result);
    }

    [Fact]
    public void ClassifySpreadRegime_LowVolumeZScore_ReturnsLowVolumeReversion()
    {
        var result = 3.0.ClassifySpreadRegime(
            spreadHistory: [5, 5, 5, 5, 5],
            volumeHistory: [10, 10, 10, 10, 1],
            entryZ: 2.0,
            momentumShortWindowDivisor: 5,
            momentumZThreshold: 1.0,
            volumeZThreshold: 5.0,
            lowVolumeZThreshold: -1.0,
            volatilitySpikeRatio: 1.5);

        Assert.Equal(SpreadRegime.LowVolumeReversion, result);
    }

    [Fact]
    public void ClassifySpreadRegime_ShortWindowVolatilitySpike_ReturnsVolatilitySpikeReversion()
    {
        double[] spreadHistory = [5, 5, 5, 5, 5, 5, 5, 100];

        var result = 3.0.ClassifySpreadRegime(
            spreadHistory,
            volumeHistory: [1, 2, 3],
            entryZ: 2.0,
            momentumShortWindowDivisor: 4,
            momentumZThreshold: 1000.0,
            volumeZThreshold: 1000.0,
            lowVolumeZThreshold: -1000.0,
            volatilitySpikeRatio: 1.0);

        Assert.Equal(SpreadRegime.VolatilitySpikeReversion, result);
    }

    private static IEqualityComparer<double> EqualityComparer() => new ApproximateDoubleComparer(1e-6);

    private sealed class ApproximateDoubleComparer(double tolerance) : IEqualityComparer<double>
    {
        public bool Equals(double x, double y) => Math.Abs(x - y) <= tolerance;

        public int GetHashCode(double obj) => 0;
    }
}
