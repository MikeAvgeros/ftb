using System.Net;
using Trading.Bot.Extensions;
using Trading.Bot.Models.DataTransferObjects;
using Trading.Bot.Models.Enums;
using Trading.Bot.Models.Indicators;

namespace Trading.Bot.Tests.Extensions;

public class MiscellaneousExtensionsTests
{
    [Fact]
    public void RoundDown_WithDaySpan_RoundsDownToNearestDayBoundary()
    {
        var time = new DateTime(2024, 1, 15, 13, 45, 30);

        var result = time.RoundDown(TimeSpan.FromDays(7));

        Assert.Equal(new DateTime(2024, 1, 14, 0, 0, 0), result);
    }

    [Fact]
    public void RoundDown_WithHourSpan_RoundsDownToNearestHourBoundary()
    {
        var time = new DateTime(2024, 1, 15, 13, 45, 30);

        var result = time.RoundDown(TimeSpan.FromHours(4));

        Assert.Equal(new DateTime(2024, 1, 15, 12, 0, 0), result);
    }

    [Fact]
    public void RoundDown_WithMinuteSpan_RoundsDownToNearestMinuteBoundary()
    {
        var time = new DateTime(2024, 1, 15, 13, 47, 30);

        var result = time.RoundDown(TimeSpan.FromMinutes(15));

        Assert.Equal(new DateTime(2024, 1, 15, 13, 45, 0), result);
    }

    [Fact]
    public void RoundDown_WithSubMinuteSpan_ReturnsOriginalTime()
    {
        var time = new DateTime(2024, 1, 15, 13, 47, 30);

        var result = time.RoundDown(TimeSpan.FromSeconds(30));

        Assert.Equal(time, result);
    }

    [Theory]
    [InlineData(200, true)]
    [InlineData(299, true)]
    [InlineData(199, false)]
    [InlineData(300, false)]
    [InlineData(404, false)]
    public void IsSuccessStatusCode_ChecksRangeInclusive(int statusCode, bool expected)
    {
        var result = ((HttpStatusCode)statusCode).IsSuccessStatusCode();

        Assert.Equal(expected, result);
    }

    [Fact]
    public void CalcTakeProfit_BuySignal_AddsGainTimesRiskReward()
    {
        var candle = new Candle { Mid_C = 100 };
        var result = new IndicatorResult { Candle = candle, Signal = Signal.Buy, Gain = 10 };

        var takeProfit = candle.CalcTakeProfit(result, 2);

        Assert.Equal(120, takeProfit);
    }

    [Fact]
    public void CalcTakeProfit_SellSignal_SubtractsGainTimesRiskReward()
    {
        var candle = new Candle { Mid_C = 100 };
        var result = new IndicatorResult { Candle = candle, Signal = Signal.Sell, Gain = 10 };

        var takeProfit = candle.CalcTakeProfit(result, 2);

        Assert.Equal(80, takeProfit);
    }

    [Fact]
    public void CalcTakeProfit_NoSignal_ReturnsZero()
    {
        var candle = new Candle { Mid_C = 100 };
        var result = new IndicatorResult { Candle = candle, Signal = Signal.None, Gain = 10 };

        var takeProfit = candle.CalcTakeProfit(result, 2);

        Assert.Equal(0, takeProfit);
    }

    [Fact]
    public void CalcStopLoss_BuySignal_SubtractsGain()
    {
        var candle = new Candle { Mid_C = 100 };
        var result = new IndicatorResult { Candle = candle, Signal = Signal.Buy, Gain = 10 };

        var stopLoss = candle.CalcStopLoss(result);

        Assert.Equal(90, stopLoss);
    }

    [Fact]
    public void CalcStopLoss_SellSignal_AddsGain()
    {
        var candle = new Candle { Mid_C = 100 };
        var result = new IndicatorResult { Candle = candle, Signal = Signal.Sell, Gain = 10 };

        var stopLoss = candle.CalcStopLoss(result);

        Assert.Equal(110, stopLoss);
    }

    [Fact]
    public void CalcStopLoss_NoSignal_ReturnsZero()
    {
        var candle = new Candle { Mid_C = 100 };
        var result = new IndicatorResult { Candle = candle, Signal = Signal.None, Gain = 10 };

        var stopLoss = candle.CalcStopLoss(result);

        Assert.Equal(0, stopLoss);
    }

    [Theory]
    [InlineData(Signal.Buy, Signal.Sell)]
    [InlineData(Signal.Sell, Signal.Buy)]
    [InlineData(Signal.None, Signal.None)]
    public void GetContinuationSignal_ReturnsOppositeSignal(Signal reversionSignal, Signal expected)
    {
        var result = reversionSignal.GetContinuationSignal();

        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData(SpreadRegime.LowVolumeReversion, Signal.Buy, Signal.Buy)]
    [InlineData(SpreadRegime.VolatilitySpikeReversion, Signal.Sell, Signal.Sell)]
    [InlineData(SpreadRegime.MomentumContinuation, Signal.Buy, Signal.Sell)]
    [InlineData(SpreadRegime.AbnormalVolumeContinuation, Signal.Sell, Signal.Buy)]
    [InlineData(SpreadRegime.None, Signal.Buy, Signal.None)]
    [InlineData(SpreadRegime.Ambiguous, Signal.Sell, Signal.None)]
    public void ResolveRegimeSignal_MapsRegimeToExpectedSignal(
        SpreadRegime regime, Signal reversionSignal, Signal expected)
    {
        var result = regime.ResolveRegimeSignal(reversionSignal);

        Assert.Equal(expected, result);
    }
}
