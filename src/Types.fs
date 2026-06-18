module Types

/// The two currencies in play. Spain settles in EUR, Australia in AUD.
type Currency =
    | EUR
    | AUD

/// The two jurisdictions being compared.
type Country =
    | Spain
    | Australia

/// The three investment vehicles compared side by side in each country.
type Vehicle =
    | Cash
    | Equity
    | Property

/// Spanish IRPF treatment. Interest, dividends and capital gains all fall in
/// the "renta del ahorro" (savings income) base and share the progressive bands.
/// Rental income falls in the general base, modelled here as a single flat rate.
type SpainTax =
    { ApplySavingsBands: bool // progressive savings bands vs a single flat rate
      FlatRate: float         // used when ApplySavingsBands = false
      RentalRate: float }     // rental income (general base, simplified)

/// Australian treatment. Ordinary income (interest, rent) taxed at the marginal
/// rate; dividends may carry franking credits; capital gains get a 50% discount
/// when held longer than 12 months.
type AustraliaTax =
    { MarginalRate: float    // ordinary income / interest / rent
      ApplyCgtDiscount: bool // 50% discount on assets held > 12 months
      ApplyFranking: bool    // gross-up + franking credit on dividends
      CompanyTaxRate: float  // company tax used for the franking gross-up (0.30)
      FrankingRatio: float } // 0..1, share of dividends that are franked

type CashInputs =
    { SpainRate: float // term deposit, ECB-linked
      AusRate: float } // term deposit, RBA-linked

type EquityInputs =
    { SpainTotalReturn: float    // IBEX 35 / Euro Stoxx, total return
      SpainDividendYield: float  // of which is paid as dividends
      AusTotalReturn: float      // ASX 200, total return
      AusDividendYield: float }  // of which is paid as dividends

type PropertyInputs =
    { SpainGrowth: float     // capital appreciation
      SpainGrossYield: float // gross rental yield
      SpainCostRate: float   // running costs as % of value
      AusGrowth: float
      AusGrossYield: float
      AusCostRate: float }

type Model =
    { LumpSum: float          // invested today, in BaseCurrency
      HorizonYears: int
      BaseCurrency: Currency
      FxSpot: float           // AUD per 1 EUR, today
      FxHorizon: float        // AUD per 1 EUR, assumed at the horizon
      FxStressPct: float      // -40..+40, stress applied to FxHorizon
      Cash: CashInputs
      Equity: EquityInputs
      Property: PropertyInputs
      Spain: SpainTax
      Australia: AustraliaTax
      TaxesEnabled: bool }    // master pre-tax / after-tax toggle

type Msg =
    | SetLumpSum of float
    | SetHorizon of int
    | SetBaseCurrency of Currency
    | SetFxSpot of float
    | SetFxHorizon of float
    | SetFxStress of float
    | SetCashSpain of float
    | SetCashAus of float
    | SetEquitySpainReturn of float
    | SetEquitySpainDiv of float
    | SetEquityAusReturn of float
    | SetEquityAusDiv of float
    | SetPropSpainGrowth of float
    | SetPropSpainYield of float
    | SetPropSpainCost of float
    | SetPropAusGrowth of float
    | SetPropAusYield of float
    | SetPropAusCost of float
    | ToggleSavingsBands
    | SetSpainFlatRate of float
    | SetSpainRentalRate of float
    | SetAusMarginal of float
    | ToggleCgtDiscount
    | ToggleFranking
    | SetFrankingRatio of float
    | ToggleTaxes
    | Reset
