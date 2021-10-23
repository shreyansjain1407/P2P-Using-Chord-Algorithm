#time "on"
#r "nuget: Akka.FSharp"
#r "nuget: Akka.TestKit"

open System
open Akka.Actor
open Akka.FSharp
open Akka.Configuration

type Message =
    | SetTotalNodes of int
    | B of int
    | C of int

let system = ActorSystem.Create("System")

//Basic Actor Structure
type ProcessController() =
    inherit Actor()
    //Define required variables here
    let mutable totalNodes = 0

    override x.OnReceive(receivedMsg) =
        match receivedMsg :?> Message with 
            | SetTotalNodes nodes ->
                totalNodes <- nodes
            | _ -> ()

type Peer() =
    inherit Actor()
    //Define required variables here

    override x.OnReceive(receivedMsg) =
        match receivedMsg :?> Message with
            | A int ->
                ()
            | _ -> ()

//Actual Working starts here
let numNodes = int (string (fsi.CommandLineArgs.GetValue 1))
let numRequests = int (string (fsi.CommandLineArgs.GetValue 2))

let processController = system.ActorOf(Props.Create(typeof<ProcessController>),"processController")

//If there needs to be any modification in the number of nodes, please do so here


//_______________________________________________________________________________

processController <! SetTotalNodes(numNodes) //Initializing the total number of nodes in the entire system
