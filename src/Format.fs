module Format

open Types

/// Format a number with thousands separators, rounded to the nearest unit.
let thousands (v: float) =
    let n = int64 (System.Math.Round(abs v))
    let s = string n
    let sb = System.Text.StringBuilder()
    let len = s.Length
    s
    |> Seq.iteri (fun i c ->
        if i > 0 && (len - i) % 3 = 0 then sb.Append(',') |> ignore
        sb.Append(c) |> ignore)
    let body = sb.ToString()
    if v < 0.0 then "-" + body else body

let symbol =
    function
    | EUR -> "€"
    | AUD -> "A$"

/// Money in a given currency, e.g. "€123,456".
let money (cur: Currency) (v: float) = symbol cur + thousands v

/// A fraction rendered as a percentage, e.g. 0.043 -> "4.3%".
let pct (v: float) = sprintf "%.1f%%" (v * 100.0)

/// A percentage *value* (already in 0..100 scale) with an explicit sign.
let signedPct (v: float) =
    if v >= 0.0 then sprintf "+%.1f%%" v
    else sprintf "%.1f%%" v

let countryName =
    function
    | Spain -> "España"
    | Australia -> "Australia"

let vehicleName =
    function
    | Cash -> "Depósito"
    | Equity -> "Renta variable"
    | Property -> "Inmobiliario"

let flag =
    function
    | Spain -> "🇪🇸"
    | Australia -> "🇦🇺"
