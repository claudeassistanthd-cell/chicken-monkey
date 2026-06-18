# ¿Dónde invierto? — España vs Australia

A single-page **F#/Fable** app (Elmish MVU + Feliz) that compares investing a
lump sum in **Spain** vs **Australia** across three vehicles, applies each
country's tax treatment, converts everything to a chosen base currency, and
ranks the outcomes at your time horizon.

You set a lump sum, a horizon and a base currency, plus an **AUD↔EUR**
assumption, and the app compares — side by side, in each country:

- **Cash / term deposit** at a configurable rate (ECB-linked vs RBA-linked — the
  rates that differ so much right now).
- **Broad equity index** — IBEX 35 / Euro Stoxx vs ASX 200, each at an expected
  total return and dividend yield you set.
- **Residential property** with a rental yield minus running costs.

Then it applies each country's tax rules as **toggleable** options, converts
every outcome to the base currency, and shows:

- a **grouped bar chart** of after-tax, currency-adjusted value at the horizon,
- a **ranked verdict** ("Australia gana por X%"),
- and an **exchange-rate stress slider** to tension the horizon FX assumption.

## Tax model (simplified, toggleable)

**Spain — IRPF.** Interest, dividends and capital gains fall in the *renta del
ahorro* base and share the progressive savings bands (19 / 21 / 23 / 27 / 28%).
A toggle switches between the bands and a single flat rate. Rental income is
modelled with its own flat rate (general base).

**Australia.** Interest and rent are taxed at a configurable marginal rate.
Dividends can carry **franking credits** (gross-up at the company tax rate, then
credit against the marginal liability — refundable if it exceeds it). Capital
gains get the **50% CGT discount** for assets held over 12 months. Each of these
is an independent toggle.

A master **"apply taxes"** switch flips the whole comparison to pre-tax.

## Currency

The lump sum is converted to each country's local currency at today's **spot**
rate, invested, then the proceeds are converted back to the base currency at the
**horizon** rate. The stress slider tenses that horizon rate by ±40%, so FX risk
actually moves the result (rather than cancelling out).

> Educational tool with deliberately simplified assumptions. Not tax or
> investment advice.

## Project layout

```
index.html            Vite entry point
style.css             Styling
vite.config.js        Vite config (bundles Fable's JS output)
.config/              dotnet local tool manifest (Fable)
src/
  App.fsproj          F# project + NuGet (Fable) package references
  Types.fs            Domain model & messages
  Format.fs           Number / currency / label formatting
  Tax.fs              Spanish IRPF bands, Australian franking & CGT
  Engine.fs           Per-vehicle compounding + FX conversion + verdict
  State.fs            Elmish init / update
  Chart.fs            Grouped bar chart (hand-rolled SVG via Feliz)
  View.fs             Elmish view (inputs + results)
  Main.fs             Program bootstrap
```

## Running it

Requires the **.NET SDK** (8.0+) and **Node.js** (18+). Fable compiles the F# to
JavaScript, then Vite bundles and serves it.

```bash
# 1. install JS deps (react, react-dom, vite)
npm install

# 2. restore the local Fable dotnet tool
npm run restore      # = dotnet tool restore

# 3. dev server with hot reload (Fable watch + Vite)
npm start            # → http://localhost:5173  (runs restore first)

# 4. production build
npm run build        # → dist/  (runs restore first)
```

`npm start` and `npm run build` both run `dotnet tool restore` automatically via
their `pre*` scripts, so step 2 is only needed if you want to restore up front.
