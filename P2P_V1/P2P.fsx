#time "on"
#r "nuget: Akka.FSharp"
#r "nuget: Akka.TestKit"

open System
open Akka.Actor
open Akka.FSharp
open Akka.Configuration

type Message =
    | A of int
    | SetTotalNodes of int
    // | PeerRing of IActorRef[]
    | RequestCompletion
    | SendRequest
    | ExitCircle of IActorRef // This can also essentiially have just the id to the current node
    | StartRequesting //This message will start the scheduler which will then start sending request messages
    | RequestFwd of int*IActorRef
    | Request of IActorRef
    | B of int
    | C of int

let system = ActorSystem.Create("System")

//Basic Actor Structure
type ProcessController(nodes: int) =
    inherit Actor()
    //Define required variables here
    let totalNodes = nodes
    let mutable completedNodes = 0

    override x.OnReceive(receivedMsg) =
        match receivedMsg :?> Message with 
            | SetTotalNodes nodes ->
                // totalNodes <- nodes
                ()
            | RequestCompletion ->
                completedNodes <- completedNodes + 1
                if(completedNodes = totalNodes) then
                    printfn "All the nodes have completed the number of requests to be made"
            | _ -> ()

type Peer(processController: IActorRef, requests: int, numNodes: int) =
    inherit Actor()
    //Define required variables here
    let totalPeers = numNodes
    //HashTable of the type <Int, Peer>
    let mutable fingerTable = Map.empty<int, Peer>

    //Counter to keep track of message requests sent by the given peer
    let mutable messageRequests = 0

    override x.OnReceive(receivedMsg) =
        match receivedMsg :?> Message with
            | A int ->
                printfn "Just some random function"
            | StartRequesting ->
                //Starts Scheduler to schedule SendRequest Message to self mailbox
                Actor.Context.System.Scheduler.ScheduleTellRepeatedly(TimeSpan.FromSeconds(0.), TimeSpan.FromSeconds(1.), Actor.Context.Self, SendRequest)
                
            | SendRequest ->
                if(messageRequests = requests) then
                    processController <! RequestCompletion
                    //Also send ExitCircle message to all the nodes in routing table
                
                //Send a request for a random peer over here
                let randomPeer = Random().Next(totalPeers)
                messageRequests <- messageRequests + 1
                // match fingerTable.TryFind(randomPeer) with
                //     | some -> some <! Request(Actor.Context.Self)
                if fingerTable.ContainsKey(randomPeer) then
                    let xyz = fingerTable.[randomPeer]
                    xyz <! Request(Actor.Context.Self)
                //request for random peer to be sent here
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

for i in [9 .. numNodes] do
    ring.[i] <- system.ActorOf(Props.Create(typeof<Peer>, processController, numRequests, numNodes), "Peer" + string i)

for i in [9 .. numNodes] do
    ring.[i] <! StartRequesting

Console.ReadLine()