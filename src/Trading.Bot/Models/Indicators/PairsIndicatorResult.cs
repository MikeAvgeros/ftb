namespace Trading.Bot.Models.Indicators
{
    public class PairsIndicatorResult : IndicatorResult
    {
        public bool Exit { get; set; }
        public decimal UnitsA { get; set; }
        public decimal UnitsB { get; set; }
    }
}
