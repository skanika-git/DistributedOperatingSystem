open System.Threading

#r "nuget: Akka.FSharp"
#r "nuget: Akka.Remote"
#r "nuget: FSharp.Json"
#load "./Messages.fsx"

open System
open Akka.FSharp
open Akka.Actor
open Akka.Configuration
open FSharp.Json
open Messages.Messages

type Message =
    | Reg of float
    | Logintime of float
    | Tweettime of float
    | Followtime of float
    | HashtagQuerytime of float
    | MentionQuerytime of float
    | SubscribedtoQuerytime of float

let config =
    Configuration.parse
        @"akka {
            actor.provider = ""Akka.Remote.RemoteActorRefProvider, Akka.Remote""
            remote.helios.tcp {
                hostname = localhost
                port = 0
            }
        }"

let timer1 = new System.Diagnostics.Stopwatch()
let timer2 = new System.Diagnostics.Stopwatch()
let timer3 = new System.Diagnostics.Stopwatch()
let timer4 = new System.Diagnostics.Stopwatch()
let timer5 = new System.Diagnostics.Stopwatch()
let timer6 = new System.Diagnostics.Stopwatch()
let timer7 = new System.Diagnostics.Stopwatch()

let system = System.create "twitterClient" config

let Server = system.ActorSelection("akka.tcp://twitterServer@localhost:9001/user/server")

let rand = new Random();

let mutable numusers = 0
let mutable numtweets = 0
let mutable numsubscribing = 0

let setusers (noOfClients: int, totaltweets: int, totalsubscribing: int) =
    numusers <- noOfClients
    numtweets <- totaltweets
    numsubscribing <- totalsubscribing

let timeractor (mailbox: Actor<'a>) =

    let rec listener rtimes ltimes ttimes ftimes htimes mtimes stimes rcount lcount tcount fcount hcount mcount scount=
        actor {
            let! msg = mailbox.Receive()
            match msg with
            | Reg(time) ->
                    let rt = rtimes + time
                    let c = rcount + 1
                    if c = numusers then
                        let u = numusers |> float
                        let avgr = rt/u
                        printfn "Average time to register %d users = %f ms" numusers avgr
                    return! listener rt ltimes ttimes ftimes htimes mtimes stimes c lcount tcount fcount hcount mcount scount
            | Logintime(time) ->
                    let lt = ltimes + time
                    let l = lcount + 1
                    if l = numusers then
                        let u = numusers |> float
                        let avgr = lt/u
                        printfn "Average login time for %d users = %f ms" numusers avgr
                    return! listener rtimes lt ttimes ftimes htimes mtimes stimes rcount l tcount fcount hcount mcount scount
            | Tweettime(time) ->
                    let tt = ttimes + time
                    let w = tcount + 1
                    if w = numtweets then
                        let u = numtweets |> float
                        let avgr = tt/u
                        printfn "Average time taken to do %d tweets = %f ms" numtweets avgr
                    return! listener rtimes ltimes tt ftimes htimes mtimes stimes rcount lcount w fcount hcount mcount scount
            | Followtime(time) ->
                    let ft = ftimes + time
                    let f = fcount + 1
                    if f = numsubscribing then
                        let u = numsubscribing |> float
                        let avgr = ft/u
                        printfn "Average time taken to subscribe to %d users = %f ms" numsubscribing avgr
                    return! listener rtimes ltimes ttimes ft htimes mtimes stimes rcount lcount tcount f hcount mcount scount
            | HashtagQuerytime(time) ->
                    let ht = htimes + time
                    let h = hcount + 1
                    if h = numusers then
                        let u = numusers |> float
                        let avgr = ht/u
                        printfn "Average time taken to do %d hashtag queries= %f ms" numusers avgr
                    return! listener rtimes ltimes ttimes ftimes ht mtimes stimes rcount lcount tcount fcount h mcount scount
            | MentionQuerytime(time) ->
                    let mt = mtimes + time
                    let m = mcount + 1
                    if m = numusers then
                        let u = numusers |> float
                        let avgr = mt/u
                        printfn "Average time taken to do %d mention queries = %f ms" numusers avgr
                    return! listener rtimes ltimes ttimes ftimes htimes mt stimes rcount lcount tcount fcount hcount m scount
            | SubscribedtoQuerytime(time) ->
                    let st = stimes + time
                    let s = scount + 1
                    if s = numusers then
                        let u = numusers |> float
                        let avgr = st/u
                        printfn "Average time taken to do %d subscribed to queries = %f ms" numusers avgr
                    return! listener rtimes ltimes ttimes ftimes htimes mtimes st rcount lcount tcount fcount hcount mcount s
            | _->
                    return! listener rtimes ltimes ttimes ftimes htimes mtimes stimes rcount lcount tcount fcount hcount mcount scount
        }
    listener 0.0 0.0 0.0 0.0 0.0 0.0 0.0 0 0 0 0 0 0 0

let timep = spawn system (sprintf "timeractor") (timeractor)

let clientActor userId (mailbox: Actor<_>) =
            let rec loop userId = actor {
                let! message = mailbox.Receive ()
                let packetObj = Json.deserialize<PacketType> message
                match packetObj.action with
                | "RegisterRequest" ->
                    timer1.Start()
                    let serverRequest : PacketType = {action = "RegisterUser"; data =  packetObj.data }  
                    Server<! Json.serialize serverRequest
                | "RegisterRequestResponse" ->
                    printfn "%A" packetObj.data
                    let regtime = timer1.ElapsedMilliseconds |> float
                    timep <! Reg(regtime)
                | "Login" ->                    
                    timer2.Start()
                    let serverRequest : PacketType = {action = "LoginUser"; data =  packetObj.data }  
                    Server<! Json.serialize serverRequest
                | "LoginResponse" ->
                    printfn "Newsfeed :"
                    printfn "%A" packetObj.data
                    let logintime = timer2.ElapsedMilliseconds |> float
                    timep <! Logintime(logintime)
                | "Logout" ->
                    let serverRequest : PacketType = {action = "Logout"; data =  packetObj.data }  
                    Server<! Json.serialize serverRequest
                | "LogoutResponse" ->
                    printfn "%A" packetObj.data
                | "SendTweet" ->
                    timer3.Start()
                    let serverRequest : PacketType = {action = "SendTweet"; data =  packetObj.data }
                    Server<! Json.serialize serverRequest
                | "SendTweetResponse" ->
                    printfn "%A" packetObj.data
                    let tweettime = timer3.ElapsedMilliseconds |> float
                    timep <! Tweettime(tweettime)
                | "FollowUser" ->
                    timer4.Start()
                    let serverRequest : PacketType = {action = "Subscribe"; data =  packetObj.data }
                    Server<! Json.serialize serverRequest
                | "FollowUserResponse" ->
                    printfn "%A" packetObj.data
                    let followtime = timer4.ElapsedMilliseconds |> float
                    timep <! Followtime(followtime)                    
                | "FindHashTag" ->
                    timer5.Start()
                    let serverRequest : PacketType = {action = "HashtagQuery"; data =  packetObj.data }
                    Server<! Json.serialize serverRequest
                | "FindHashTagResponse" ->
                    let resp = Json.deserialize<tweetListFormat> packetObj.data
                    let rrlist = resp.tweetList
                    let hashtime = timer5.ElapsedMilliseconds |> float
                    timep <! HashtagQuerytime(hashtime)  
                    if rrlist <> list<string>.Empty then
                        printfn "%A" rrlist
                    else
                        printfn "This hashtag is not found in any tweet"
                | "FindMeMentioned" ->
                    timer6.Start()
                    let serverRequest : PacketType = {action = "MentionQuery"; data =  packetObj.data }
                    Server<! Json.serialize serverRequest
                | "FindMeMentionedResponse" ->
                    let resp = Json.deserialize<tweetListFormat> packetObj.data
                    let rrlist = resp.tweetList
                    let mentiontime = timer6.ElapsedMilliseconds |> float
                    timep <! MentionQuerytime(mentiontime) 
                    if rrlist <> list<string>.Empty then
                        printfn "%A" rrlist
                    else
                        printfn "You are not mentioned in any tweet"
                | "Retweet" ->
                    let serverRequest : PacketType = {action = "RetweetQuery"; data =  packetObj.data }
                    Server<! Json.serialize serverRequest
                | "RetweetResponse" ->
                    let rtlist = Json.deserialize<tweetListFormat> packetObj.data
                    let tlist = rtlist.tweetList
                    if tlist <> list<string>.Empty then
                        let mutable rtstring = tlist.[rand.Next(tlist.Length)] 
                        rtstring <- "My retweet is " + rtstring
                        let tweetData : tweetFormat = {
                            name = userId
                            tweet = rtstring
                        }
                        let request5 : PacketType = {action = "SendTweet"; data = Json.serialize tweetData }
                        mailbox.Self <! (Json.serialize request5)
                | "NewsFeed" ->
                    timer7.Start()
                    let serverRequest : PacketType = {action = "NewsFeed"; data =  packetObj.data }
                    Server<! Json.serialize serverRequest
                | "NewsFeedResponse" ->
                    let resp = Json.deserialize<tweetListFormat> packetObj.data
                    printfn "News Feed of this user %A" resp
                    let newsfeedtime = timer6.ElapsedMilliseconds |> float
                    timep <! SubscribedtoQuerytime(newsfeedtime) 
                | _ ->
                    printfn "%A" packetObj.data
                return! loop userId
            }
            loop userId