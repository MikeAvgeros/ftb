# ftb — Algorithmic Trading Bot

An algorithmic trading system for [OANDA](https://www.oanda.com/) FX accounts.

The system streams live prices, evaluates a library of technical and statistical indicators and strategies, and automatically places and manages trades. It also provides a companion HTTP API for account and candle inspection, as well as offline strategy backtesting.

## Solution Layout

The solution consists of two .NET 10 projects:

| Project               | Type                 | Purpose                                                                                                                                                                     |
| --------------------- | -------------------- | --------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `src/Trading.Bot`     | .NET Worker Service  | Connects to OANDA's streaming and REST APIs, evaluates configured strategies on new candles, and places and manages live trades. Runs continuously as a background service. |
| `src/Trading.Bot.API` | ASP.NET Core Web API | Provides read-only account, instrument, and candle endpoints, plus a `/api/simulation/run` endpoint for backtesting strategies against historical candles uploaded as CSV.  |

Both projects target **.NET 10** and share the indicator/strategy library, models, and OANDA client code defined in `Trading.Bot`.

---

## `Trading.Bot` — Live Trading Worker

### `Services/`

Background services and trading infrastructure:

- **`OandaApiService`** — REST client for OANDA, providing access to candles, instruments, orders, trades, and account information.
- **`OandaStreamService` / `StreamProcessor` / `StreamWorker`** — Consume and process OANDA's live price stream.
- **`LiveTradeCache`** — In-memory cache of live prices and open trades.
- **`TradeManager`** — Evaluates single-instrument strategies and executes trades.
- **`PairsTradeManager`** — Evaluates pairs-trading strategies across two correlated instruments and manages both legs as a unit, including entry, exit, and partial-fill rollback.
- **`RolloverManager`** — Optionally flattens positions around the daily rollover period.
- **`EmailService`** — Sends trade notification emails.

### `Extensions/IndicatorExtensions/`

The indicator and strategy library.

Each file contains an extension method on `Candle[]` that returns an array of indicator results:

- `IndicatorResult[]` for single-instrument strategies.
- `PairsIndicatorResult[]` for pairs-trading strategies.

Examples include:

- `CalcRsi`
- `CalcMacd`
- `CalcBollingerBands`
- `CalcTrendConfluence`
- `CalcMaDistanceZScore`
- `CalcReturnSpreadZScore`
- `CalcHedgeZScore`
- `CalcRatioZScore`
- `CalcKalmanFilteredReturnSpread`
- `CalcEqualWeightedZScore`

### `Extensions/NumericExtensions.cs`

Shared numerical building blocks used across indicators and strategies, including:

- Moving averages
- Standard deviations
- Z-scores
- Correlation and beta
- Kalman filter calculations
- Winsorization
- Other statistical and numerical operations

### `Configuration/`

Strongly typed application settings:

- `TradeConfiguration`
- `TradeSettings`
- `EmailConfiguration`
- OANDA constants

These are bound from `appsettings.json`.

### `Models/`

Shared models, including:

- DTOs
- API response models
- Indicator result models
- Enums such as `Signal` and `SpreadRegime`

---

## `Trading.Bot.API` — Backtesting & Inspection API

### `Endpoints/`

Minimal API endpoints:

| Method | Endpoint              | Purpose                                                           |
| ------ | --------------------- | ----------------------------------------------------------------- |
| `GET`  | `/api/account`        | Returns an account summary.                                       |
| `GET`  | `/api/candles`        | Retrieves historical candles for an instrument.                   |
| `GET`  | `/api/instruments`    | Returns instrument metadata.                                      |
| `POST` | `/api/simulation/run` | Runs a selected strategy against uploaded historical candle data. |

### `Mediator/Strategies/`

Contains one `IStrategy` implementation for each `StrategyType`.

Each strategy maps a `RunStrategyRequest` — including window sizes, thresholds, and risk settings — onto the corresponding indicator function and simulator.

### `Extensions/BackTestingExtensions.cs`

Contains the backtesting simulator.

The simulator:

1. Replays indicator output bar-by-bar.
2. Opens and closes simulated trades according to generated signals.
3. Applies transaction costs and slippage.
4. Produces `SimulationSummary` statistics such as win rate and balance.

Pairs-trading strategies are simulated twice per run:

- **Regime-aware strategy** — respects the strategy's classification of mean-reversion and continuation signals.
- **Pure mean-reversion baseline** — treats all qualifying spread deviations as mean-reversion opportunities.

Both runs use identical transaction-cost and slippage assumptions, allowing their performance to be compared directly.

### `Diagnostics/`

OpenTelemetry configuration for tracing and metrics export.

---

## Configuration

Runtime configuration is stored in each project's `appsettings.json`, with `appsettings.Development.json` providing local overrides.

### `Constants`

OANDA configuration, including:

- API key
- Account ID
- REST API base URL
- Streaming API base URL

### `TradeConfiguration`

Trading configuration, including:

- Trading mode
- Pairs-trading enablement
- Risk per trade
- Email notifications
- Instrument-specific `TradeSettings`

Each `TradeSettings` entry contains parameters such as:

- Candle granularity
- Indicator windows
- Indicator thresholds
- Maximum spread
- Risk/reward
- Trailing stop

### `EmailConfiguration`

SMTP configuration used for trade notification emails.

> **Do not commit real API keys or credentials.**

The values committed to `appsettings.json` are placeholders or practice-account values. CI replaces sensitive values at build time using GitHub Actions secrets (see `.github/workflows/docker-image.yml`). Production secrets are injected in the same way and are not stored in the repository.

---

## Building & Running

Build the entire solution:

```bash
dotnet build
```

Run the live trading bot:

```bash
dotnet run --project src/Trading.Bot
```

Run the backtesting and inspection API:

```bash
dotnet run --project src/Trading.Bot.API
```

Both projects also include Dockerfiles for containerized deployment:

```text
src/Trading.Bot/Dockerfile
src/Trading.Bot.API/Dockerfile
```

---

## Observability

The `observability/docker-compose.yml` file provides a local observability stack containing:

- OpenTelemetry Collector
- Prometheus
- Grafana
- Preconfigured Grafana datasource

Start the stack with:

```bash
docker compose -f observability/docker-compose.yml up
```

---

## Backtesting a Strategy

### 1. Prepare historical data

Export historical candles for each instrument to CSV using the format expected by:

```text
GetObjectFromCsv<Candle>
```

### 2. Run the simulation

Send the candle CSV files to:

```text
POST /api/simulation/run
```

Use the query string to select the `StrategyType` and provide its parameters, including:

- `Ints`
- `Doubles`
- `MaxSpread`
- `TradeRisk`
- `TransactionCost`
- `Slippage`

See `RunStrategyRequest` for the complete set of available parameters.

### 3. Review the results

The response is a ZIP archive containing:

- Raw signal data
- Simulated trade-by-trade results
- Simulation summary statistics

For pairs-trading strategies, the archive additionally contains:

- Regime-aware simulation results
- Pure mean-reversion baseline results
- Comparison summary
