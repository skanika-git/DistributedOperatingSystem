
// #time "on"
#r "nuget: Akka.FSharp"
#r "nuget: Akka.TestKit"

open System
open Akka.Actor
open Akka.Configuration
open Akka.FSharp
open System.Numerics
open System.Diagnostics
open System.Collections.Generic
open System.Threading

let mutable allNodes = Dictionary()
let mutable numNodes = 0
let mutable numRequests = 0
let mutable basis = 16
let mutable numDigits = 0
let mutable totalHops = int64 0

type Message =
    | PastryInit of String
    | SetRoutingTable of String[]*int
    | AddNode of String*int
    | Route of String
    | Print

let hexToInt c = 
    if c >= '0' && c <= '9' then int c - int '0'
    elif c >= 'A' && c <= 'F' then (int c - int 'A') + 10
    elif c >= 'a' && c <= 'f' then (int c - int 'a') + 10
    else 0

let hexStringToInt (hex:String) = 
    let str = hex |> Seq.rev |> System.String.Concat
    let mutable intVal = 0
    for i in 0 .. str.Length-1 do
        intVal <- intVal * 16 + (hexToInt (str.Chars i))
    intVal

let byteToHex bytes = 
    bytes 
    |> Array.map (fun (x : byte) -> System.String.Format("{0:X2}", x))
    |> String.concat System.String.Empty

let generateId (value:int)=
    let b = BitConverter.GetBytes(value)
    Array.Reverse(b);
    let hex = byteToHex(b)
    hex |> Seq.rev |> System.String.Concat
    //printfn "%s" hex

let generateRandom numNodes =
    let num = System.Random().Next(0, numNodes-1)
    generateId(num)

let findClosest nodeId = 
    let mutable closest = ""
    let mutable closestDist = Int64.MaxValue
    for KeyValue(key,value) in allNodes do
        let id = hexStringToInt(key)
        let dist = int64 (abs( hexStringToInt(nodeId) - id))
        if closestDist > dist then
            closest <- generateId(id)
            closestDist <- dist
    closest

let peerActor (mailbox: Actor<'a>) =

    let rec listener leafSet RoutingTable currId =
        actor{
            let! msg = mailbox.Receive()
            match msg with
            | PastryInit(nodeId) ->
                    let mutable leafSetP = Set([nodeId])
                    let RoutingTableP = [| for i in 1 .. numDigits -> [| for j in 1 .. basis -> null|] |]

                    let l = 7
                    let mutable left = hexStringToInt nodeId
                    for j in 0 .. l do
                        if left = 0 then
                            left <- allNodes.Count-1
                        let leftstr = generateId(left)
                        leafSetP <- leafSetP.Add(leftstr)
                        left <- left - 1

                    let r = 15
                    let mutable right = hexStringToInt nodeId
                    for j in l+1 .. r do
                        if right = allNodes.Count-1 then
                            right <- 0
                        let rightstr = generateId(right)
                        leafSetP <- leafSetP.Add(rightstr)
                        right <- right + 1

                    return! listener leafSetP RoutingTableP nodeId

            | SetRoutingTable(row, rowNum) ->
                    let mutable newRoutingTable = RoutingTable
                    Array.set newRoutingTable rowNum row
                    return! listener leafSet newRoutingTable currId

            | AddNode(nodeId, start) ->
                    (* copy routing table of currId into nodeID from row start *)
                    let mutable i = 0
                    let mutable newRoutingTable = RoutingTable
                    while nodeId.Chars i = currId.Chars i do
                        i <- i + 1
                    
                    let mutable curr = start
                    for k in start .. i do
                        //printfn "-----i is %d" k
                        let mutable currRow = RoutingTable.[k]
                        Array.set currRow (hexToInt (currId.Chars k)) (currId)
                        curr <- k
                        allNodes.Item(nodeId) <! SetRoutingTable(currRow, k)

                    let col = hexToInt (nodeId.Chars i)
                    if RoutingTable.[i].[col] = null then
                        Array.set newRoutingTable.[i] col nodeId
                    else
                        let entry = RoutingTable.[i].[col]
                        allNodes.Item(entry) <! AddNode(nodeId, curr)

                    return! listener leafSet newRoutingTable currId

            | Route(key) ->
                    //printfn "%s" currId
                    if key = currId then
                        //printfn "%s%s" "Horray FOUND IT at " currId
                        return! listener leafSet RoutingTable currId

                    if leafSet.Contains(key) then
                        //printfn "%s%s" "Found in leafset at " currId
                        totalHops <- totalHops + (int64 1)
                        allNodes.Item(key) <! Route(key)
                        return! listener leafSet RoutingTable currId

                    let mutable i = 0
                    while key.Chars i = currId.Chars i do
                        i <- i + 1

                    let mutable col = hexToInt (key.Chars i)
                    let mutable neighbor = RoutingTable.[i].[col]
                    if RoutingTable.[i].[col] = null then
                        col <- 0
                        while col<basis && RoutingTable.[i].[col] = null do
                            col <- col + 1
                        if col = basis then 
                            neighbor <- generateId((hexStringToInt(currId)+1)%numNodes)
                        else
                            neighbor <- RoutingTable.[i].[col]

                    allNodes.Item(neighbor) <! Route(key)
                    totalHops <- totalHops + (int64 1)
                    // printfn "totalhops %d" totalHops
                    return! listener leafSet RoutingTable currId

            | Print ->
                printfn "%A" RoutingTable
                return! listener leafSet RoutingTable currId
        }

    listener Set.empty [|[|""|]|] "0"

let sender nodeNum numNodes maxRequests (mailbox: Actor<'a>) =

    let rec listener reqCount node actorRef = 
        actor{
            let! msg = mailbox.Receive()
            match msg with
            | "send" ->
                    //printfn "Actor %s" node
                    if reqCount<maxRequests then
                        let mutable dst = generateRandom numNodes
                        while dst = node do
                            dst <- generateRandom numNodes
                        actorRef <! Route(dst)
                        //printfn "Actor %s sent message %s" node dst
                        mailbox.Context.System.Scheduler.ScheduleTellOnce(
                            TimeSpan.FromMilliseconds(1000.0),
                            mailbox.Self,
                            "send",
                            mailbox.Self
                        )
                    return! listener (reqCount + 1) node actorRef
        }

    let nodeStr = generateId(nodeNum)
    let nodeActorRef = allNodes.Item(nodeStr)
    listener 0 nodeStr nodeActorRef

let main() =
    let system = System.create "system" (Configuration.defaultConfig())
    numNodes <- fsi.CommandLineArgs.[1] |> int
    numRequests <- fsi.CommandLineArgs.[2] |> int
    numDigits <- int( Math.Ceiling (Math.Log(double numNodes, double basis)) )

    let actor = spawn system "peer0" (peerActor)
    let id = generateId(0)
    actor <! PastryInit(id)
    allNodes.Add(id, actor)

    //Building network
    for i in 1 .. numNodes-1 do
        let actorName = sprintf "peer%d" i
        let actor = spawn system actorName (peerActor)
        let id = generateId(i)
        let nid = findClosest id
        allNodes.Add(id, actor)
        actor <! PastryInit(id)
        allNodes.Item(nid) <! AddNode(id, 0)
        Thread.Sleep(10)

    //Routing
    [0 .. numNodes-1] |> List.map(fun id -> spawn system (sprintf "sender%d" id) (sender id numNodes numRequests) <! "send") |>ignore

    Thread.Sleep(10000)

    printfn "%s%d" "Total hops = " totalHops
    printfn "%s%f" "Average hops = " ((totalHops |> float)/((numRequests*numNodes) |> float)) |> ignore

    system.Terminate() |> ignore
    0

main()