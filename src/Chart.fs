module Chart

open Feliz
open Types
open Engine

let private spainColor = "#e1492e"
let private ausColor = "#1f6feb"

let private colorOf country =
    match country with
    | Spain -> spainColor
    | Australia -> ausColor

/// SVG <text> with font styling applied via CSS (robust across Feliz versions).
let private label (x: float) (y: float) (anchor: string) (size: int) (weight: int) (color: string) (content: string) =
    Svg.text
        [ svg.x x
          svg.y y
          prop.custom ("textAnchor", anchor)
          svg.fill color
          prop.style [ style.fontSize size; style.fontWeight weight ]
          prop.text content ]

/// A grouped bar chart of after-tax, currency-adjusted value at the horizon.
/// One group per vehicle, two bars per group (Spain vs Australia).
let render (model: Model) (breakdowns: Breakdown list) : ReactElement =
    let width = 720.0
    let height = 380.0
    let padLeft = 64.0
    let padRight = 16.0
    let padTop = 28.0
    let padBottom = 56.0
    let plotW = width - padLeft - padRight
    let plotH = height - padTop - padBottom

    let vehicles = [ Cash; Equity; Property ]
    let baseCur = model.BaseCurrency

    let valueOf country vehicle =
        breakdowns
        |> List.tryFind (fun b -> b.Country = country && b.Vehicle = vehicle)
        |> Option.map (fun b -> b.BaseFinal)
        |> Option.defaultValue 0.0

    let maxVal =
        breakdowns
        |> List.map (fun b -> b.BaseFinal)
        |> List.fold max model.LumpSum
    let niceMax = (max 1.0 maxVal) * 1.12

    let groupW = plotW / float (List.length vehicles)
    let barW = groupW * 0.30
    let yOf v = padTop + plotH - (v / niceMax) * plotH

    // y-axis gridlines + labels at 0/25/50/75/100% of the scale
    let gridlines =
        [ for frac in [ 0.0; 0.25; 0.5; 0.75; 1.0 ] do
            let v = niceMax * frac
            let y = yOf v
            yield Svg.line
                [ svg.x1 padLeft
                  svg.y1 y
                  svg.x2 (padLeft + plotW)
                  svg.y2 y
                  svg.stroke "#e6e8ec"
                  svg.strokeWidth 1.0 ]
            yield label (padLeft - 8.0) (y + 4.0) "end" 11 400 "#8a93a3" (Format.money baseCur v) ]

    // dashed reference line at the initial capital, to show real growth
    let capitalLine =
        let y = yOf model.LumpSum
        [ Svg.line
            [ svg.x1 padLeft
              svg.y1 y
              svg.x2 (padLeft + plotW)
              svg.y2 y
              svg.stroke "#b0853a"
              svg.strokeWidth 1.5
              prop.custom ("strokeDasharray", "6 4") ]
          label (padLeft + plotW) (y - 6.0) "end" 11 600 "#b0853a"
              (sprintf "capital inicial · %s" (Format.money baseCur model.LumpSum)) ]

    let bar country vehicle gi =
        let v = valueOf country vehicle
        let center = padLeft + groupW * (float gi + 0.5)
        let x =
            match country with
            | Spain -> center - barW - 3.0
            | Australia -> center + 3.0
        let y = yOf v
        let h = max 0.0 (padTop + plotH - y)
        Svg.g
            [ prop.children
                [ Svg.rect
                    [ svg.x x
                      svg.y y
                      svg.width barW
                      svg.height h
                      svg.rx 3.0
                      svg.fill (colorOf country) ]
                  label (x + barW / 2.0) (y - 6.0) "middle" 10 600 "#3a4252" (Format.money baseCur v) ] ]

    let groups =
        [ for gi, vehicle in List.indexed vehicles do
            yield bar Spain vehicle gi
            yield bar Australia vehicle gi
            let center = padLeft + groupW * (float gi + 0.5)
            yield label center (padTop + plotH + 22.0) "middle" 13 600 "#2b3140" (Format.vehicleName vehicle) ]

    let axis =
        Svg.line
            [ svg.x1 padLeft
              svg.y1 (padTop + plotH)
              svg.x2 (padLeft + plotW)
              svg.y2 (padTop + plotH)
              svg.stroke "#c3c9d2"
              svg.strokeWidth 1.0 ]

    Svg.svg
        [ prop.custom ("viewBox", sprintf "0 0 %g %g" width height)
          prop.custom ("preserveAspectRatio", "xMidYMid meet")
          prop.role "img"
          prop.ariaLabel "Valor final después de impuestos por activo y país"
          prop.style [ style.width (length.percent 100); style.height length.auto ]
          prop.children (gridlines @ [ axis ] @ capitalLine @ groups) ]

/// Small colour legend used under the chart.
let legend: ReactElement =
    Html.div
        [ prop.className "legend"
          prop.children
            [ for (country, text) in [ Spain, "España (EUR)"; Australia, "Australia (AUD)" ] do
                Html.span
                    [ prop.className "legend-item"
                      prop.children
                        [ Html.span [ prop.className "swatch"; prop.style [ style.backgroundColor (colorOf country) ] ]
                          Html.span [ prop.text text ] ] ] ] ]
