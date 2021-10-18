#time "on"
#r "nuget: Akka.FSharp"
#r "nuget: Akka.TestKit"

open System
open Akka.Actor
open Akka.FSharp
open Akka.Configuration

type Message =
    | A of int
    | B of int
    | C of int

let system = ActorSystem.Create("System")

//Basic Actor Structure
type SomeActor() =
    inherit Actor()
    //Define required variables here

    override x.OnReceive(receivedMsg) =
        match receivedMsg :?> Message with 
            | A int ->
                ()
            | _ -> ()

