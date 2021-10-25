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
    //HashTable to be defined here
    let totalPeers = numNodes
    let fingerTable = Map.empty
    // successors.Add(processController, "23")
    //Counter to keep track of message requests sent by the given peer
    let mutable messageRequests = 0

    //################################################################################
    //THIS HAS BEEN IMPLEMENTED AND THE COMMENTS NEED TO BE REMOVED BEFORE SUBMISSION OF THE FILE
    //################################################################################
    //This while loop needs to be replaced with a scheduling function that automatically
    //sends a message every second according to the project spec something similar to:
    //https://www.dotkam.com/2011/10/11/akka-scheduler-sending-message-to-actors-self-on-start/
    
    // while messageRequests < totalRequests do
    //     Actor.Context.Self <! SendRequest

    // Actor.Context.System.Scheduler.ScheduleTellRepeatedly(....)

    //Actor.Context.Dispatcher 
    //This is supposed to be used for automated message scheduling
    //What we can also do is to run a loop in the primary execution space that will implement
    //a thread.sleep(1s) and then send a message to all of the actors to randomly send requests
    //this way we will not only have a consistent message sending process where we know how many
    //messages have been sent but also we don't need to implement a self dispacher and all the actors
    //can be initialized to the ring of actors beforehand
    //################################################################################
    override x.OnReceive(receivedMsg) =
        match receivedMsg :?> Message with
            | A int ->
                ()
            | StartRequesting ->
                //Starts Scheduler to schedule SendRequest Message to self mailbox
                Actor.Context.System.Scheduler.ScheduleTellRepeatedly(TimeSpan.FromSeconds(0.), TimeSpan.FromSeconds(1.), Actor.Context.Self, SendRequest)
                
            | SendRequest ->
                if(messageRequests = requests) then
                    processController <! RequestCompletion
                    //Also send ExitCircle message to all the nodes in routing table
                else 
                    //Send a request for a random peer over here
                    let randomPeer = Random().Next(totalPeers)
                    messageRequests <- messageRequests + 1
                    //request for random peer to be sent here
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

for i in [9 .. numNodes] do
    ring.[i] <- system.ActorOf(Props.Create(typeof<Peer>, processController, numRequests, numNodes), "Peer" + string i)

for i in [9 .. numNodes] do
    ring.[i] <! StartRequesting