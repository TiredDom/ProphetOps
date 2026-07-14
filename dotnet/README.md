# ProphetOps — .NET migration (in progress)

The backend is being ported Laravel/PHP → ASP.NET Core (C#). The Laravel app stays
the running reference ("oracle") until each module reaches proven parity, then we cut over.
See `information/dotnet-implementation-pivot.md` for the full plan.

## Task 1 — Holt-Winters forecasting engine (parity port)

**Goal / definition of done:** the C# engine reproduces the original PHP engine
(`app/Support/HoltWintersForecaster.php`) *exactly* on the same input — same tuned
α/β/γ, forecasts, prediction intervals, metrics, and baselines — with `dotnet test` green.

| Item | State |
| --- | --- |
| PHP oracle captured (`ProphetOps.Forecasting.Tests/oracle.json`) | ✅ |
| `ProphetOps.Forecasting/HoltWintersForecaster.cs` (line-for-line port) | ✅ |
| Parity + behavioural tests | ✅ |
| `dotnet test` passes | ✅ **8/8 green** (.NET SDK 8.0.422) — C# output matches the PHP oracle to 1e-6 |

### Run it
Requires the **.NET 8 SDK** (`dotnet --version` should print 8.x). Then:

```bash
cd dotnet
dotnet test ProphetOps.Forecasting.Tests/ProphetOps.Forecasting.Tests.csproj
```

### Re-capturing the oracle
If the PHP engine changes, regenerate the fixture (PHP required):

```bash
php <harness that calls \App\Support\HoltWintersForecaster::forecast on the fixed
    seasonal/declining/tooShort series> > ProphetOps.Forecasting.Tests/oracle.json
```

### Parity notes (why the port is faithful, not just similar)
- **Rounding:** PHP `round()` is half-**away-from-zero**; C#'s default is banker's
  rounding. The port uses `MidpointRounding.AwayFromZero` everywhere.
- **Grid search:** same α/β/γ iteration order and strict `<` tie-break, so the same
  parameter combination is selected.
