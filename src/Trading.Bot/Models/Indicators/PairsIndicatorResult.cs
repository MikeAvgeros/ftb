namespace Trading.Bot.Models.Indicators
{
    public class PairsIndicatorResult : IndicatorBase
    {
        public Candle CandleA { get; set; }
        public Candle CandleB { get; set; }
        public bool TakeProfit { get; set; }
        public bool StopLoss { get; set; }
        public decimal Beta { get; set; } = 1m;
        public double ZScore { get; set; }
        public decimal UnitsA { get; set; }
        public decimal UnitsB { get; set; }
    }
}
