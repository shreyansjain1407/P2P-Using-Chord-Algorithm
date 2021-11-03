# P2P-Using-Chord-Algorithm

By: Shreyans Jain and Marcus Elosegui 

## Running the Programs

The P2P.fsx file can be found in the P2P_V1 folder. To run it, please use the following command:
	dotnet fsi P2P.fsx <numNodes> <numRequests>

## Program Description
Upon execution, the program initializes the ring of actors and creates finger tables for each one. A scheduler 
is set to the peers send out a message request every second. Once the message that will be requested
is generated, the node looks within its finger table to see if a matching peer id is contained within
it. If not, then the node iterates to find the next successor. Once the message is found the
original node is made aware of this and returns the number of hops it took to retrieve that message
to a process controlling node, which also keeps track of the total number of requests made. Once all 
requests have been fulfilled, the program gracefully ends and displays the average number of hops.

Failure is handled within this program by continuously updating each finger table every 5
seconds, as set by another scheduler. This allows for the unexpected death of a node to properly
leave the system and stop any other actors from attempting to make contact.

The largest network that we were able to deal with was 30k node.