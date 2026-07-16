# Demand Forecasting — Holt-Winters Algorithm

This document explains the forecasting **algorithm** used by ProphetOps, for the
methodology chapter and the defense. It is written so the procedure can be
examined step by step — the system implements the computation itself rather than
calling a pre-built forecasting model or library.

## Why an algorithm, not a model

The forecasting requirement is satisfied by **Holt-Winters (Triple Exponential
Smoothing)**, a deterministic, fully transparent procedure that the team
implemented from first principles in PHP
(`app/Support/HoltWintersForecaster.php`). There is no external statistical
package and nothing pre-trained: every value the system produces can be traced
back to the four equations below. This is the distinction between *applying an
algorithm we own and can defend* and *consuming a black-box model*.

## The components

Holt-Winters keeps three smoothed quantities and updates them one period at a
time as it walks through the monthly history:

- **Level (L)** — the current baseline level of revenue.
- **Trend (T)** — the direction and size of period-to-period change.
- **Seasonal (S)** — the repeating yearly pattern (e.g. summer and December peaks).

`m` is the season length (12 for monthly data).

## The equations

For each month *t* (additive formulation):

```
Level     L_t = α·(y_t − S_{t−m}) + (1 − α)·(L_{t−1} + T_{t−1})
Trend     T_t = β·(L_t − L_{t−1}) + (1 − β)·T_{t−1}
Season    S_t = γ·(y_t − L_t)     + (1 − γ)·S_{t−m}
Forecast  ŷ_{t+h} = L_t + h·T_t + S_{t+h−m}
```

Each line is a **weighted average**: blend the newest observation with the
previous estimate. The constants α, β, γ (each between 0 and 1) control how
quickly each component reacts to new data.

## Initialisation

The recurrence is seeded from the first two seasonal cycles:

- `L = mean(first m months)`
- `T = (mean(months m..2m−1) − mean(first m months)) / m`
- `S_i = y_i − L` for the first `m` months

This is why at least **two full cycles (24 monthly records)** are required before
a seasonal forecast is meaningful.

## Parameter tuning (automatic)

α, β and γ are not guessed. The system **grid-searches** them and selects the
combination that minimises the **Mean Absolute Error (MAE)** on a held-out
validation window (the most recent months are hidden during fitting, then the
model is judged on them). The chosen model is finally refit on the full history
before projecting forward. The selected parameters and the validation window are
shown on the Forecast screen. When fewer than roughly 28 months are available (too
few to hold out a full validation window), the search falls back to minimising
in-sample error instead of a held-out one.

## Evaluation

Accuracy is reported with three standard measures over the in-sample one-step
forecasts:

- **MAE** — average peso error.
- **RMSE** — root mean squared error (penalises large misses).
- **MAPE** — average percentage error (the "model accuracy" figure is `100 − MAPE`).

Because these three measures are computed on the same history the model was fit on,
they describe **in-sample fit** — how faithfully the algorithm reproduces the known
months — which is why the screen labels the headline figure "in-sample fit" rather
than out-of-sample accuracy. The held-out validation window above is used only to
choose α, β and γ.

The forecast is also compared against two **naive baselines** — "same month last
year" (seasonal-naive) and "same as last month" (naive). A credible algorithm
should beat both, and the screen reports the percentage improvement.

## Prediction interval

A shaded band around the forecast shows uncertainty. It is built from the model's
residual error (RMSE) and widens with the horizon (≈ √h), giving roughly an 80%
band. This is an assumption-based interval, disclosed as a limitation.

## Plain-language insight (the interpretation layer)

The Forecast screen and dashboard show a plain-language reading of the algorithm's
output: the projected direction and size of change, the strongest month ahead, and a
recommended review action. This interpretation layer
(`app/Support/ForecastInsight.php`) only rephrases the computed forecast in business
terms — it is not a separate predictive model and adds no new prediction.

## Limitations (for the paper)

- Accuracy depends on the consistency and length of the agency's historical data.
- The current screen uses a **representative sample** monthly history; it must be
  replaced with the agency's digitised monthly records for production use.
- Forecasts assume historical patterns continue and do not account for
  macroeconomic shocks or one-off events.
- The additive formulation is used; a multiplicative variant (seasonal swings that
  scale with the level) is a documented future extension.
- The prediction interval assumes the residual errors are roughly normal
  (z ≈ 1.28 for the ≈80% band); heavy-tailed errors would make it optimistic.
- Only a single aggregate revenue series is modelled — there is no per-package or
  per-destination forecast yet.
- No external drivers are used: promotions, holidays and weather are not regressors;
  the algorithm sees only the past revenue values.
- Because the current series is a deterministic sample, the reported metrics show
  that the algorithm faithfully **recovers a known pattern** (an implementation
  sanity check); they are not a claim of real-world accuracy, which requires the
  agency's actual digitised records.

## Where it lives in the code

| Concern | File |
| --- | --- |
| Algorithm (level/trend/season, tuning, metrics, baselines) | `app/Support/HoltWintersForecaster.php` |
| Sample monthly history | `App\Support\ProphetOpsData::salesHistory()` |
| Page controller (forecast, chart series, insight) | `app/Http/Controllers/ForecastController.php` |
| Forecast screen | `resources/js/Pages/Forecast.vue` |
| Algorithm tests | `tests/Feature/HoltWintersForecasterTest.php` |

## Why a single forecasting algorithm (not TOPSIS or Prophet)

The study deliberately ships **one** decision-support algorithm rather than a pair
or a pre-built model:

- **Why not Meta/Facebook Prophet** — Prophet is a pre-built statistical *model*
  (a library the team would only configure and call). The adviser's requirement is
  an *algorithm the team implements and can defend* line by line, which Holt-Winters
  satisfies and Prophet does not.
- **Why not TOPSIS** — TOPSIS is a multi-criteria *ranking* method for choosing
  between fixed options, not a demand forecast. It answered a different question and
  was removed to keep the capstone focused on one examinable forecasting algorithm.
  A prescriptive ranking layer remains possible future work, not current scope.

The result is one transparent, hand-derivable procedure — Holt-Winters — end to end,
from raw monthly revenue to the projected outlook.
