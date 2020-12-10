open System.Threading

#r "nuget: Akka.FSharp"
#r "nuget: Akka.Remote"
#r "nuget: FSharp.Json"
#load "./Messages.fsx"
#load "./Client.fsx"

open System
open Akka.FSharp
open Akka.Actor
open Akka.Configuration
open FSharp.Json
open System.Collections.Generic
open Messages.Messages
open Client

let rand = new Random();
let PasswordMap = Dictionary<string,string>()
let LoggedInUsers = Dictionary<string,IActorRef>()
let hashtaglist = ["#happy";"#smile";"#travel";"#cool";"#winter";"#angry"]

let randomStr = 
    let chars = "ABCDEFGHIJKLMNOPQRSTUVWUXYZ0123456789"
    let charsLen = chars.Length
    let random = System.Random()

    fun len -> 
        let randomChars = [|for i in 0..len -> chars.[random.Next(charsLen)]|]
        new System.String(randomChars)

let createUsers (noOfClients: int) =
    for user in 1..noOfClients do
        let data : registerType = {
            name = user |> string
            password = randomStr(10)
        }
        PasswordMap.Add(data.name,data.password)  
        let request : PacketType = {action = "RegisterRequest"; data = Json.serialize data }
        let client = spawn system (sprintf "clientActor%d" user) (clientActor (user|> string))
        client <! (Json.serialize request)
        //Thread.Sleep(1000)
        let request1 : PacketType = {action = "Login"; data = Json.serialize data }
        client <! (Json.serialize request1)
        LoggedInUsers.Add(data.name,client)

let FollowUsers (numClients: int, maxfollowerCount: int) =
    for user in [1..numClients] do
        //let num = maxfollowerCount/(numClients-user+1) |> int
        let n1 = maxfollowerCount/(numClients-user+1) |> float
        let n2 = floor n1
        let n3 = round(n2) |> int
        let num = n3 - 1
        let curruser = user |> string
        if num > 0 then
            let mutable followerlist : list<string> = []  
            for i in [1..num] do
                if i <> user then
                    let b = i |> string
                    followerlist  <- [b] |> List.append followerlist
            for i in followerlist do
                LoggedInUsers.Item(curruser) <! Json.serialize {action = "FollowUser"; data = Json.serialize {user1=curruser; user2=i} }

let sendTweet (numClients: int, maxtotaltweets: int) =
    for user in [1..numClients] do
        let t1 = maxtotaltweets/user |> float
        let t2 = floor t1
        let tweetnum = round(t2) |> int
        for i in [1..tweetnum] do
            let tweetData : tweetFormat = {
                name = user |> string
                tweet = randomStr(4) + " is my tweet"
            }
            let request2 : PacketType = {action = "SendTweet"; data = Json.serialize tweetData }
            LoggedInUsers.Item(user|>string) <! (Json.serialize request2)

let sendHashtagTweet (numClients: int) =
    for user in [1..numClients] do
        let tweetData : tweetFormat = {
            name = user |> string
            tweet = "I am tweeting a hashtag " + hashtaglist.[rand.Next(hashtaglist.Length)]
        }
        let request7 : PacketType = {action = "SendTweet"; data = Json.serialize tweetData }
        LoggedInUsers.Item(user|>string) <! (Json.serialize request7)

let sendMentionTweet (numClients: int) =
    for user in [1..numClients] do
        let mentionuser = rand.Next(1, numClients) |> string
        let tweetData : tweetFormat = {
            name = user |> string
            tweet = "Hello @" + mentionuser
        }
        let request8 : PacketType = {action = "SendTweet"; data = Json.serialize tweetData }
        LoggedInUsers.Item(user|>string) <! (Json.serialize request8)

let Query (numClients: int) =
    for user in [1..numClients] do
        let qhash = hashtaglist.[rand.Next(hashtaglist.Length)]
        let request6 : PacketType = {action = "FindHashTag"; data = Json.serialize {name=qhash} }
        LoggedInUsers.Item(user|>string) <! (Json.serialize request6)
    
    Thread.Sleep(2000)

    for user in [1..numClients] do
        let request6 : PacketType = {action = "FindMeMentioned"; data = Json.serialize {name=user|>string} }
        LoggedInUsers.Item(user|>string) <! (Json.serialize request6)
      
    Thread.Sleep(2000)  

    for user in [1..numClients] do
        let request6 : PacketType = {action = "NewsFeed"; data = Json.serialize {name=user|>string} }
        LoggedInUsers.Item(user|>string) <! (Json.serialize request6)

let Retweet (numClients: int) =
    for i in [3..5] do
        let tweetData : userIdFormat = {
            name = i |> string
        }
        let request2 : PacketType = {action = "Retweet"; data = Json.serialize tweetData }
        LoggedInUsers.Item(i |> string) <! (Json.serialize request2)

let disconnect(numClients: int,numToDisconnect: int) =
    let mutable disconnectList = []
    for i in [1..numToDisconnect] do
        let disconnectClientId = rand.Next(1, numClients)
        disconnectList  <- [disconnectClientId] |> List.append disconnectList
        let id  = disconnectClientId |> string
        let data : LogoutType = {
            name = id
        }
        let request5 : PacketType = {action = "Logout"; data = Json.serialize data }
        LoggedInUsers.Item(id) <! (Json.serialize request5)
        printfn "%d logged out" disconnectClientId
        LoggedInUsers.Item(id).Tell(PoisonPill.Instance);
        LoggedInUsers.Remove(id) |> ignore
    
    disconnectList

let rec LiveConnectionDisconnection(numClients: int) =
    Thread.Sleep(2000);
    let n = 20 * numClients
    let numToDisconnect = n / 100 |> int
    let disconnectList = disconnect (numClients,numToDisconnect)
    Thread.Sleep(2000);   
    //for each client in the disconnectList, connect them again
    for id in disconnectList do
        let client = spawn system (sprintf "clientActor%d" id) (clientActor (id |> string))
        let data : LoginType = {
            name = id |> string
            password = PasswordMap.Item(id |> string)
        }
        let request3 : PacketType = {action = "Login"; data = Json.serialize data }
        client <! (Json.serialize request3)
        printfn "%d logged in" id
        LoggedInUsers.Add(data.name,client)
        let tweetData : tweetFormat = {
            name = id |> string
            tweet = randomStr(4) + " is my latest tweet"
        }
        let request4 : PacketType = {action = "SendTweet"; data = Json.serialize tweetData }
        LoggedInUsers.Item(id|>string) <! (Json.serialize request4)

    LiveConnectionDisconnection (numClients)

let main() =

    let numClients = fsi.CommandLineArgs.[1] |> int
    let maxfollowerCount = fsi.CommandLineArgs.[2] |> int
    let maxtotaltweets = fsi.CommandLineArgs.[3] |> int

    let mutable totaltweets = 0
    for i in [1..numClients] do
        let t1 = maxtotaltweets/i |> float
        let t2 = floor t1
        let tweetnum = round(t2) |> int
        totaltweets <- totaltweets + tweetnum

    let mutable totalsubscribing = 0
    for user in [1..numClients] do
        let n1 = maxfollowerCount/(numClients-user+1) |> float
        let n2 = floor n1
        let n3 = round(n2) |> int
        let num = n3 - 1
        if num > 0 then
            totalsubscribing <- totalsubscribing + num

    setusers (numClients, totaltweets, totalsubscribing)

    createUsers (numClients)
    Thread.Sleep(2000)

    FollowUsers (numClients, maxfollowerCount)
    Thread.Sleep(2000)

    sendTweet (numClients, maxtotaltweets)
    Thread.Sleep(2000)

    sendHashtagTweet (numClients)
    Thread.Sleep(2000)

    sendMentionTweet (numClients)
    Thread.Sleep(2000)   

    Query (numClients)   
    Thread.Sleep(2000)
    
    Retweet (numClients)   
    Thread.Sleep(2000)

    LiveConnectionDisconnection(numClients)

    system.WhenTerminated.Wait()
    0

main()