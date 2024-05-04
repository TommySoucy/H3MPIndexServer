# H3MPIndexServer
 Index Server for H3MP
 
## Usage
-p *port* : Specify which port to use

A client can request admin control over the server by sending a ServerHandle.AdminRequest (10) packet with the password corresponding to the SHA-256 hash stored in Program.cs's "hash" variable. Their client will then be stored as the admin client on the server, and will then be able to send packets for AdminDisconnectClient and AdminRemoveHostEntry. The admin client will also be sent an up to date list of host entries and connected clients whenever there is a change to them. The admin client will also be sent all logs from the server.

This was originally used for debugging and being able to get all server logs without physical access to it.

## To run on Raspberry PI
- Publish for linux-arm from visual studio
- cd path/H3MPIndexServer/linux-arm
- ./H3MPIndexServer -p 7862
