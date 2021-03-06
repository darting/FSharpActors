module FSharpActors.Murmur

// copy from http://www.fssnip.net/7RH/title/MurmurHash3

let private multiply (x : uint32) (r : uint32) =
    ((x &&& 0xffffu) * r) + ((((x >>> 16) * r) &&& 0xffffu) <<< 16)

let private rotl (x : uint32) (r : int) =
    (x <<< r) ||| (x >>> (32 - r))

let private fmix (h : uint32) =
    h
    |> fun x -> x ^^^ (x >>> 16)
    |> fun x -> multiply x 0x85ebca6bu
    |> fun x -> x ^^^ (x >>> 13)
    |> fun x -> multiply x 0xc2b2ae35u
    |> fun x -> x ^^^ (x >>> 16)

let private bitSum (b : byte[]) =
    b
    |> Array.reduce (|||)

let private tailReducer b c1 c2 =
    b
    |> fun x -> multiply (uint32 x) c1
    |> fun x -> rotl x 15
    |> fun x -> multiply x c2

let hash (convertString : string) (seed : uint32) =
    let byteArray = System.Text.Encoding.UTF8.GetBytes(convertString)
    let c1 = 0xcc9e2d51u
    let c2 = 0x1b873593u
    let mutable h1 = seed
    let len = byteArray.Length
    let remainder = len % 4
    if len-remainder-1 > 0 then do
        let headArray = byteArray.[..(len-remainder-1)]
        for i = 0 to ((len-remainder)/4)-1 do
            let miniArray = headArray.[(i*4)..(i*4)+3]
            miniArray.[1] <- miniArray.[1] <<< 8
            miniArray.[2] <- miniArray.[2] <<< 16
            miniArray.[3] <- miniArray.[3] <<< 24
            let helper =
                bitSum miniArray
                |> fun x -> multiply (uint32 x) c1
                |> fun x -> rotl x 15
                |> fun x -> multiply x c2
                |> fun x -> multiply h1 x
                |> fun x -> rotl x 13
                |> fun x -> (multiply x 5u) + 0xe6546b64u
                |> fun x -> x ^^^ (uint32 len)
                |> fmix
            h1 <- helper
    let tailArray = byteArray.[(len-remainder)..]
    let k1 = 
        match tailArray.Length with
        | 3 -> 
                tailArray.[1] <- tailArray.[1] <<< 8
                tailArray.[2] <- tailArray.[2] <<< 16
                tailReducer (bitSum tailArray) c1 c2
        | 2 -> 
                tailArray.[1] <- tailArray.[1] <<< 8
                tailReducer (bitSum tailArray) c1 c2
        | 1 -> 
                tailReducer tailArray.[0] c1 c2
        | _ ->  0u
    h1 <- h1 ^^^ k1
    h1 <- h1 ^^^ (uint32 len)
    h1

