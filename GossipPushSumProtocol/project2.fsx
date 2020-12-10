// #time "on"
#r "nuget: Akka.FSharp"
#r "nuget: Akka.TestKit"

open System
open Akka.Actor
open Akka.Configuration
open Akka.FSharp
open System.Numerics
open System.Diagnostics
open System.Threading

type Message =
    | Rumour
    | Neighbour of List<IActorRef>
    | Push of double*double
    | Wake
    | Done

let system = System.create "system" (Configuration.defaultConfig())

let timer = new System.Diagnostics.Stopwatch() 

let mutable nodes = 0
let mutable mainActorRef = []
let mutable topo = ""
let mutable ActorRef = [] 

let GossipActor (mailbox: Actor<'a>) =
    let maxCount = 10

    let rec listener childRef maxCount rumourCount=
        actor {
            let! msg = mailbox.Receive()
            match msg with
            | Rumour ->                                              
                    if rumourCount = 0 then
                            mainActorRef.[0] <! Done
                    if rumourCount < maxCount-1 then
                            let nbor = Random().Next(childRef |> List.length)
                            childRef |> List.item nbor <! Rumour
                            mailbox.Self <! Wake // for sending to neighbors again
                    else
                            if topo = "line" then
                                let nbor1 = Random().Next(ActorRef |> List.length)
                                ActorRef |> List.item nbor1 <! Rumour
                    return! listener childRef maxCount (rumourCount + 1)
            | Wake ->
                    if rumourCount < maxCount then
                        let nbor = Random().Next(childRef |> List.length)
                        childRef |> List.item nbor <! Rumour
                        mailbox.Self <! Wake
                    return! listener childRef maxCount rumourCount
            | Neighbour(x) ->
                    return! listener x maxCount rumourCount
            | _-> return! listener childRef maxCount rumourCount
        }
    listener [] maxCount 0

let PushSumActor idx (mailbox: Actor<'a>) =
    let s = idx |> double
    let w = 0 |> double
    let counters = 0

    let rec listener childRef s w cons c=
        actor {
            let! msg = mailbox.Receive()
            match msg with
            | Push(p,q)  -> 
                    let c = counters + 1
                    if c = 1 then
                        mailbox.Self <! Wake

                    let m = ((s+p)/(w+q)) - (s/w)
                    let sp = (s + p)
                    let s1 = sp / 2.0
                    let wp = (w + q)
                    let w1 = wp / 2.0
                    let mutable consp = cons + 1
 
                    if p=0.0 || (abs m > pown 10.0 -10) then
                        consp <- 0                             
                    else
                        if consp = 3 then
                            mainActorRef.[0] <! Done

                    childRef |> List.item (Random().Next(childRef.Length)) <! Push(s1,w1)
                    return! listener childRef s1 w1 consp c
            | Wake ->
                    let sk = s/2.0
                    let wk = w/2.0
                    childRef |> List.item (Random().Next(childRef.Length)) <! Push(sk,wk)
                    return! listener childRef sk wk cons c
            | Neighbour(x) -> 
                    return! listener x s w cons c
            | _-> return! listener childRef s w cons c
        }
    listener [] s w 0 0

let CreateTopology (topology: String, ActorRef: byref<'T>, N: int) =
    if topology = "full" then
        ActorRef |> List.item 0 <! Neighbour(ActorRef.[1..])
        ActorRef |> List.item (N-1) <! Neighbour(ActorRef.[..N-2])
        for i in 1 .. N-2 do
            ActorRef |> List.item i <! Neighbour(ActorRef.[..i-1] @ ActorRef.[i+1..])
    elif topology = "line" then
        ActorRef |> List.item 0 <! Neighbour([ActorRef.[1]])
        ActorRef |> List.item (N-1) <! Neighbour([ActorRef.[N-2]])
        for i in [1 .. N-2] do
            ActorRef |> List.item i <! Neighbour(ActorRef.[i-1] :: [ActorRef.[i+1]])
    elif topology = "2D" then
        let P = N |> float
        let k  = sqrt P |>int
        let max = k*k-1
        ActorRef |> List.item 0 <! Neighbour(ActorRef.[1] :: [ActorRef.[k]])
        ActorRef |> List.item (k-1) <! Neighbour(ActorRef.[k-2] :: [ActorRef.[2*k-1]])
        ActorRef |> List.item max <! Neighbour(ActorRef.[max-k] :: [ActorRef.[max-1]])
        ActorRef |> List.item (max-k+1) <! Neighbour(ActorRef.[max-k+2] :: [ActorRef.[max+1-2*k]])
        for i in [1 .. max] do
            if i<>0 && i<>k-1 && i<>max && i<>max-k+1 then
                if i<k then
                    ActorRef |> List.item i <! Neighbour(ActorRef.[i-1] :: ActorRef.[i+1] :: [ActorRef.[i+k]])
                elif i>max-k then
                    ActorRef |> List.item i <! Neighbour(ActorRef.[i-1] :: ActorRef.[i+1] :: [ActorRef.[i-k]])
                elif i%k=0 then
                    ActorRef |> List.item i <! Neighbour(ActorRef.[i+1] :: ActorRef.[i+k] :: [ActorRef.[i-k]])
                elif i%k=k-1 then
                    ActorRef |> List.item i <! Neighbour(ActorRef.[i-1] :: ActorRef.[i+k] :: [ActorRef.[i-k]])
                else
                    ActorRef |> List.item i <! Neighbour(ActorRef.[i-1] :: ActorRef.[i+1] :: ActorRef.[i-k] :: [ActorRef.[i+k]])
    elif topology = "imp2D" then
        let P = N |> float
        let k = sqrt P |> int
        let max = k*k-1
        ActorRef |> List.item 0 <! Neighbour(ActorRef.[1] :: ActorRef.[k] :: [ActorRef.[Random().Next(max+1)]])
        ActorRef |> List.item (k-1) <! Neighbour(ActorRef.[k-2] :: ActorRef.[2*k-1] :: [ActorRef.[Random().Next(max+1)]])
        ActorRef |> List.item max <! Neighbour(ActorRef.[max-k] :: ActorRef.[max-1] :: [ActorRef.[Random().Next(max+1)]])
        ActorRef |> List.item (max-k+1) <! Neighbour(ActorRef.[max-k+2] :: ActorRef.[max+1-2*k] :: [ActorRef.[Random().Next(max+1)]])
        for i in [1 .. max] do
            if i<>0 && i<>k-1 && i<>max && i<>max-k+1 then     
                if i<k then
                    ActorRef |> List.item i <! Neighbour(ActorRef.[i-1] :: ActorRef.[i+1] :: ActorRef.[i+k] :: [ActorRef.[Random().Next(max+1)]])
                elif i>max-k then
                    ActorRef |> List.item i <! Neighbour(ActorRef.[i-1] :: ActorRef.[i+1] :: ActorRef.[i-k] :: [ActorRef.[Random().Next(max+1)]])
                elif i%k=0 then
                    ActorRef |> List.item i <! Neighbour(ActorRef.[i+1] :: ActorRef.[i+k] :: ActorRef.[i-k] :: [ActorRef.[Random().Next(max+1)]])
                elif i%k=k-1 then
                    ActorRef |> List.item i <! Neighbour(ActorRef.[i-1] :: ActorRef.[i+k] :: ActorRef.[i-k] :: [ActorRef.[Random().Next(max+1)]])
                else
                    ActorRef |> List.item i <! Neighbour(ActorRef.[i-1] :: ActorRef.[i+1] :: ActorRef.[i-k] :: ActorRef.[i+k] :: [ActorRef.[Random().Next(max+1)]])
    else
        printfn "Invalid topology"

let mainActor algo topology N (mailbox: Actor<'a>) =
    if algo = "gossip" then
        ActorRef <-
            [0 .. N-1]
            |> List.map(fun id ->
                        let name = sprintf "GossipActor%d" id
                        spawn system name (GossipActor))
        CreateTopology (topology, &ActorRef, N)
        //Thread.Sleep(10)
        timer.Start()
        ActorRef |> List.item 0 <! Rumour

    elif algo = "push-sum" then
        ActorRef <-
            [0 .. N-1]
            |> List.map(fun id -> 
                        let name = sprintf "PushSumActor%d" id
                        spawn system name (PushSumActor (id + 1)))
        CreateTopology (topology, &ActorRef, N)
        //Thread.Sleep(10)
        timer.Start()
        ActorRef |> List.item 0 <! Push(0.0,0.0)  
    else
        printfn "Invalid algorithm"

    let rec listener counter =
        actor {
            let! msg = mailbox.Receive()
            match msg with
            | Done ->
                    let counterp = (counter + 1)
                    // printfn "counter = %d node = %d" counterp nodes
                    if counterp = int (9 * nodes) / 10 then
                        printfn "%d" timer.ElapsedMilliseconds
                        mailbox.Context.System.Terminate() |> ignore
                    return! listener counterp
        }

    listener 0

let main() =
    let mutable N = int fsi.CommandLineArgs.[1]
    let topology = fsi.CommandLineArgs.[2] //"imp2D"
    let algo = fsi.CommandLineArgs.[3] //"push-sum"

    if topology="2D" || topology="imp2D" then
        let P = N |> float
        N <- pown (int (sqrt P)) 2

    nodes <- N
    topo <- topology

    mainActorRef <- [spawn system "mainActor" (mainActor algo topology N)]
    
    system.WhenTerminated.Wait()
    0

main()