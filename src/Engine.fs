module Engine

open Types

/// After-tax, currency-adjusted outcome for one (country, vehicle).
type Breakdown =
    { Vehicle: Vehicle
      Country: Country
      LocalFinal: float  // after-tax value in the country's local currency
      BaseFinal: float } // the same value converted to the chosen base currency

/// Local settlement currency of a country.
let localCurrency country =
    match country with
    | Spain -> EUR
    | Australia -> AUD

/// Convert an amount between currencies given an AUD-per-EUR rate.
let convert (amount: float) (from: Currency) (target: Currency) (audPerEur: float) =
    match from, target with
    | EUR, AUD -> amount * audPerEur
    | AUD, EUR -> amount / audPerEur
    | _ -> amount

/// The horizon FX rate after applying the stress-test slider.
let effectiveFxHorizon (m: Model) =
    m.FxHorizon * (1.0 + m.FxStressPct / 100.0)

// --- Tax dispatch (honouring the master pre-tax/after-tax toggle) ---

let private incomeTax (m: Model) country (amount: float) =
    if not m.TaxesEnabled then 0.0
    else
        match country with
        | Spain -> Tax.spainSavingsTax m.Spain amount
        | Australia -> Tax.ausOrdinaryTax m.Australia amount

let private dividendTax (m: Model) country (amount: float) =
    if not m.TaxesEnabled then 0.0
    else
        match country with
        | Spain -> Tax.spainSavingsTax m.Spain amount
        | Australia -> Tax.ausDividendTax m.Australia amount

let private capitalGainsTax (m: Model) country (gain: float) =
    if not m.TaxesEnabled then 0.0
    else
        match country with
        | Spain -> Tax.spainSavingsTax m.Spain gain
        | Australia -> Tax.ausCgt m.Australia gain

let private rentalTax (m: Model) country (amount: float) =
    if not m.TaxesEnabled then 0.0
    else
        match country with
        | Spain -> if amount <= 0.0 then 0.0 else amount * m.Spain.RentalRate
        | Australia -> Tax.ausOrdinaryTax m.Australia amount

// --- Cash / term deposit: interest taxed annually as it is earned ---
let private cashFinal (m: Model) country (start: float) (rate: float) =
    let mutable v = start
    for _ in 1 .. m.HorizonYears do
        let interest = v * rate
        let tax = incomeTax m country interest
        v <- v + interest - tax
    v

/// Approximate after-tax annual cash rate, used to grow accumulated rent.
let private afterTaxCashRate (m: Model) country (rate: float) =
    if not m.TaxesEnabled then
        rate
    else
        match country with
        | Spain ->
            // first IRPF savings band as a representative marginal rate
            if m.Spain.ApplySavingsBands then rate * (1.0 - 0.21)
            else rate * (1.0 - m.Spain.FlatRate)
        | Australia -> rate * (1.0 - m.Australia.MarginalRate)

// --- Equity index: dividends taxed annually & reinvested net of tax;
//     capital gains taxed once, on realisation at the horizon ---
let private equityFinal (m: Model) country (start: float) (totalReturn: float) (divYield: float) =
    let growth = max 0.0 (totalReturn - divYield)
    let mutable v = start
    let mutable basis = start
    for _ in 1 .. m.HorizonYears do
        let dividend = v * divYield
        let dtax = dividendTax m country dividend
        let netDiv = dividend - dtax
        v <- v * (1.0 + growth) + netDiv
        basis <- basis + netDiv // reinvested net dividends lift the cost basis
    let gain = v - basis
    let cgt = capitalGainsTax m country gain
    v - cgt

// --- Residential property: net rent accumulates in a cash sidecar growing at
//     the after-tax cash rate; capital gain taxed once at the horizon ---
let private propertyFinal (m: Model) country (start: float) (growth: float) (grossYield: float) (costRate: float) =
    let netYield = grossYield - costRate
    let cashRate =
        match country with
        | Spain -> m.Cash.SpainRate
        | Australia -> m.Cash.AusRate
    let sidecarRate = afterTaxCashRate m country cashRate
    let mutable propVal = start
    let mutable sidecar = 0.0
    for _ in 1 .. m.HorizonYears do
        let netRent = propVal * netYield
        let tax = rentalTax m country netRent
        let afterRent = netRent - tax
        sidecar <- sidecar * (1.0 + sidecarRate) + afterRent
        propVal <- propVal * (1.0 + growth)
    let gain = propVal - start
    let cgt = capitalGainsTax m country gain
    propVal - cgt + sidecar

let private startLocal (m: Model) country =
    convert m.LumpSum m.BaseCurrency (localCurrency country) m.FxSpot

let private toBase (m: Model) country (localFinal: float) =
    convert localFinal (localCurrency country) m.BaseCurrency (effectiveFxHorizon m)

/// Compute one outcome.
let computeOne (m: Model) (country: Country) (vehicle: Vehicle) : Breakdown =
    let start = startLocal m country
    let localFinal =
        match country, vehicle with
        | Spain, Cash -> cashFinal m Spain start m.Cash.SpainRate
        | Australia, Cash -> cashFinal m Australia start m.Cash.AusRate
        | Spain, Equity -> equityFinal m Spain start m.Equity.SpainTotalReturn m.Equity.SpainDividendYield
        | Australia, Equity -> equityFinal m Australia start m.Equity.AusTotalReturn m.Equity.AusDividendYield
        | Spain, Property -> propertyFinal m Spain start m.Property.SpainGrowth m.Property.SpainGrossYield m.Property.SpainCostRate
        | Australia, Property -> propertyFinal m Australia start m.Property.AusGrowth m.Property.AusGrossYield m.Property.AusCostRate
    { Vehicle = vehicle
      Country = country
      LocalFinal = localFinal
      BaseFinal = toBase m country localFinal }

/// All six outcomes (3 vehicles x 2 countries).
let allBreakdowns (m: Model) : Breakdown list =
    [ for country in [ Spain; Australia ] do
        for vehicle in [ Cash; Equity; Property ] do
            yield computeOne m country vehicle ]

type Verdict =
    { Ranked: Breakdown list // all six, best first, in base currency
      Winner: Breakdown
      Runner: Breakdown      // best option of the *other* country
      MarginPct: float }     // how much the winner beats the runner, in %

let verdict (m: Model) : Verdict =
    let all = allBreakdowns m |> List.sortByDescending (fun b -> b.BaseFinal)
    let winner = List.head all
    let runner =
        all
        |> List.filter (fun b -> b.Country <> winner.Country)
        |> List.head
    let margin =
        if runner.BaseFinal <> 0.0 then
            (winner.BaseFinal - runner.BaseFinal) / runner.BaseFinal * 100.0
        else 0.0
    { Ranked = all
      Winner = winner
      Runner = runner
      MarginPct = margin }
