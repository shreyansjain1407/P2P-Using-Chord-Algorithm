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
    | Init of int
    | SendRequest of int
    | StartRequesting

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
                if(completedNodes = totalNodes) then
                    let avgHops = (float numHops) / (float totalNodes)
                    printfn "All the nodes have completed the number of requests to be made"
                    printfn "Average number of hops: %.1f" avgHops
            | _ -> ()





type Peer(ProcessController : IActorRef, requests : int) =
    inherit Actor()
    let totalRequests = requests
    let mutable nodeID = 0
    let mutable messageRequests = 0

    override x.OnReceive(receivedMsg) = 
        match receivedMsg :?> PeerMessage with
            | Init id ->
                nodeID <- id
            | StartRequesting ->
                //Starts Scheduler to schedule SendRequest Message to self mailbox
                Actor.Context.System.Scheduler.ScheduleTellRepeatedly(TimeSpan.FromSeconds(0.), TimeSpan.FromSeconds(1.), Actor.Context.Self, SendRequest)
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
    ring.[i] <- system.ActorOf(Props.Create(typeof<Peer>, processController, numRequests), "Peer" + string i)
for i in [9 .. numNodes] do
    ring.[i] <! Init(i)
