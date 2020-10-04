// Project1.fsx
#time "on"
#r "nuget: Akka.FSharp" 
#r "nuget: Akka.TestKit" 
// #load "Bootstrap.fsx"

open System
open Akka.Actor
open Akka.Configuration
open Akka.FSharp
open System.Numerics

let square (x: int) = BigInteger (x * x)

type Message =
    | Next of int
    | SquareMessage of int*int

let squareSumActor square isPerfectSquare K (mailbox: Actor<'a>) = 
    let rec listener = 
        actor {
            let! msg = mailbox.Receive()
            match msg with
            | SquareMessage(idx, I) ->
                let mutable sum = BigInteger 0
                for x in [I .. I+K-1] do sum <- BigInteger.Add(sum, square x)
                let perfSq = isPerfectSquare sum
                if perfSq = true then printfn "%d\n" I
                mailbox.Sender() <! Next(idx)
            return! listener
        }

    listener

let isPerfectSquare (n: BigInteger) =
    let bigOne = (BigInteger 1)

    let rec binary_search (low:BigInteger) (high:BigInteger) =
        let mid = BigInteger.Divide(BigInteger.Add(high, low), (BigInteger 2))
        let midSquare = BigInteger.Multiply(mid, mid)
        let compare1 = BigInteger.Compare(low, high)
        let compare2 = BigInteger.Compare(n, midSquare)
        if compare1 > 0 then false
        else if compare2 = 0 then true
        else if compare2 < 0 then 
            let minusOne = BigInteger.Subtract(mid, bigOne)
            binary_search low minusOne 
        else 
            let plusOne = BigInteger.Add(mid, bigOne)
            binary_search plusOne high

    binary_search (BigInteger 1) n

let mainActor K N (mailbox: Actor<'a>) =
    let maxSpawn = 12
    let childRef = 
        [1 .. maxSpawn]
        |> List.map(fun id -> 
                        let name = sprintf "square-sum-actor%d" id
                        spawn mailbox name (squareSumActor square isPerfectSquare K))
    [1 .. maxSpawn] |> List.map(fun id -> childRef |> List.item(id-1) <! SquareMessage(id-1, id)) |> ignore

    let rec listener childRef spawned actorsFinished =
        if actorsFinished = N then
            mailbox.Context.System.Terminate() |> ignore
        actor {
            let! msg = mailbox.Receive()
            match msg with
            | Next(i) ->
                if spawned < N then 
                    childRef |> List.item(i) <! SquareMessage(i, spawned+1)
                return! listener childRef (spawned + 1) (actorsFinished + 1)
            | _ -> return! listener childRef spawned actorsFinished
        }
    listener childRef maxSpawn 0

let main(args) =
    let N = int fsi.CommandLineArgs.[1]
    let K = int fsi.CommandLineArgs.[2]
    let system = System.create "system" (Configuration.defaultConfig())
    
    let mainA = spawn system "mainActor" (mainActor K N)
    
    system.WhenTerminated.Wait()

    0

main()