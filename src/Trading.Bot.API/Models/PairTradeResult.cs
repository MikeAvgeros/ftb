namespace Trading.Bot.API.Models;

public class PairTradeResult
{
    public bool Running { get; set; }
    public Signal CandleASignal { get; set; }
    public Signal CandleBSignal { get; set; }
    public decimal TriggerAPrice { get; set; }
    public decimal TriggerBPrice { get; set; }
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public decimal Result { get; set; }
    public decimal UnrealisedPl { get; set; }
}