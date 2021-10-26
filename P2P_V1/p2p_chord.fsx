#time "on"
#r "nuget: Akka.FSharp"
#r "nuget: Akka.TestKit"

open System
open Akka.Actor
open Akka.FSharp
open Akka.Configuration

type Message = 
    | SetTotalNodes of int
    | RequestCompletion of int

type PeerMessage =
    | Init of int * IActorRef[]
    | SendRequest of string
    | ReceiveRequest of int * int * int
    | StartRequesting
    | RequestComplete of int

let system = ActorSystem.Create("System")


type ProcessController(nodes : int) =
    inherit Actor()
    let mutable totalNodes = nodes
    let mutable completedNodes = 0
    let mutable numHops = 0

    override x.OnReceive(receivedMsg) =
        match receivedMsg :?> Message with
            | SetTotalNodes nodes ->
                totalNodes <- nodes
            | RequestCompletion hops ->
                completedNodes <- completedNodes + 1
                numHops <- numHops + hops
                //printfn "Complete numHops: %i" numHops
                if(completedNodes = totalNodes) then
                    let avgHops = (float numHops) / (float totalNodes)
                    printfn "All the nodes have completed the number of requests to be made"
                    printfn "Average number of hops: %.1f" avgHops
                    Environment.Exit(0)
                    //system.Terminate()
                    ()
            | _ -> ()

let table (index : int, m : int) =
    let fingerTable = Map.empty<int, int>
    let upperBound = pown 2 (m-1)
    let mutable k = 1
    let mutable i = 0
    let mutable j = 1
    while(j < upperBound) do
        fingerTable |> Map.add i k
        printfn "%i" k
        if(k = 0) then
            k <- 1
        else
            k <- k * 2
            j <- j * 2
        if(k > m) then
            k <- 0 + (k - m)
        i <- i + 1
    fingerTable |> Map.iter (fun s n -> printfn "%i %i" s n)
    fingerTable

table(1, 10)
let selectPeerID (fingerTable : Map<int, int>, nodeID : int, desiredID : int) =
    let mutable peer = nodeID
    let mutable i =  0
    while (peer < desiredID) do
        peer <- fingerTable.[i]
        i <- i + 1
    peer <- fingerTable.[i]
    peer

type Peer(processController: IActorRef, requests: int, numNodes: int) =
    inherit Actor()
    let totalRequests = requests
    let totalPeers = numNodes
    let mutable nodeID = 0
    let mutable messageRequests = 0
    let mutable nodeLocation = ""
    let mutable totalHops = 0
    let mutable ring = Array.zeroCreate(numNodes)
    let mutable fingerTable = Map.empty<int, int>

    override x.OnReceive(receivedMsg) = 
        match receivedMsg :?> PeerMessage with
            | Init (id, peers) ->
                nodeID <- id
                nodeLocation <- "akka://system/user/Peer" + string nodeID
                ring <- peers
                fingerTable <- table(nodeID, ring.Length)
                ()
            | StartRequesting ->
                //Starts Scheduler to schedule SendRequest Message to self mailbox
                Actor.Context.System.Scheduler.ScheduleTellRepeatedly(TimeSpan.FromSeconds(0.), TimeSpan.FromSeconds(1.), Actor.Context.Self, SendRequest nodeLocation)
            | SendRequest node ->
                //Send a request for a random peer over here
                let randomPeer = Random().Next(totalPeers)
                let nextPeerID = selectPeerID(fingerTable, nodeID, randomPeer)
                let nodePeer = ring.[nextPeerID]
                nodePeer <! ReceiveRequest (nodeID, randomPeer, 0) 
                //printfn "Send Node: %s: %i" nodeLocation messageRequests
                //request for random peer to be sent here
                ()
            | ReceiveRequest (originalNode, desiredID, hops) ->
                let numHops = hops + 1
                if(desiredID = nodeID) then
                    ring.[originalNode] <! RequestComplete numHops
                else
                    let randomPeer = Random().Next(totalPeers)
                    let nodePeer = ring.[randomPeer]
                    nodePeer <! ReceiveRequest (nodeID, desiredID, numHops) 
                ()
            | RequestComplete hops ->
                messageRequests <- messageRequests + 1
                totalHops <- totalHops + hops
                if(messageRequests >= requests) then
                    processController <! RequestCompletion totalHops
                ()
            | _ -> ()
    


//Actual Working starts here
let numNodes = int (string (fsi.CommandLineArgs.GetValue 1))
let numRequests = int (string (fsi.CommandLineArgs.GetValue 2))

let processController = system.ActorOf(Props.Create(typeof<ProcessController>, numNodes),"processController")

//If there needs to be any modification in the number of nodes, please do so here


//_______________________________________________________________________________

processController <! SetTotalNodes(numNodes) //Initializing the total number of nodes in the entire system

//Initializing the entire ring as an array for now, until further progress

let ring = Array.zeroCreate(numNodes)

for i in [0 .. numNodes-1] do
    ring.[i] <- system.ActorOf(Props.Create(typeof<Peer>, processController, numRequests, numNodes), "Peer" + string i)
for i in [0 .. numNodes-1] do
    ring.[i] <! Init(i, ring)
let randomPeer = Random().Next(numNodes)
let nodePeer = "akka://system/user/Peer" + string randomPeer
for i in [0 .. numNodes-1] do
     ring.[i] <! StartRequesting

Console.ReadLine()