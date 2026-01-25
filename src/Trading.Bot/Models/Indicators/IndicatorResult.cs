namespace Trading.Bot.Models.Indicators;

public class IndicatorResult : IndicatorBase
{
    public Candle Candle { get; set; }
    public decimal Gain { get; set; }
    public decimal TakeProfit { get; set; }
    public decimal StopLoss { get; set; }
    public decimal Loss { get; set; }
}