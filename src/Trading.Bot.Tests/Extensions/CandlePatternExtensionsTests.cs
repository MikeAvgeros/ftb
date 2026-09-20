using Trading.Bot.Extensions;
using Trading.Bot.Models.DataTransferObjects;

namespace Trading.Bot.Tests.Extensions;

public class CandlePatternExtensionsTests
{
    [Fact]
    public void IsHangingMan_NullCandle_ReturnsFalse()
    {
        Candle? candle = null;

        Assert.False(candle!.IsHangingMan());
    }

    [Fact]
    public void IsHangingMan_LongLowerWickSmallBody_ReturnsTrue()
    {
        var candle = new Candle { BodyBottomPercentage = 80, BodyPercentage = 10 };

        Assert.True(candle.IsHangingMan());
    }

    [Fact]
    public void IsHangingMan_LargeBody_ReturnsFalse()
    {
        var candle = new Candle { BodyBottomPercentage = 80, BodyPercentage = 20 };

        Assert.False(candle.IsHangingMan());
    }

    [Fact]
    public void IsShootingStar_NullCandle_ReturnsFalse()
    {
        Candle? candle = null;

        Assert.False(candle!.IsShootingStar());
    }

    [Fact]
    public void IsShootingStar_LongUpperWickSmallBody_ReturnsTrue()
    {
        var candle = new Candle { BodyTopPercentage = 80, BodyPercentage = 10 };

        Assert.True(candle.IsShootingStar());
    }

    [Fact]
    public void IsShootingStar_ShortUpperWick_ReturnsFalse()
    {
        var candle = new Candle { BodyTopPercentage = 70, BodyPercentage = 10 };

        Assert.False(candle.IsShootingStar());
    }

    [Fact]
    public void IsSpinningTop_NullCandle_ReturnsFalse()
    {
        Candle? candle = null;

        Assert.False(candle!.IsSpinningTop());
    }

    [Fact]
    public void IsSpinningTop_CenteredSmallBody_ReturnsTrue()
    {
        var candle = new Candle { BodyTopPercentage = 45, BodyBottomPercentage = 45, BodyPercentage = 10 };

        Assert.True(candle.IsSpinningTop());
    }

    [Fact]
    public void IsSpinningTop_LargeBody_ReturnsFalse()
    {
        var candle = new Candle { BodyTopPercentage = 45, BodyBottomPercentage = 45, BodyPercentage = 20 };

        Assert.False(candle.IsSpinningTop());
    }

    [Fact]
    public void IsMarubozu_LargeBody_ReturnsTrue()
    {
        var candle = new Candle { BodyPercentage = 99 };

        Assert.True(candle.IsMarubozu());
    }

    [Fact]
    public void IsMarubozu_SmallBody_ReturnsFalse()
    {
        var candle = new Candle { BodyPercentage = 90 };

        Assert.False(candle.IsMarubozu());
    }

    [Fact]
    public void IsEngulfingCandle_NullCandle_ReturnsFalse()
    {
        Candle? candle = null;
        var prevCandle = new Candle();

        Assert.False(candle!.IsEngulfingCandle(prevCandle));
    }

    [Fact]
    public void IsEngulfingCandle_NullPrevCandle_ReturnsFalse()
    {
        var candle = new Candle();
        Candle? prevCandle = null;

        Assert.False(candle.IsEngulfingCandle(prevCandle!));
    }

    [Fact]
    public void IsEngulfingCandle_OppositeDirectionLargerBody_ReturnsTrue()
    {
        var candle = new Candle { Direction = 1, BodySize = 15 };
        var prevCandle = new Candle { Direction = -1, BodySize = 10 };

        Assert.True(candle.IsEngulfingCandle(prevCandle));
    }

    [Fact]
    public void IsEngulfingCandle_SameDirection_ReturnsFalse()
    {
        var candle = new Candle { Direction = 1, BodySize = 15 };
        var prevCandle = new Candle { Direction = 1, BodySize = 10 };

        Assert.False(candle.IsEngulfingCandle(prevCandle));
    }

    [Fact]
    public void IsEngulfingCandle_BodyNotLargeEnough_ReturnsFalse()
    {
        var candle = new Candle { Direction = 1, BodySize = 11 };
        var prevCandle = new Candle { Direction = -1, BodySize = 10 };

        Assert.False(candle.IsEngulfingCandle(prevCandle));
    }

    [Fact]
    public void IsTweezerTop_NullCandle_ReturnsFalse()
    {
        Candle? candle = null;
        var prevCandle = new Candle();

        Assert.False(candle!.IsTweezerTop(prevCandle));
    }

    [Fact]
    public void IsTweezerTop_MatchingHighsWithReversal_ReturnsTrue()
    {
        var prevCandle = new Candle { Mid_L = 100m, Mid_H = 110m, BodySize = 10m, Direction = 1 };
        var candle = new Candle
        {
            Mid_L = 100.005m, Mid_H = 110.005m, BodySize = 10.5m, Direction = -1, BodyTopPercentage = 30
        };

        Assert.True(candle.IsTweezerTop(prevCandle));
    }

    [Fact]
    public void IsTweezerTop_SameDirection_ReturnsFalse()
    {
        var prevCandle = new Candle { Mid_L = 100m, Mid_H = 110m, BodySize = 10m, Direction = 1 };
        var candle = new Candle
        {
            Mid_L = 100.005m, Mid_H = 110.005m, BodySize = 10.5m, Direction = 1, BodyTopPercentage = 30
        };

        Assert.False(candle.IsTweezerTop(prevCandle));
    }

    [Fact]
    public void IsTweezerBottom_NullCandle_ReturnsFalse()
    {
        Candle? candle = null;
        var prevCandle = new Candle();

        Assert.False(candle!.IsTweezerBottom(prevCandle));
    }

    [Fact]
    public void IsTweezerBottom_MatchingLowsWithReversal_ReturnsTrue()
    {
        var prevCandle = new Candle { Mid_L = 100m, Mid_H = 110m, BodySize = 10m, Direction = -1 };
        var candle = new Candle
        {
            Mid_L = 100.005m, Mid_H = 110.005m, BodySize = 10.5m, Direction = 1, BodyBottomPercentage = 70
        };

        Assert.True(candle.IsTweezerBottom(prevCandle));
    }

    [Fact]
    public void IsTweezerBottom_SmallLowerWick_ReturnsFalse()
    {
        var prevCandle = new Candle { Mid_L = 100m, Mid_H = 110m, BodySize = 10m, Direction = -1 };
        var candle = new Candle
        {
            Mid_L = 100.005m, Mid_H = 110.005m, BodySize = 10.5m, Direction = 1, BodyBottomPercentage = 50
        };

        Assert.False(candle.IsTweezerBottom(prevCandle));
    }

    [Fact]
    public void IsMorningStar_NullCandle_ReturnsFalse()
    {
        Candle? candle = null;
        var lastTwoCandles = new[] { new Candle(), new Candle() };

        Assert.False(candle!.IsMorningStar(lastTwoCandles));
    }

    [Fact]
    public void IsMorningStar_WrongNumberOfPriorCandles_ReturnsFalse()
    {
        var candle = new Candle();
        var lastTwoCandles = new[] { new Candle() };

        Assert.False(candle.IsMorningStar(lastTwoCandles));
    }

    [Fact]
    public void IsMorningStar_BullishPattern_ReturnsTrue()
    {
        var prev2Candle = new Candle
        {
            Time = new DateTime(2024, 1, 1), BodyPercentage = 95, Direction = -1, MidPoint = 100
        };
        var prevCandle = new Candle { Time = new DateTime(2024, 1, 2), BodyPercentage = 5 };
        var candle = new Candle { Direction = 1, Mid_C = 105 };

        Assert.True(candle.IsMorningStar([prevCandle, prev2Candle]));
    }

    [Fact]
    public void IsMorningStar_CloseDoesNotClearMidpoint_ReturnsFalse()
    {
        var prev2Candle = new Candle
        {
            Time = new DateTime(2024, 1, 1), BodyPercentage = 95, Direction = -1, MidPoint = 100
        };
        var prevCandle = new Candle { Time = new DateTime(2024, 1, 2), BodyPercentage = 5 };
        var candle = new Candle { Direction = 1, Mid_C = 95 };

        Assert.False(candle.IsMorningStar([prevCandle, prev2Candle]));
    }

    [Fact]
    public void HigherHighs_MultipleAscendingSwingHighs_ReturnsTrue()
    {
        var candles = CreateCandles(midHighs: [10, 15, 8, 20, 12, 5, 5]);

        Assert.True(candles.HigherHighs());
    }

    [Fact]
    public void HigherHighs_SingleSwingHigh_ReturnsFalse()
    {
        var candles = CreateCandles(midHighs: [10, 15, 8, 12, 9, 5, 5]);

        Assert.False(candles.HigherHighs());
    }

    [Fact]
    public void LowerHighs_MultipleDescendingSwingHighs_ReturnsTrue()
    {
        var candles = CreateCandles(midHighs: [100, 10, 90, 5, 70, 5, 100]);

        Assert.True(candles.LowerHighs());
    }

    [Fact]
    public void LowerHighs_SingleQualifyingSwingHigh_ReturnsFalse()
    {
        var candles = CreateCandles(midHighs: [100, 10, 90, 5, 95, 5, 100]);

        Assert.False(candles.LowerHighs());
    }

    [Fact]
    public void HigherLows_MultipleAscendingSwingLows_ReturnsTrue()
    {
        var candles = CreateCandles(midLows: [-100, 90, 10, 95, 20, 95, -100]);

        Assert.True(candles.HigherLows());
    }

    [Fact]
    public void HigherLows_SingleQualifyingSwingLow_ReturnsFalse()
    {
        var candles = CreateCandles(midLows: [-100, 90, 10, 95, 5, 95, -100]);

        Assert.False(candles.HigherLows());
    }

    [Fact]
    public void LowerLows_MultipleDescendingSwingLows_ReturnsTrue()
    {
        var candles = CreateCandles(midLows: [10, 5, 12, 3, 8, 9, 9]);

        Assert.True(candles.LowerLows());
    }

    [Fact]
    public void LowerLows_SingleQualifyingSwingLow_ReturnsFalse()
    {
        var candles = CreateCandles(midLows: [10, 5, 12, 6, 8, 9, 9]);

        Assert.False(candles.LowerLows());
    }

    [Fact]
    public void CalcResistance_ReturnsHighestSwingHighOrFirstCandle()
    {
        var candles = CreateCandles(midHighs: [10, 15, 8, 20, 12, 5, 5]);

        var result = candles.CalcResistance();

        Assert.Equal(20.0, result);
    }

    [Fact]
    public void CalcSupport_ReturnsLowestSwingLowOrFirstCandle()
    {
        var candles = CreateCandles(midLows: [10, 5, 12, 3, 8, 9, 9]);

        var result = candles.CalcSupport();

        Assert.Equal(3.0, result);
    }

    private static Candle[] CreateCandles(decimal[]? midHighs = null, decimal[]? midLows = null)
    {
        var length = midHighs?.Length ?? midLows?.Length ?? 0;

        var candles = new Candle[length];

        for (var i = 0; i < length; i++)
        {
            candles[i] = new Candle
            {
                Mid_H = midHighs?[i] ?? 0,
                Mid_L = midLows?[i] ?? 0
            };
        }

        return candles;
    }
}
