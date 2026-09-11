# ftb — Algorithmic Trading Bot

**ftb** is an algorithmic foreign-exchange trading system built on .NET 10 and designed to work with [OANDA](https://www.oanda.com/?utm_source=chatgpt.com) accounts.

It can:

- 📈 Stream live FX prices from OANDA
- 🧮 Calculate technical and statistical indicators
- 🤖 Evaluate configurable trading strategies
- 💱 Automatically open, manage and close trades
- 🔗 Run pairs-trading strategies across correlated instruments
- 🧪 Backtest strategies against historical candle data
- 📊 Expose account, instrument and market-data information through an HTTP API
- 📡 Export telemetry through OpenTelemetry, Prometheus and Grafana
- 📧 Send email notifications for trading activity

The system is split into a **live trading worker** and a **Web API** for inspection and backtesting.

---

## How it works

At a high level, the live trading flow looks like this:

```text
             OANDA
               │
               │ Live prices
               ▼
      ┌──────────────────┐
      │OandaStreamService│
      └────────┬─────────┘
               │
               ▼
       ┌───────────────┐
       │StreamProcessor│
       └───────┬───────┘
               │
               ▼
      ┌──────────────────┐
      │ Indicators &     │
      │ Strategies       │
      └────────┬─────────┘
               │
               ▼
      ┌──────────────────┐
      │   TradeManager   │
      │/PairsTradeManager│
      └────────┬─────────┘
               │
               ▼
             OANDA
          Orders / Trades
```

Historical data follows a similar path through the backtesting API, except that the live market and order execution are replaced by historical candles and a simulation engine.

---

# Solution structure

The solution contains two .NET 10 applications:

| Project               | Type                 | Responsibility                                                 |
| --------------------- | -------------------- | -------------------------------------------------------------- |
| `src/Trading.Bot`     | .NET Worker Service  | Live market streaming, strategy evaluation and trade execution |
| `src/Trading.Bot.API` | ASP.NET Core Web API | Account/market inspection and historical strategy backtesting  |

Both applications share the indicator, strategy, model and OANDA client code contained in `Trading.Bot`.

---

# `Trading.Bot` — Live Trading

`Trading.Bot` is the continuously running worker responsible for interacting with OANDA and managing live trading.

## Services

The `Services/` directory contains the main trading infrastructure.

### `OandaApiService`

REST client for OANDA.

It provides access to:

- Historical candles
- Instruments
- Account information
- Orders
- Trades
- Other OANDA REST resources

### `OandaStreamService`

Connects to OANDA's streaming API and receives live price updates.

### `StreamProcessor` / `StreamWorker`

Process the incoming market stream and feed new market data into the trading system.

### `LiveTradeCache`

Maintains an in-memory view of:

- Live prices
- Open trades
- Other state required during live trading

### `TradeManager`

Handles single-instrument strategies.

It evaluates strategy signals and is responsible for executing and managing the resulting trades.

### `PairsTradeManager`

Handles pairs-trading strategies involving two correlated instruments.

The two legs are treated as a single trading unit, including:

- Entry
- Exit
- Position management
- Partial-fill handling
- Rollback when one leg cannot be completed

### `RolloverManager`

Optionally flattens open positions around the daily FX rollover period.

### `EmailService`

Sends email notifications for relevant trading events.

---

# Indicators & Strategies

The indicator and strategy library lives under:

```text
Trading.Bot/
└── Extensions/
    └── IndicatorExtensions/
```

Each indicator is implemented as an extension method over `Candle[]` and produces a sequence of calculated results.

Single-instrument strategies use:

```text
IndicatorResult[]
```

Pairs-trading strategies use:

```text
PairsIndicatorResult[]
```

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

The intention is to keep the indicator calculations reusable across both **live trading** and **backtesting**.

---

# Numerical & Statistical Calculations

Shared numerical operations are contained in:

```text
Trading.Bot/
└── Extensions/
    └── NumericExtensions.cs
```

These provide the mathematical building blocks used by the indicators and strategies, including:

- Moving averages
- Standard deviation
- Z-scores
- Correlation
- Beta
- Kalman filtering
- Winsorization
- Other statistical calculations

Keeping these operations separate means strategies can compose the same numerical primitives without duplicating the underlying calculations.

---

# Configuration

Application configuration lives under:

```text
Trading.Bot/
└── Configuration/
```

The main configuration types are:

- `TradeConfiguration`
- `TradeSettings`
- `EmailConfiguration`

OANDA-related constants are also defined here.

Configuration is bound from `appsettings.json`, with `appsettings.Development.json` providing local development overrides.

## `TradeConfiguration`

Controls the overall behaviour of the trading system, including:

- Trading mode
- Pairs-trading enablement
- Risk per trade
- Email notifications
- Instrument-specific trading settings

Each `TradeSettings` entry can define parameters such as:

- Candle granularity
- Indicator windows
- Indicator thresholds
- Maximum spread
- Risk/reward
- Trailing stops

This allows different instruments and strategies to be configured independently.

---

# `Trading.Bot.API` — Backtesting & Inspection

`Trading.Bot.API` provides an HTTP API for inspecting the account and market data, as well as running historical strategy simulations.

## Endpoints

| Method | Endpoint              | Description                                             |
| ------ | --------------------- | ------------------------------------------------------- |
| `GET`  | `/api/account`        | Returns an account summary                              |
| `GET`  | `/api/candles`        | Retrieves historical candles for an instrument          |
| `GET`  | `/api/instruments`    | Returns instrument metadata                             |
| `POST` | `/api/simulation/run` | Runs a strategy against uploaded historical candle data |

The API is intentionally separate from live trade execution. Its simulation endpoint can therefore be used to experiment with strategies without placing real orders.

---

# Strategy execution

The strategy implementations live under:

```text
Trading.Bot.API/
└── Mediator/
    └── Strategies/
```

There is one `IStrategy` implementation for each `StrategyType`.

A strategy takes a `RunStrategyRequest`, which contains the parameters required by that strategy, and connects the relevant indicator calculation to the simulation engine.

This keeps the strategy selection and configuration separate from the underlying indicator calculations and trading simulation.

---

# Backtesting

The backtesting engine is implemented in:

```text
Trading.Bot.API/
└── Extensions/
    └── BackTestingExtensions.cs
```

A simulation works by replaying historical candles and processing the resulting indicator values one bar at a time.

The simulator:

1. Calculates/replays indicator output for each bar.
2. Generates trading signals.
3. Opens and closes simulated positions.
4. Applies transaction costs.
5. Applies slippage.
6. Produces summary statistics.

The resulting `SimulationSummary` includes metrics such as:

- Balance
- Number of trades
- Win rate
- Other performance statistics

## Pairs-trading comparison

Pairs-trading strategies are evaluated in two ways during a simulation.

### Regime-aware strategy

Uses the strategy's classification of market regimes to determine whether a spread movement should be treated as a mean-reversion or continuation opportunity.

### Pure mean-reversion baseline

Treats qualifying spread deviations as mean-reversion opportunities regardless of the detected regime.

Both simulations use the **same transaction costs and slippage assumptions**, making the two approaches directly comparable.

---

# Running a backtest

## 1. Prepare historical data

Export historical candles for the required instrument(s) as CSV using the format expected by:

```text
GetObjectFromCsv<Candle>
```

For pairs strategies, provide the historical data required for both instruments.

## 2. Run the simulation

Send the candle CSV file(s) to:

```text
POST /api/simulation/run
```

The strategy is selected using the `StrategyType` query parameter.

Strategy-specific parameters are supplied through `RunStrategyRequest`, including values such as:

- `Ints`
- `Doubles`
- `MaxSpread`
- `TradeRisk`
- `TransactionCost`
- `Slippage`

See `RunStrategyRequest` for the complete parameter set.

## 3. Analyse the results

The API returns a ZIP archive containing:

```text
simulation-results.zip
├── Signal data
├── Simulated trades
└── Simulation summary
```

For pairs-trading strategies, the archive additionally contains:

```text
├── Regime-aware results
├── Pure mean-reversion results
└── Comparison summary
```

This makes it possible to inspect the individual signals and trades as well as the overall performance of the strategy.

---

# Observability

The repository includes a local observability stack under:

```text
observability/
└── docker-compose.yml
```

It provides:

- **OpenTelemetry Collector** — collects application telemetry
- **Prometheus** — stores metrics
- **Grafana** — visualises metrics
- **Preconfigured Grafana datasource** — connects Grafana to Prometheus

Start the stack with:

```bash
docker compose -f observability/docker-compose.yml up
```

The application also contains OpenTelemetry configuration under:

```text
Trading.Bot.API/
└── Diagnostics/
```

---

# Building & running

## Build the solution

```bash
dotnet build
```

## Run the live trading worker

```bash
dotnet run --project src/Trading.Bot
```

## Run the API

```bash
dotnet run --project src/Trading.Bot.API
```

Both applications also include Dockerfiles for containerised deployment:

```text
src/Trading.Bot/Dockerfile
src/Trading.Bot.API/Dockerfile
```

---

# Configuration & secrets

OANDA configuration includes:

- API key
- Account ID
- REST API base URL
- Streaming API base URL

Email configuration contains the SMTP settings required for trade notifications.

> ⚠️ **Never commit real API keys, passwords or other credentials to the repository.**

The committed values in `appsettings.json` are placeholders or practice-account values.

For CI/CD, sensitive values are supplied through **GitHub Actions secrets** and injected during the build/deployment process. Production secrets are handled in the same way and are not stored in the repository.

The relevant workflow is:

```text
.github/workflows/docker-image.yml
```

---

# Technology

The project is built around:

- **.NET 10**
- **C#**
- **ASP.NET Core Minimal APIs**
- **OANDA REST & streaming APIs**
- **OpenTelemetry**
- **Prometheus**
- **Grafana**
- **Docker**
- **GitHub Actions**

The core trading calculations are implemented directly in C# so that the same indicator and strategy logic can be used for both **live trading and historical backtesting**.

---

# Project at a glance

```text
ftb
│
├── src/
│   ├── Trading.Bot
│   │   │
│   │   ├── Services/
│   │   │   ├── OandaApiService
│   │   │   ├── OandaStreamService
│   │   │   ├── StreamProcessor
│   │   │   ├── TradeManager
│   │   │   ├── PairsTradeManager
│   │   │   └── RolloverManager
│   │   │
│   │   ├── Extensions/
│   │   │   ├── IndicatorExtensions/
│   │   │   └── NumericExtensions.cs
│   │   │
│   │   ├── Configuration/
│   │   └── Models/
│   │
│   └── Trading.Bot.API
│       │
│       ├── Endpoints/
│       ├── Mediator/
│       │   └── Strategies/
│       ├── Extensions/
│       │   └── BackTestingExtensions.cs
│       └── Diagnostics/
│
├── observability/
│   └── docker-compose.yml
│
└── .github/
    └── workflows/
```

In short, **`Trading.Bot` handles the live market and trading lifecycle, while `Trading.Bot.API` provides the tools needed to inspect data and test strategies against historical markets.**
