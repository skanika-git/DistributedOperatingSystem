#r "nuget: Akka.FSharp"
#r "nuget: Akka.Remote"
#r "nuget: Akka.TestKit"
#r "nuget: FSharp.Json"
#load "./Messages.fsx"


// open important modules
open System
open Akka.FSharp
open Akka.Actor
open Akka.Configuration
open FSharp.Json
open Messages.Messages
open System.Numerics
open System.Diagnostics
open System.Collections.Generic
open System.Threading
open System.Text.RegularExpressions

type Message =
    | SendTweet of int * String * IActorRef
    | ParseTweet of int * String * IActorRef
    | UserTweet of int * int * IActorRef
    | Subscribe of int * int * IActorRef
    | GetFollowerList of int * int * IActorRef
    | UserList of Set<int> * int * IActorRef
    | Follow of int * int * IActorRef
    | RegisterUser of int * String * IActorRef
    | Login of int * String * IActorRef
    | Logout of int * IActorRef
    | HashTagQuery of String * IActorRef
    | MentionsQuery of int * IActorRef
    | GetTweetsFromId of Set<int> * IActorRef* string
    | GetFollowingTweets of int * IActorRef * string
    
let mutable sendTweetActorRef = null
let mutable hashTagActorRef = null
let mutable userTagActorRef = null
let mutable userTimeLineActorRef = null
let mutable newsFeedActorRef = null
let mutable followerActorRef = null
let mutable registerLoginActorRef = null

let SendResponse(a, d, client) =
    let resp: PacketType= {action=a; data=d}
    client <! Json.serialize resp
    
let registerLoginActor (mailbox: Actor<'a>) =
    
    let rec listener (regDict:  IDictionary<int,String>) (loggedInUser: Set<int>) = 
        actor{
            let! msg = mailbox.Receive()
            match msg with
            | RegisterUser(userId,b,client) -> 
                let found, valIs = regDict.TryGetValue(userId)
                match found with
                | true -> None |> ignore
                | false -> regDict.Add(userId,b)
            | Login(userId, password,client) ->
                let found, valIs = regDict.TryGetValue(userId)
                match found with
                | true -> if valIs = password then
                              loggedInUser.Add(userId) 
                          else
                              None |> ignore
                | false -> None |> ignore
            | Logout(userId,client) ->
                loggedInUser.Remove(userId)
            return! listener regDict loggedInUser
        }
    
    listener (new Dictionary<int,String>()) (Set.empty<int>)
    
//TWEET ACTOR
let sendTweetActor (mailbox: Actor<'a>) =
    
    let rec listener (tweetDict: IDictionary<int,String>) (tweetID : int)=   
        actor{
            let! msg = mailbox.Receive()
            match msg with
            | SendTweet(a,b,client) ->
                tweetDict.Add(tweetID,b)
                //printfn "%A" b
                hashTagActorRef <! ParseTweet(tweetID,b,client)
                userTagActorRef <! ParseTweet(tweetID,b,client)
                userTimeLineActorRef <! UserTweet(a,tweetID,client)
                followerActorRef <! GetFollowerList(a,tweetID,client)
                //printfn "%A" tweetDict
                return! listener tweetDict (tweetID+1)
            | GetTweetsFromId(setOfId,client, responseAction) ->
                //printfn "============= Tweet DICT %A ========== END" tweetDict 
                //printfn "============= SET OF ID %A ========== END" setOfId
                let mutable tList = []
                for x in setOfId do
                    let found, valIs = tweetDict.TryGetValue(x)
                    match found with
                    | true ->  tList <- tList @ [valIs]
                SendResponse(responseAction, Json.serialize {tweetList=tList},client)
                return! listener tweetDict tweetID
            | _ -> return! listener tweetDict tweetID
        }

    listener (new Dictionary<int, String>()) 1
    
// USER TAG DICTIONARY
let userTagActor (mailbox: Actor<'a>) =
    
    let rec listener (userTagDict: IDictionary<int,Set<int>>) =
        actor {
            let! msg = mailbox.Receive()
            match msg with
            | ParseTweet(a,b,client) ->
                let result = b.Split ' '
                for value in result do
                    if value.StartsWith("@") then
                        let taggedUserId = value.Substring(1) |> int
                        let found, valIs = userTagDict.TryGetValue(taggedUserId)
                        match found with
                        | true -> 
                            let temp = valIs
                            userTagDict.[taggedUserId] <- temp.Add(a)
                        | false -> userTagDict.Add(taggedUserId,Set.empty.Add(a))
                // printfn "ParseTweet ======%A" userTagDict
            |MentionsQuery(user,client) ->
                //printfn "Mentions ======%A" userTagDict
                let found, valIs = userTagDict.TryGetValue(user)
                match found with
                | true ->
                        // printfn "======%A" valIs 
                        sendTweetActorRef <! GetTweetsFromId(valIs,client,"FindMeMentionedResponse")
                | false -> SendResponse("FindMeMentionedResponse", Json.serialize {tweetList=[]}, client)
            return! listener userTagDict
        }
    listener (new Dictionary<int, Set<int>>())
    
// USER TIME LINE ACTOR
let userTimeLineActor (mailbox: Actor<'a>) =
    
    let rec listener (userTimeDict: IDictionary<int,Set<int>>) =
        actor {
            let! msg = mailbox.Receive()
            match msg with
            | UserTweet(a,b,client) ->
                let found, valIs = userTimeDict.TryGetValue(a)
                match found with
                | true -> 
                    let temp = valIs
                    userTimeDict.[a] <- temp.Add(b)
                | false -> userTimeDict.Add(a,Set.empty.Add(b))
            return! listener userTimeDict
        }
    listener (new Dictionary<int,Set<int>>())
    
// HASHTAG ACTOR
let hashTagActor (mailbox: Actor<'a>) =
    
    let rec listener (hashTagDict: IDictionary<String,Set<int>>) =
        actor {
            let! msg = mailbox.Receive()
            match msg with
            | ParseTweet(a,b,client) ->
                //printfn "%A" b
                let result = b.Split ' '
                for value in result do
                    if value.StartsWith("#") then
                        let found, valIs = hashTagDict.TryGetValue(value)
                        match found with
                        | true -> 
                            let temp = valIs
                            hashTagDict.[value] <- temp.Add(a)
                        | false -> hashTagDict.Add(value,Set.empty.Add(a))
            |HashTagQuery(hashTag,client) ->
                let found, valIs = hashTagDict.TryGetValue(hashTag)
                match found with
                | true -> sendTweetActorRef <! GetTweetsFromId(valIs,client,"FindHashTagResponse")
                | false -> SendResponse("FindHashTagResponse",Json.serialize {tweetList=[]},client)
            //printfn "%A" hashTagDict
            return! listener hashTagDict
    }

    listener (new Dictionary<String, Set<int>>())
    
// FOLLOWER ACTOR

let followerActor (mailbox: Actor<'a>) =
    
    let rec listener (followerDict: IDictionary<int,Set<int>>) =
        actor {
            let! msg = mailbox.Receive()
            match msg with
            | GetFollowerList(a,b,client) ->
                let found, valIs = followerDict.TryGetValue(a)
                match found with
                | true -> newsFeedActorRef <! UserList(valIs,b,client)
                | false -> None
            | Follow(u1,u2,client) ->
                let found, valIs = followerDict.TryGetValue(u2)
                match found with
                | true -> 
                    let temp = valIs
                    followerDict.[u2] <- temp.Add(u1)
                | false -> followerDict.Add(u2,Set.empty.Add(u1))
            return! listener followerDict
        }
    listener (new Dictionary<int, Set<int>>())
    
// NEWS FEED ACTOR

let newsFeedActor (mailbox: Actor<'a>) =
    
    let rec listener (newsFeedDict: IDictionary<int,Set<int>>) =
        actor {
            let! msg = mailbox.Receive()
            match msg with
            | UserList(a,b,client) ->
                for user in a do
                    let found, valIs = newsFeedDict.TryGetValue(user)
                    match found with
                    | true ->
                        let temp = valIs
                        newsFeedDict.[user] <- temp.Add(b)
                    | false -> newsFeedDict.Add(user,Set.empty.Add(b))
            | GetFollowingTweets(user, client, responseString) ->
                let found, valIs = newsFeedDict.TryGetValue(user)
                match found with
                | true -> sendTweetActorRef <! GetTweetsFromId(valIs,client,responseString)
                | false -> SendResponse(responseString,Json.serialize {tweetList=[]},client)
            return! listener newsFeedDict
        }
    listener (new Dictionary<int,Set<int>>())

let config =
    Configuration.parse
        @"akka {
            actor.provider = ""Akka.Remote.RemoteActorRefProvider, Akka.Remote""
            remote.helios.tcp {
                hostname = localhost
                port = 9001
            }
        }"

type ServerCommand = 
        | InitialiseServer 

// global variables
let system = System.create "twitterServer" config

let serverActor (mailbox: Actor<_>) =
            let mutable globalNumberOfUsers = 0
            let rec loop () = actor {
                let! message = mailbox.Receive ()
                let packetObj = Json.deserialize<PacketType> message 
                match packetObj.action with
                | "RegisterUser" ->
                    let registerTypeObject = Json.deserialize<registerType> packetObj.data
                    let username = registerTypeObject.name
                    let password = registerTypeObject.password
                    //printfn "%A%A" (registerTypeObject.name) (registerTypeObject.password)
                    let msg = RegisterUser(username|>int, password, mailbox.Context.Sender)
                    registerLoginActorRef <! msg
                    let response : PacketType = {action = "RegisterRequestResponse"; data = sprintf "%s registed" username}
                    mailbox.Sender() <! (Json.serialize response)
                | "LoginUser" ->
                    let registerTypeObject = Json.deserialize<registerType> packetObj.data
                    let username = registerTypeObject.name
                    let password = registerTypeObject.password
                    //printfn "%A%A" (registerTypeObject.name) (registerTypeObject.password)
                    let msg = Login(username|>int, password, mailbox.Context.Sender)
                    registerLoginActorRef <! msg
                    let msg2 = GetFollowingTweets(username |>int,mailbox.Context.Sender, "LoginResponse")
                    newsFeedActorRef <! msg2
                | "Logout" ->
                    let registerTypeObject = Json.deserialize<userIdFormat> packetObj.data
                    let username = registerTypeObject.name
                   // printfn "%A%A" (registerTypeObject.name) (registerTypeObject.password)
                    let msg = Logout(username|>int, mailbox.Context.Sender)
                    registerLoginActorRef <! msg
                | "SendTweet" ->
                    let TweetTypeObject = Json.deserialize<tweetFormat> packetObj.data
                    let username = TweetTypeObject.name
                    let tweet = TweetTypeObject.tweet
                    //printfn "%A%A" (registerTypeObject.name) (registerTypeObject.password)
                    let msg = SendTweet(username|>int, tweet,mailbox.Context.Sender)
                    sendTweetActorRef <! msg
                    let response : PacketType = {action = "SendTweetResponse"; data = sprintf "%s tweeted : %s" username tweet}
                    mailbox.Sender() <! (Json.serialize response)
                | "MentionQuery" ->
                    let TweetTypeObject = Json.deserialize<userIdFormat> packetObj.data
                    let username = TweetTypeObject.name
                    //printfn "%A%A" (registerTypeObject.name) (registerTypeObject.password)
                    let msg = MentionsQuery(username|>int,mailbox.Context.Sender)
                    userTagActorRef <! msg
                | "HashtagQuery" ->
                    let TweetTypeObject = Json.deserialize<userIdFormat> packetObj.data
                    let hashtag = TweetTypeObject.name
                    //printfn "%A%A" (registerTypeObject.name) (registerTypeObject.password)
                    let msg = HashTagQuery(hashtag,mailbox.Context.Sender)
                    hashTagActorRef <! msg
                | "RetweetQuery" ->
                    let retweetObject = Json.deserialize<userIdFormat> packetObj.data
                    let username = retweetObject.name
                    let msg = GetFollowingTweets(username |>int,mailbox.Context.Sender, "RetweetResponse")
                    newsFeedActorRef <! msg
                | "Subscribe" ->
                    let followObject = Json.deserialize<followFormat> packetObj.data
                    let u1 = followObject.user1
                    let u2 = followObject.user2
                    let msg = Follow(u1|>int, u2|>int,mailbox.Context.Sender)
                    followerActorRef <! msg
                    let response : PacketType = {action = "FollowUserResponse"; data = sprintf "%s is now following %s" u1 u2}
                    mailbox.Sender() <! (Json.serialize response)
                | "NewsFeed" ->
                    let newsFeedObj = Json.deserialize<userIdFormat> packetObj.data
                    let username = newsFeedObj.name
                    let msg = GetFollowingTweets(username |>int,mailbox.Context.Sender, "NewsFeedResponse")
                    newsFeedActorRef <! msg
                return! loop ()
            }
            loop ()



// initialize actors for individual services

let twitterServer = spawn system "server" serverActor
sendTweetActorRef <- spawn system "peer0" (sendTweetActor)
hashTagActorRef <- spawn system "hashActor" (hashTagActor)
userTagActorRef <- spawn system "userTagActor" (userTagActor)
userTimeLineActorRef <- spawn system "userTimeActor" (userTimeLineActor)
newsFeedActorRef <- spawn system "newsFeedActor" (newsFeedActor)
followerActorRef <- spawn system "followerActor" (followerActor)
registerLoginActorRef <- spawn system "regLoginActor" (registerLoginActor)
twitterServer <! ServerCommand.InitialiseServer
system.WhenTerminated.Wait () 

