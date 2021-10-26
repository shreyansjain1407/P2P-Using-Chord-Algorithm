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
    | SetFingerTable of Map<int,IActorRef>
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
                printfn "This is just an initialization might not even be needed"
            | RequestCompletion ->
                completedNodes <- completedNodes + 1
                if(completedNodes = totalNodes) then
                    printfn "All the nodes have completed the number of requests to be made"
                    Environment.Exit(-1)
            | _ -> ()

type Peer(processController: IActorRef, requests: int, numNodes: int, PeerID: int) =
    inherit Actor()
    //Define required variables here
    let totalPeers = numNodes
    let mutable cancelRequesting = false
    //HashTable of the type <Int, Peer>
    let mutable fingerTable = Map.empty<int, IActorRef>


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
                    cancelRequesting <- true
                    processController <! RequestCompletion
                    //Also send ExitCircle message to all the nodes in routing table
                
                //Send a request for a random peer over here
                let randomPeer = Random().Next(totalPeers)
                printfn "Send Message received, Message Index %i" messageRequests
                messageRequests <- messageRequests + 1
                // match fingerTable.TryFind(randomPeer) with
                //     | some -> some <! Request(Actor.Context.Self)
                if fingerTable.ContainsKey(randomPeer) then
                    fingerTable.[randomPeer] <! Request(Actor.Context.Self)
                else
                    // Implement search message here by finding the closest peer
                    printfn "XYZ"
                    
                //request for random peer to be sent here
            | SetFingerTable x ->
                printfn "FingerTable Set Message Received"
                fingerTable <- x

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
let ringCapacity = (int)(2. ** (float)nearestPow)
numNodes <- ringCapacity

printfn "Nearest Power: %i, Ring Capacity: %i" nearestPow ringCapacity
//_______________________________________________________________________________

processController <! SetTotalNodes(numNodes) //Initializing the total number of nodes in the entire system

//Initializing the entire ring as an array for now, until further progress

let ring = Array.zeroCreate(numNodes)

for i in [0 .. numNodes-1] do
    ring.[i] <- system.ActorOf(Props.Create(typeof<Peer>, processController, numRequests, numNodes, i), "Peer" + string i)



for i in [0 .. numNodes-1] do
    let fingers = Map.empty<int, IActorRef>
    for j in [0 .. nearestPow - 1] do
        let x = (i + (int)(2. ** (float)j)) % (int)(2.** (float)nearestPow)
        // printfn "%i" x
        fingers.Add(x, ring.[x])
    printfn "The Map: %A" fingers
    printfn "Wiii"
    ring.[i] <! SetFingerTable(fingers)

for i in [0 .. numNodes-1] do
    // printfn "Peer %i Started Queueing messages" i
    ring.[i] <! StartRequesting

Console.ReadLine()