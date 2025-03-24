namespace MicaStudio.Utilities

open System;
open System.Globalization

module ColourUtilities =
    // Convert a hexadecimal string to a byte
    let private StringToByte (s: string): byte = Byte.Parse(s, NumberStyles.AllowHexSpecifier)

    // Convert a hexadecimal RGB colour to a byte
    let HexToByte (colour: string): int = 
        if colour.Length >= 6 then
            let start = if colour.StartsWith '#' then 1 else 0
            let r = StringToByte (colour.Substring(start, 2)) |> int
            let g = StringToByte (colour.Substring(start + 2, 2)) |> int
            let b = StringToByte (colour.Substring(start + 4, 2)) |> int

            r ||| (g <<< 8) ||| (b <<< 16) 
        else failwith ("Invalid Hex colour " + colour)

    (* Similair recursive implementation with pattern matching done to learn F# features
    let rec HexToByte (colour: string): int = 
        match colour[0] with
        | '#' -> HexToByte (colour.Substring 1) // Remove leading '#' symbol
        | _  when colour.Length >= 6 -> 
            let r = StringToByte (colour.Substring(0, 2)) |> int
            let g = StringToByte (colour.Substring(2, 2)) |> int
            let b = StringToByte (colour.Substring(4, 2)) |> int

            r ||| (g <<< 8) ||| (b <<< 16)
        | _ -> failwith ("Invalid Hex colour " + colour)
    *)




        
