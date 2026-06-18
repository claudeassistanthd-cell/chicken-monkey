module Tax

open Types

/// Progressive tax on a positive amount of income given a list of
/// (upperBound, marginalRate) bands ordered from lowest band up.
let private progressive (bands: (float * float) list) (income: float) =
    if income <= 0.0 then
        0.0
    else
        let mutable remaining = income
        let mutable lower = 0.0
        let mutable tax = 0.0
        for (upper, rate) in bands do
            if remaining > 0.0 then
                let span = max 0.0 (upper - lower)
                let taxed = min remaining span
                tax <- tax + taxed * rate
                remaining <- remaining - taxed
                lower <- upper
        tax

/// IRPF "renta del ahorro" bands (EUR thresholds, 2024 scale).
let spainSavingsBands: (float * float) list =
    [ 6000.0, 0.19
      50000.0, 0.21
      200000.0, 0.23
      300000.0, 0.27
      System.Double.PositiveInfinity, 0.28 ]

/// Spanish savings-income tax (interest, dividends, capital gains).
let spainSavingsTax (spain: SpainTax) (income: float) =
    if income <= 0.0 then 0.0
    elif spain.ApplySavingsBands then progressive spainSavingsBands income
    else income * spain.FlatRate

/// Australian tax on ordinary income (interest, rent) at the marginal rate.
let ausOrdinaryTax (aus: AustraliaTax) (income: float) =
    if income <= 0.0 then 0.0 else income * aus.MarginalRate

/// Australian dividend tax accounting for franking credits.
/// Returns the *net tax payable* on the cash dividend, which can be negative
/// (a refund) when franking credits exceed the marginal liability.
let ausDividendTax (aus: AustraliaTax) (cashDividend: float) =
    if cashDividend <= 0.0 then
        0.0
    elif not aus.ApplyFranking then
        cashDividend * aus.MarginalRate
    else
        let frankedPart = cashDividend * aus.FrankingRatio
        // credit attached to a fully franked dividend = cash * t/(1-t)
        let credit = frankedPart * (aus.CompanyTaxRate / (1.0 - aus.CompanyTaxRate))
        let grossedUp = cashDividend + credit
        grossedUp * aus.MarginalRate - credit

/// Australian capital gains tax, with the optional 50% long-term discount.
let ausCgt (aus: AustraliaTax) (gain: float) =
    if gain <= 0.0 then
        0.0
    else
        let taxable = if aus.ApplyCgtDiscount then gain * 0.5 else gain
        taxable * aus.MarginalRate
