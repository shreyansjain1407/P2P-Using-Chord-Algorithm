#time "on"
#r "nuget: Akka.FSharp"
#r "nuget: Akka.TestKit"

open System
open Akka.Actor
open Akka.FSharp
//open Akka.Configuration
//open System.Reflection

type Message =
    | A of int
    // | PeerRing of IActorRef[]
    | RequestCompletion of int
    | SendRequest
    | ExitCircle of IActorRef // This can also essentially have just the id to the current node
    | StartRequesting //This message will start the scheduler which will then start sending request messages
    | RequestFwd of int*IActorRef*int
    | Request of IActorRef*int
    | SetFingerTable of Map<int,IActorRef>*Map<int,int>
    | SetRequests of int
    | Receipt
    | Join of int*IActorRef
    | UpdateFingerTable of Map<int, IActorRef>*Map<int,int>
    | UpdateSuccessors
    | RequestFingerTables of IActorRef

let system = ActorSystem.Create("System")

//Basic Actor Structure
type ProcessController(nodes: int) =
    inherit Actor()
    //Define required variables here
    let totalNodes = nodes
    let mutable completedNodes = 0
    let mutable totalHops = 0
    let mutable requests = 0

    override x.OnReceive(receivedMsg) =
        match receivedMsg :?> Message with 
            | RequestCompletion x ->
                completedNodes <- completedNodes + 1
                totalHops <-totalHops + x
                if(completedNodes = totalNodes) then
                    printfn $"All the nodes have completed the number of requests to be made with {(float(totalHops)/float(requests*totalNodes))} average hops"
                    Environment.Exit(-1)
            | SetRequests requests' ->
                requests <- requests'
            | _ -> ()

type Peer(processController: IActorRef, requests: int, numNodes: int, PeerID: int, N: int) =
    inherit Actor()
    //Define required variables here
    let totalPeers = numNodes
    let mutable cancelRequesting = false
    //HashTable of the type <Int, Peer>
    let mutable fingerTable = Map.empty<int, IActorRef>
    let mutable fingerPeerID = Map.empty<int, int>
    let mutable totalHops = 0
    //Counter to keep track of message requests sent by the given peer
    let mutable messageReceipts = 0
    
    let replace (peerID: int) (currentPeerID: int) =
        let mutable distance = 0
        if(currentPeerID < peerID) then
            distance <- peerID - currentPeerID
        else
            distance <- currentPeerID + int(2. ** 4.) - peerID
        
        let mutable closest = int(2. ** (Math.Log2(float distance)/Math.Log2(2.)))
        closest <- (closest + currentPeerID) % N
        match fingerPeerID.TryFind(closest) with
            | Some x ->
                if x = -1 then
                    true
                else
                    not(fingerPeerID.[closest] < peerID)
            | None -> true //This condition will, for the most part, never be accessed

    override x.OnReceive(receivedMsg) =
        match receivedMsg :?> Message with
            | Request (actor, hops) ->
                // Function when the request is received at the receiving peer
                totalHops <- totalHops + hops
                actor <! Receipt

            | RequestFwd (reqID, requestingPeer, hops) ->
                match fingerTable.TryFind(reqID) with
                    | Some actor ->
                        actor <! Request(requestingPeer, hops + 1)

                    | None ->
                        let mutable closest = -1
                        fingerTable |> Map.iter (fun _key _value -> if (_key < reqID || _key > closest) then closest <- _key)
                        fingerTable.[closest] <! RequestFwd(reqID, requestingPeer, hops + 1)

            | StartRequesting ->
                //Starts Scheduler to schedule SendRequest Message to self mailbox
                Actor.Context.System.Scheduler.ScheduleTellRepeatedly(TimeSpan.FromSeconds(0.), TimeSpan.FromSeconds(1.), Actor.Context.Self, SendRequest)
                
            | SendRequest ->
                //Send a request for a random peer over here
                let randomPeer = Random().Next(totalPeers)
                
                match fingerTable.TryFind(randomPeer) with
                    | Some actor ->
                        actor <! Request(Actor.Context.Self, 1)
                    | None ->
                        let mutable closest = -1
                        fingerTable |> Map.iter (fun _key _value -> if (_key < randomPeer || _key > closest) then closest <- _key)
                        fingerTable.[closest] <! RequestFwd(randomPeer, Actor.Context.Self, 1)

            | SetFingerTable (x, y)->
                fingerTable <- x
                fingerPeerID <- y

            | Receipt ->
                messageReceipts <- messageReceipts + 1
                if(messageReceipts = requests) then
                        cancelRequesting <- true
                        processController <! RequestCompletion(totalHops)
                printfn "Message Received at designated peer"
                
            | Join (peerID, peer) ->
                printfn $"Peer {peerID} has joined the ring"
                // According to new code, the table will never be entirely empty, It will have null values or -1s
                fingerTable |> Map.iter (fun _key _value ->
                    if replace peerID PeerID then
                        fingerTable <- fingerTable |> Map.add _key peer
                        fingerPeerID <- fingerPeerID |> Map.add _key peerID
                        //May implement else block if in case needed
                )
                fingerTable <- fingerTable |> Map.add peerID peer
                peer <! UpdateFingerTable(fingerTable, fingerPeerID)
                
            | UpdateFingerTable (x, y) ->
                //Function that takes an incoming PeerTable and matches it with the current peer table and updates values
                printfn $"Peer {PeerID} is updating its fingerTable"
                let mutable updateSuccessorRequest = false
                fingerPeerID |> Map.iter (fun key' value' ->
                    let mutable lowestValue =
                        if value' = -1 then
                            int (2. ** float N)
                        else
                            value'
                    let mutable leastKey = int (2. ** float N)        
                    y |> Map.iter (fun _key _value ->
                        if _value < lowestValue && _value >= key' then
                            lowestValue <- _value
                            leastKey <- _key
                    )
                    if leastKey <> (int (2. ** float N)) then
                        fingerPeerID <- fingerPeerID |> Map.add key' lowestValue
                        fingerTable <- fingerTable |> Map.add key' x.[leastKey]
                        updateSuccessorRequest <- true
                )
                if updateSuccessorRequest then
                    Actor.Context.Self <! UpdateSuccessors
            
            | UpdateSuccessors ->
                let curActor = Actor.Context.Self //I tried to put this in the message itself but for some reason it didn't except it
                fingerTable |> Map.iter (fun key' value' ->
                    if value' <> null then
                        value' <! RequestFingerTables(curActor)
                    )
            
            | RequestFingerTables x ->
                x <! UpdateFingerTable(fingerTable, fingerPeerID)
                
            | _ -> ()

//Actual Working starts here
let mutable numNodes = 5//int (string (fsi.CommandLineArgs.GetValue 1))
let numRequests = 10//int (string (fsi.CommandLineArgs.GetValue 2))

let processController = system.ActorOf(Props.Create(typeof<ProcessController>, numNodes),"processController")

//If there needs to be any modification in the number of nodes, please do so here
//Function to return greater or equal nearest power of 2 for the table
let nearestPower n=
    if ((n > 0) && (n &&& (n-1) = 0)) then
        n
    else
        let mutable count = 0
        let mutable x = n
        while (x <> 0) do
            x <- x >>> 1
            count <- count + 1
        count

let nearestPow = nearestPower numNodes
let ringCapacity = int (2.**float nearestPow)
numNodes <- ringCapacity

printfn $"Nearest Power: {nearestPow}, Ring Capacity: {ringCapacity}"
//_______________________________________________________________________________

processController <! SetRequests(numRequests)
//Initializing the entire ring as an array for now, until further progress

let ring = Array.zeroCreate(numNodes)

for i in [0 .. numNodes-1] do
    ring.[i] <- system.ActorOf(Props.Create(typeof<Peer>, processController, numRequests, numNodes, i, nearestPow), "Peer" + string i)

//Empty FingerTable Initialization
for i in [0 .. numNodes-1] do
    let mutable fingers = Map.empty<int, IActorRef>
    let mutable peerIDTable = Map.empty<int, int>
    fingers <- fingers |> Map.add i ring.[i]
    for j in [0 .. nearestPow - 1] do
        let x = i + int (2. ** float j) % int (2.** float nearestPow)
        fingers <- fingers |> Map.add x null
        peerIDTable <- peerIDTable |> Map.add x -1
    ring.[i] <! SetFingerTable(fingers, peerIDTable)

//At this point we pick a base peer, which will be the entry point for all other peers with empty peer tables and then
//once the process of adding peers is complete, we will start sending of messages to all peers in which case even if a
//peer drops dead then the other peers will have all the info they need to continue the entire process

let baseActor = ring.[0]
for i in [1 .. numNodes-1] do
    baseActor <! Join(i, ring.[i])

for i in [0 .. numNodes-1] do
    ring.[i] <! StartRequesting

Console.ReadLine()