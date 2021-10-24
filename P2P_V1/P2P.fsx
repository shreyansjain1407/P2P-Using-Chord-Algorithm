#time "on"
#r "nuget: Akka.FSharp"
#r "nuget: Akka.TestKit"

open System
open Akka.Actor
open Akka.FSharp
open Akka.Configuration

type Message =
    | SetTotalNodes of int
    | PeerRing of IActorRef[]
    | RequestCompletion
    | SendRequest
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
                totalNodes <- nodes
            | RequestCompletion ->
                completedNodes <- completedNodes + 1
                if(completedNodes = totalNodes)
                    printfn "All the nodes have completed the number of requests to be made"
            | _ -> ()

type Peer(processController: IActorRef, requests: int) =
    inherit Actor()
    //Define required variables here
    //HashTable to be defined here
    let totalRequests = requests
    let fingerTable = Map.empty
    // successors.Add(processController, "23")
    //Counter to keep track of message requests sent by the given peer
    let mutable messageRequests = 0

    //This while loop needs to be replaced with a scheduling function that automatically
    //sends a message every second according to the project spec something similar to:
    //https://www.dotkam.com/2011/10/11/akka-scheduler-sending-message-to-actors-self-on-start/
    //################################################################################
    while messageRequests < totalRequests do
        Actor.Context.Self <! SendRequest


    Actor.Context.Dispatcher //This is supposed to be used for automated message scheduling
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
            | SendRequest ->
                if(messageRequests = totlRequests){
                    processController <! RequestCompletion
                } else {
                    //Send a request for a random peer over here
                    
                }
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

