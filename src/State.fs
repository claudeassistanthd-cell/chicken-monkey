module State

open Types

/// Sensible mid-2026 starting assumptions: ECB well below the RBA, ASX with a
/// higher dividend/franking profile than the Spanish indices, EUR base currency.
let init () : Model =
    { LumpSum = 100000.0
      HorizonYears = 10
      BaseCurrency = EUR
      FxSpot = 1.63
      FxHorizon = 1.63
      FxStressPct = 0.0
      Cash = { SpainRate = 0.020; AusRate = 0.043 }
      Equity =
        { SpainTotalReturn = 0.070
          SpainDividendYield = 0.032
          AusTotalReturn = 0.075
          AusDividendYield = 0.040 }
      Property =
        { SpainGrowth = 0.030
          SpainGrossYield = 0.045
          SpainCostRate = 0.015
          AusGrowth = 0.040
          AusGrossYield = 0.035
          AusCostRate = 0.013 }
      Spain =
        { ApplySavingsBands = true
          FlatRate = 0.21
          RentalRate = 0.24 }
      Australia =
        { MarginalRate = 0.45
          ApplyCgtDiscount = true
          ApplyFranking = true
          CompanyTaxRate = 0.30
          FrankingRatio = 0.80 }
      TaxesEnabled = true }

let private clampPct v = max 0.0 (min 1.0 v)

let update (msg: Msg) (m: Model) : Model =
    match msg with
    | SetLumpSum v -> { m with LumpSum = max 0.0 v }
    | SetHorizon v -> { m with HorizonYears = max 1 (min 40 v) }
    | SetBaseCurrency c -> { m with BaseCurrency = c }
    | SetFxSpot v -> { m with FxSpot = max 0.01 v }
    | SetFxHorizon v -> { m with FxHorizon = max 0.01 v }
    | SetFxStress v -> { m with FxStressPct = max -40.0 (min 40.0 v) }
    | SetCashSpain v -> { m with Cash = { m.Cash with SpainRate = v } }
    | SetCashAus v -> { m with Cash = { m.Cash with AusRate = v } }
    | SetEquitySpainReturn v -> { m with Equity = { m.Equity with SpainTotalReturn = v } }
    | SetEquitySpainDiv v -> { m with Equity = { m.Equity with SpainDividendYield = v } }
    | SetEquityAusReturn v -> { m with Equity = { m.Equity with AusTotalReturn = v } }
    | SetEquityAusDiv v -> { m with Equity = { m.Equity with AusDividendYield = v } }
    | SetPropSpainGrowth v -> { m with Property = { m.Property with SpainGrowth = v } }
    | SetPropSpainYield v -> { m with Property = { m.Property with SpainGrossYield = v } }
    | SetPropSpainCost v -> { m with Property = { m.Property with SpainCostRate = v } }
    | SetPropAusGrowth v -> { m with Property = { m.Property with AusGrowth = v } }
    | SetPropAusYield v -> { m with Property = { m.Property with AusGrossYield = v } }
    | SetPropAusCost v -> { m with Property = { m.Property with AusCostRate = v } }
    | ToggleSavingsBands -> { m with Spain = { m.Spain with ApplySavingsBands = not m.Spain.ApplySavingsBands } }
    | SetSpainFlatRate v -> { m with Spain = { m.Spain with FlatRate = clampPct v } }
    | SetSpainRentalRate v -> { m with Spain = { m.Spain with RentalRate = clampPct v } }
    | SetAusMarginal v -> { m with Australia = { m.Australia with MarginalRate = clampPct v } }
    | ToggleCgtDiscount -> { m with Australia = { m.Australia with ApplyCgtDiscount = not m.Australia.ApplyCgtDiscount } }
    | ToggleFranking -> { m with Australia = { m.Australia with ApplyFranking = not m.Australia.ApplyFranking } }
    | SetFrankingRatio v -> { m with Australia = { m.Australia with FrankingRatio = clampPct v } }
    | ToggleTaxes -> { m with TaxesEnabled = not m.TaxesEnabled }
    | Reset -> init ()
