namespace Trading.Bot.Models.DataTransferObjects;

public class LivePrice : PriceBase
{
    public DateTime Time { get; set; }

    public LivePrice(PriceResponse price)
    {
        Instrument = price.Instrument;
        Price = price.Bids is { Length: > 0 } && price.Asks is { Length: > 0 }
            ? (price.Bids[0].Price + price.Asks[0].Price) / 2
            : (price.CloseoutBid + price.CloseoutAsk) / 2;
        Time = price.Time;
    }

    private LivePrice() { }

    public LivePrice Snapshot(DateTime candleTime) => new()
    {
        Instrument = Instrument,
        Price = Price,
        Time = candleTime
    };
}
