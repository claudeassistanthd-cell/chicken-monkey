module View

open Feliz
open Types

let private parseFloat (s: string) =
    match System.Double.TryParse s with
    | true, v -> Some v
    | _ -> None

// --- small reusable controls ---

let private numberField (label: string) (unit': string) (step: float) (value: float) (onChange: float -> unit) =
    Html.label
        [ prop.className "field"
          prop.children
            [ Html.span [ prop.className "field-label"; prop.text label ]
              Html.div
                [ prop.className "field-input"
                  prop.children
                    [ Html.input
                        [ prop.type' "number"
                          prop.step step
                          prop.value value
                          prop.onChange (fun (s: string) -> parseFloat s |> Option.iter onChange) ]
                      if unit' <> "" then Html.span [ prop.className "unit"; prop.text unit' ] ] ] ] ]

/// A percentage field: the model stores a fraction, the user types a percent.
let private pctField (label: string) (value: float) (onChange: float -> unit) =
    numberField label "%" 0.1 (System.Math.Round(value * 100.0, 2)) (fun v -> onChange (v / 100.0))

let private slider (min': float) (max': float) (step: float) (value: float) (onChange: float -> unit) =
    Html.input
        [ prop.type' "range"
          prop.min min'
          prop.max max'
          prop.step step
          prop.value value
          prop.onChange (fun (s: string) -> parseFloat s |> Option.iter onChange) ]

let private toggle (label: string) (isOn: bool) (onToggle: unit -> unit) =
    Html.label
        [ prop.className (if isOn then "toggle on" else "toggle")
          prop.children
            [ Html.input
                [ prop.type' "checkbox"
                  prop.isChecked isOn
                  prop.onChange (fun (_: bool) -> onToggle ()) ]
              Html.span [ prop.className "switch" ]
              Html.span [ prop.text label ] ] ]

let private card (title: string) (children: ReactElement list) =
    Html.div
        [ prop.className "card"
          prop.children
            [ Html.h3 [ prop.className "card-title"; prop.text title ]
              Html.div [ prop.className "card-body"; prop.children children ] ] ]

/// A heading plus a Spain/Australia pair of percent fields.
let private pairPct (heading: string) (sLabel: string) (sVal: float) (sMsg: float -> Msg)
                    (aLabel: string) (aVal: float) (aMsg: float -> Msg) (dispatch: Msg -> unit) =
    Html.div
        [ prop.className "pair"
          prop.children
            [ Html.div [ prop.className "pair-heading"; prop.text heading ]
              Html.div
                [ prop.className "pair-grid"
                  prop.children
                    [ pctField sLabel sVal (sMsg >> dispatch)
                      pctField aLabel aVal (aMsg >> dispatch) ] ] ] ]

// --- input panels ---

let private globalsCard (m: Model) dispatch =
    card "Parámetros"
        [ numberField (sprintf "Capital inicial (%s)" (Format.symbol m.BaseCurrency)) "" 1000.0 m.LumpSum (SetLumpSum >> dispatch)
          Html.div
            [ prop.className "field"
              prop.children
                [ Html.span [ prop.className "field-label"; prop.text "Divisa base" ]
                  Html.div
                    [ prop.className "seg"
                      prop.children
                        [ for (c, lbl) in [ EUR, "EUR €"; AUD, "AUD A$" ] do
                            Html.button
                                [ prop.className (if c = m.BaseCurrency then "seg-btn active" else "seg-btn")
                                  prop.onClick (fun _ -> dispatch (SetBaseCurrency c))
                                  prop.text lbl ] ] ] ] ]
          Html.label
            [ prop.className "field"
              prop.children
                [ Html.span [ prop.className "field-label"; prop.text (sprintf "Horizonte: %d años" m.HorizonYears) ]
                  slider 1.0 40.0 1.0 (float m.HorizonYears) (fun v -> dispatch (SetHorizon(int v))) ] ]
          toggle "Aplicar impuestos (si no, comparación bruta)" m.TaxesEnabled (fun () -> dispatch ToggleTaxes) ]

let private fxCard (m: Model) dispatch =
    card "Tipo de cambio AUD↔EUR"
        [ numberField "Hoy (AUD por 1 EUR)" "" 0.01 m.FxSpot (SetFxSpot >> dispatch)
          numberField "Asunción al horizonte (AUD por 1 EUR)" "" 0.01 m.FxHorizon (SetFxHorizon >> dispatch)
          Html.p
            [ prop.className "hint"
              prop.text "El capital se convierte hoy al tipo «spot»; los resultados se reconvierten a la divisa base al tipo del horizonte. Usa el deslizador de estrés en los resultados para tensar ese tipo." ] ]

let private cashCard (m: Model) dispatch =
    card "Depósito / cuenta a plazo"
        [ pairPct "Tipo anual"
            "🇪🇸 España (BCE)" m.Cash.SpainRate SetCashSpain
            "🇦🇺 Australia (RBA)" m.Cash.AusRate SetCashAus dispatch ]

let private equityCard (m: Model) dispatch =
    card "Renta variable (índice amplio)"
        [ pairPct "Retorno total esperado · IBEX/Euro Stoxx vs ASX 200"
            "🇪🇸 España" m.Equity.SpainTotalReturn SetEquitySpainReturn
            "🇦🇺 Australia" m.Equity.AusTotalReturn SetEquityAusReturn dispatch
          pairPct "Del cual, rentabilidad por dividendo"
            "🇪🇸 España" m.Equity.SpainDividendYield SetEquitySpainDiv
            "🇦🇺 Australia" m.Equity.AusDividendYield SetEquityAusDiv dispatch ]

let private propertyCard (m: Model) dispatch =
    card "Inmobiliario residencial"
        [ pairPct "Revalorización anual"
            "🇪🇸 España" m.Property.SpainGrowth SetPropSpainGrowth
            "🇦🇺 Australia" m.Property.AusGrowth SetPropAusGrowth dispatch
          pairPct "Rentabilidad bruta por alquiler"
            "🇪🇸 España" m.Property.SpainGrossYield SetPropSpainYield
            "🇦🇺 Australia" m.Property.AusGrossYield SetPropAusYield dispatch
          pairPct "Costes anuales (% del valor)"
            "🇪🇸 España" m.Property.SpainCostRate SetPropSpainCost
            "🇦🇺 Australia" m.Property.AusCostRate SetPropAusCost dispatch ]

let private spainTaxCard (m: Model) dispatch =
    card "Fiscalidad · España (IRPF)"
        [ toggle "Tramos del ahorro (19–28%)" m.Spain.ApplySavingsBands (fun () -> dispatch ToggleSavingsBands)
          (if not m.Spain.ApplySavingsBands then
                pctField "Tipo plano del ahorro" m.Spain.FlatRate (SetSpainFlatRate >> dispatch)
           else
                Html.p [ prop.className "hint"; prop.text "Intereses, dividendos y plusvalías tributan en la base del ahorro con tramos progresivos." ])
          pctField "Tipo sobre rentas de alquiler" m.Spain.RentalRate (SetSpainRentalRate >> dispatch) ]

let private ausTaxCard (m: Model) dispatch =
    card "Fiscalidad · Australia"
        [ pctField "Tipo marginal (renta/intereses/alquiler)" m.Australia.MarginalRate (SetAusMarginal >> dispatch)
          toggle "Descuento CGT del 50% (>12 meses)" m.Australia.ApplyCgtDiscount (fun () -> dispatch ToggleCgtDiscount)
          toggle "Créditos de imputación (franking)" m.Australia.ApplyFranking (fun () -> dispatch ToggleFranking)
          (if m.Australia.ApplyFranking then
                pctField "Proporción de dividendos con franking" m.Australia.FrankingRatio (SetFrankingRatio >> dispatch)
           else
                Html.none) ]

// --- results ---

let private verdictBanner (m: Model) (v: Engine.Verdict) =
    Html.div
        [ prop.className "verdict"
          prop.children
            [ Html.div [ prop.className "verdict-flag"; prop.text (Format.flag v.Winner.Country) ]
              Html.div
                [ prop.className "verdict-text"
                  prop.children
                    [ Html.div
                        [ prop.className "verdict-title"
                          prop.text (sprintf "%s gana por %s" (Format.countryName v.Winner.Country) (Format.signedPct v.MarginPct)) ]
                      Html.div
                        [ prop.className "verdict-sub"
                          prop.text
                            (sprintf "Mejor opción: %s en %s · %s"
                                (Format.vehicleName v.Winner.Vehicle)
                                (Format.countryName v.Winner.Country)
                                (Format.money m.BaseCurrency v.Winner.BaseFinal)) ]
                      Html.div
                        [ prop.className "verdict-sub muted"
                          prop.text
                            (sprintf "frente a %s en %s · %s"
                                (Format.vehicleName v.Runner.Vehicle)
                                (Format.countryName v.Runner.Country)
                                (Format.money m.BaseCurrency v.Runner.BaseFinal)) ] ] ] ] ]

let private fxStress (m: Model) dispatch =
    let eff = Engine.effectiveFxHorizon m
    Html.div
        [ prop.className "fx-stress"
          prop.children
            [ Html.div
                [ prop.className "fx-stress-head"
                  prop.children
                    [ Html.span [ prop.text "Estrés del tipo de cambio" ]
                      Html.strong
                        [ prop.text (sprintf "%g AUD/EUR (%s)" (System.Math.Round(eff, 3)) (Format.signedPct m.FxStressPct)) ] ] ]
              slider -40.0 40.0 1.0 m.FxStressPct (SetFxStress >> dispatch)
              Html.div
                [ prop.className "fx-scale"
                  prop.children
                    [ Html.span [ prop.text "EUR más fuerte −40%" ]
                      Html.span [ prop.text "+40% AUD más fuerte" ] ] ] ] ]

let private rankingTable (m: Model) (v: Engine.Verdict) =
    Html.table
        [ prop.className "rank"
          prop.children
            [ Html.thead
                [ Html.tr
                    [ Html.th [ prop.text "#" ]
                      Html.th [ prop.text "Activo" ]
                      Html.th [ prop.text "País" ]
                      Html.th [ prop.className "num"; prop.text (sprintf "Valor (%s)" (Format.symbol m.BaseCurrency)) ]
                      Html.th [ prop.className "num"; prop.text "Mult." ] ] ]
              Html.tbody
                [ for i, b in List.indexed v.Ranked do
                    let mult = if m.LumpSum > 0.0 then b.BaseFinal / m.LumpSum else 0.0
                    Html.tr
                        [ prop.className (if i = 0 then "top" else "")
                          prop.children
                            [ Html.td [ prop.text (string (i + 1)) ]
                              Html.td [ prop.text (Format.vehicleName b.Vehicle) ]
                              Html.td [ prop.text (sprintf "%s %s" (Format.flag b.Country) (Format.countryName b.Country)) ]
                              Html.td [ prop.className "num"; prop.text (Format.money m.BaseCurrency b.BaseFinal) ]
                              Html.td [ prop.className "num"; prop.text (sprintf "%.2fx" mult) ] ] ] ] ] ]

let private resultsColumn (m: Model) dispatch =
    let breakdowns = Engine.allBreakdowns m
    let v = Engine.verdict m
    Html.div
        [ prop.className "results"
          prop.children
            [ verdictBanner m v
              card "Valor final · después de impuestos y divisa"
                [ Chart.render m breakdowns
                  Chart.legend
                  fxStress m dispatch ]
              card "Ranking" [ rankingTable m v ] ] ]

// --- top-level ---

let render (m: Model) (dispatch: Msg -> unit) : ReactElement =
    Html.div
        [ prop.className "app"
          prop.children
            [ Html.header
                [ prop.className "topbar"
                  prop.children
                    [ Html.div
                        [ prop.className "brand"
                          prop.children
                            [ Html.h1 [ prop.text "¿Dónde invierto?" ]
                              Html.p [ prop.text "España vs Australia · depósito, bolsa e inmobiliario, neto de impuestos y divisa" ] ] ]
                      Html.button
                        [ prop.className "ghost"
                          prop.onClick (fun _ -> dispatch Reset)
                          prop.text "Restablecer" ] ] ]
              Html.main
                [ prop.className "layout"
                  prop.children
                    [ Html.div
                        [ prop.className "inputs"
                          prop.children
                            [ globalsCard m dispatch
                              fxCard m dispatch
                              cashCard m dispatch
                              equityCard m dispatch
                              propertyCard m dispatch
                              spainTaxCard m dispatch
                              ausTaxCard m dispatch ] ]
                      resultsColumn m dispatch ] ]
              Html.footer
                [ prop.className "foot"
                  prop.text "Herramienta educativa con supuestos simplificados (tramos del ahorro IRPF; descuento CGT y franking australianos). No es asesoramiento fiscal ni de inversión." ] ] ]
