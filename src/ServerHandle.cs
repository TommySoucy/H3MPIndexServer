using System.Net;

namespace H3MPIndexServer
{
    public class ServerHandle
    {
        public static void WelcomeReceived(int clientID, Packet packet)
        {
            if (packet.UnreadLength() == 0)
            {
                Entry.LogWarning("Client " + clientID + " did not report a version, disconnecting...");
                Server.clients[clientID].Disconnect(4);
            }
            else
            {
                Server.clients[clientID].reportedVersion = packet.ReadString();
                Entry.LogWarning("Client " + clientID + " reported version "+ Server.clients[clientID].reportedVersion);
                if (Server.clients[clientID].reportedVersion.Equals(Entry.H3MPMinimumVersion))
                {
                    Server.clients[clientID].welcomed = true;
                    Entry.Log(Server.GetPublicEndPoint(Server.clients[clientID].socket.Client.RemoteEndPoint) + " connected successfully and is now client " + clientID);
                }
                else
                {
                    string[] versionSplit = Entry.H3MPMinimumVersion.Split('.');
                    int minimumMajor = int.Parse(versionSplit[0]);
                    int minimumMinor = int.Parse(versionSplit[1]);
                    int minimumPatch = int.Parse(versionSplit[2]);
                    versionSplit = Server.clients[clientID].reportedVersion.Split('.');
                    int major = int.Parse(versionSplit[0]);
                    int minor = int.Parse(versionSplit[1]);
                    int patch = int.Parse(versionSplit[2]);

                    if (major > minimumMajor || minor > minimumMinor || patch > minimumPatch)
                    {
                        Server.clients[clientID].welcomed = true;
                        Entry.Log(Server.GetPublicEndPoint(Server.clients[clientID].socket.Client.RemoteEndPoint) + " connected successfully and is now client " + clientID);
                    }
                    else
                    {
                        Server.clients[clientID].Disconnect(5);
                    }
                }
            }
        }

        public static void Ping(int clientID, Packet packet)
        {
            long time = packet.ReadLong();
            Server.clients[clientID].ping = Convert.ToInt64((DateTime.Now.ToUniversalTime() - ThreadManager.epoch).TotalMilliseconds) - time;
            Server.clients[clientID].pingTimer = 0;
            ServerSend.Ping(clientID, time);
        }

        public static void RequestHostEntries(int clientID, Packet packet)
        {
            Entry.Log("Received request for host enries from "+clientID);
            ServerSend.HostEntries(clientID);
        }

        public static void List(int clientID, Packet packet)
        {
            Entry.Log("Listing request received from " + clientID);
            Client client = Server.clients[clientID];

            if (client.listed)
            {
                if (Server.hostEntries.TryGetValue(client.listedID, out HostEntry entry))
                {
                    Entry.Log("\tAlready listed, sending ID");
                    ServerSend.Listed(clientID, entry.ID);
                    return;
                }
                else
                {
                    client.listed = false;
                }
            }

            HostEntry newEntry = new HostEntry();
            newEntry.ID = Server.hostEntryID++;
            newEntry.name = packet.ReadString();
            newEntry.playerCount = 1;
            newEntry.limit = packet.ReadInt();
            newEntry.hasPassword = packet.ReadBool();
            if (newEntry.hasPassword)
            {
                newEntry.passwordHash = packet.ReadString();
            }
            newEntry.endPoint = new IPEndPoint((client.socket.Client.RemoteEndPoint as IPEndPoint).Address, packet.ReadUShort());
            newEntry.clientID = clientID;
            newEntry.modlistEnforcement = packet.ReadByte();
            if(newEntry.modlistEnforcement != 2)
            {
                newEntry.modlist = new List<string>();
                int modCount = packet.ReadInt();
                for(int i=0; i < modCount; ++i)
                {
                    newEntry.modlist.Add(packet.ReadString());
                }
            }

            Server.hostEntries.Add(newEntry.ID, newEntry);
            ServerSend.Admin(-1, 1);
            client.listed = true;
            client.listedID = newEntry.ID;

            ServerSend.Listed(clientID, newEntry.ID);

            Entry.Log("\tListed client "+clientID+" as entry "+ newEntry.ID);

            Entry.Log("Current host entry list: ");
            foreach (KeyValuePair<int, HostEntry> currentEntry in Server.hostEntries)
            {
                Entry.Log("\t" + currentEntry.Key + ": " + currentEntry.Value.name + " @ " + currentEntry.Value.endPoint + " hosted by client " + currentEntry.Value.clientID);
            }
            Entry.ListClients();
        }

        public static void Unlist(int clientID, Packet packet)
        {
            Entry.Log("Unlist request received from " + clientID);
            Client client = Server.clients[clientID];
            if (client.listed)
            {
                Entry.Log("Client listed, unlisting...");
                Server.hostEntries.Remove(client.listedID);
                ServerSend.Admin(-1, 1);
                client.listed = false;
            }

            Entry.Log("Current host entry list: ");
            foreach (KeyValuePair<int, HostEntry> currentEntry in Server.hostEntries)
            {
                Entry.Log("\t" + currentEntry.Key + ": " + currentEntry.Value.name + " @ " + currentEntry.Value.endPoint + " hosted by client " + currentEntry.Value.clientID);
            }
            Entry.ListClients();
        }

        public static void Disconnect(int clientID, Packet packet)
        {
            Entry.Log("Disconnect request received from " + clientID);
            Client client = Server.clients[clientID];
            client.Disconnect(2);
        }

        public static void Join(int clientID, Packet packet)
        {
            int entryID = packet.ReadInt();
            string hash = packet.ReadString();
            Entry.Log("Join request received from " + clientID+" for entry with ID: "+ entryID+" and hash start: "+hash.Substring(0,5));

            Entry.Log("Current host entry list: ");
            foreach (KeyValuePair<int, HostEntry> currentEntry in Server.hostEntries)
            {
                Entry.Log("\t" + currentEntry.Key + ": " + currentEntry.Value.name + " @ " + currentEntry.Value.endPoint+" hosted by client "+currentEntry.Value.clientID);
            }
            Entry.ListClients();

            if (Server.hostEntries.TryGetValue(entryID, out HostEntry entry))
            {
                if(!entry.hasPassword || entry.passwordHash.Equals(hash))
                {
                    Entry.Log("\tValid confirming connection with host");
                    if (Server.clients.ContainsKey(entry.clientID))
                    {
                        ServerSend.ConfirmConnection(entry.clientID, clientID);
                    }
                    else
                    {
                        Entry.Log("\t\tHOST " + entry.clientID + " OF ENTRY " + entry.ID + " WITH ENDPOINT " + entry.endPoint + " IS APPARENTLY NOT A CLIENT ANYMORE, UNLISTING ENTRY");
                    }
                }
                else // Wrong password
                {
                    Entry.Log("\tWrong password, sending null endpoint");
                    ServerSend.Connect(clientID, null);
                }
            }
            else // This host is not listed anymore
            {
                Entry.Log("\tHost not listed anymore, sending null endpoint");
                ServerSend.Connect(clientID, null);
            }
        }

        public static void RequestModlist(int clientID, Packet packet)
        {
            int entryID = packet.ReadInt();
            string hash = packet.ReadString();
            Entry.Log("Modlist request received from " + clientID+" for entry with ID: "+ entryID+" and hash start: "+hash.Substring(0,5));

            if (Server.hostEntries.TryGetValue(entryID, out HostEntry entry))
            {
                if(!entry.hasPassword || entry.passwordHash.Equals(hash))
                {
                    ServerSend.Modlist(clientID, entry.ID, entry.modlist);
                }
                else // Wrong password
                {
                    Entry.Log("\tWrong password, sending null modlist");
                    ServerSend.Modlist(clientID, entry.ID, null);
                }
            }
            else // This host is not listed anymore
            {
                Entry.Log("\tHost not listed anymore, sending null modlist");
                ServerSend.Modlist(clientID, entry.ID, null);
            }
        }

        public static void ConfirmConnection(int clientID, Packet packet)
        {
            bool valid = packet.ReadBool();
            int forClient = packet.ReadInt();
            Entry.Log("Confirm connection received from " + clientID + " for client: " + forClient);

            if (valid)
            {
                if (Server.hostEntries.TryGetValue(Server.clients[clientID].listedID, out HostEntry entry))
                {
                    Entry.Log("\tValid, sending endpoint");
                    ServerSend.Connect(forClient, entry.endPoint as IPEndPoint);
                }
                else // Not listed anymore
                {
                    Entry.Log("\tHost not listed anymore after confirming valid, sending null endpoint");
                    ServerSend.Connect(forClient, null);
                }
            }
            else // Host said connection not valid, probably no more space
            {
                Entry.Log("\tHost said connection not valid, sending null endpoint");
                ServerSend.Connect(forClient, null);
            }
        }

        public static void PlayerCount(int clientID, Packet packet)
        {
            int count = packet.ReadInt();
            Entry.Log("PlayerCount update request with new count: "+count+" received from " + clientID);

            if (Server.hostEntries.TryGetValue(Server.clients[clientID].listedID, out HostEntry entry))
            {
                entry.playerCount = count;
            }
            // else, this host is not listed anymore
        }

        public static void AdminRequest(int clientID, Packet packet)
        {
            Entry.Log("Admin access requested from " + clientID);
            string hash = packet.ReadString();

            if (hash.Equals(Entry.hash))
            {
                Entry.Log("\tPassword correct, sending initial data");
                Server.adminClient = Server.clients[clientID];

                // Send initial data
                ServerSend.Admin(clientID, 0);
                ServerSend.Admin(clientID, 1);
            }
        }

        public static void Admin(int clientID, Packet packet)
        {
            int key = packet.ReadInt();
            string hash = packet.ReadString();
            Entry.Log("Admin request received from " + clientID);

            if (hash.Equals(Entry.hash))
            {
                ServerSend.Admin(clientID, key);
            }
        }

        public static void AdminDisconnectClient(int clientID, Packet packet)
        {
            int ID = packet.ReadInt();
            Entry.Log("AdminDisconnectClient received from " + clientID+" for "+ID);

            if (Server.adminClient != null && Server.adminClient.ID == clientID && Server.adminClient.socket != null && Server.clients.TryGetValue(ID, out Client toDisconnect))
            {
                toDisconnect.Disconnect(1);
            }
            else
            {
                Entry.LogWarning("\tCould not disconnect client");
            }
        }

        public static void AdminRemoveHostEntry(int clientID, Packet packet)
        {
            int ID = packet.ReadInt();
            Entry.Log("AdminRemoveHostEntry received from " + clientID+" for "+ID);

            if (Server.adminClient != null && Server.adminClient.ID == clientID && Server.adminClient.socket != null && Server.hostEntries.TryGetValue(ID, out HostEntry toRemove))
            {
                if (Server.clients.TryGetValue(toRemove.clientID, out Client hostClient) && hostClient.socket != null && hostClient.socket.Client.RemoteEndPoint.ToString().Equals(toRemove.endPoint.ToString()))
                {
                    hostClient.listed = false;
                }
                Server.hostEntries.Remove(toRemove.ID);
                ServerSend.Admin(-1, 1);
            }
            else
            {
                Entry.LogWarning("\tCould not remove host entry");
            }
        }
    }
}
