using System.Net;

namespace H3MPIndexServer
{
    public class ServerSend
    {
        public static void SendTCPData(int toClient, Packet packet, bool custom = false)
        {
            packet.WriteLength();
            Server.clients[toClient].SendData(packet);
        }

        public static void SendTCPData(List<int> toClients, Packet packet, int exclude = -1, bool custom = false)
        {
            packet.WriteLength();
            for (int i = 0; i < toClients.Count; ++i)
            {
                if (exclude == -1 || toClients[i] != exclude)
                {
                    Server.clients[toClients[i]].SendData(packet);
                }
            }
        }

        public static void SendTCPDataToAll(Packet packet, bool custom = false)
        {
            packet.WriteLength();
            foreach (KeyValuePair<int, Client> clientEntry in Server.clients)
            {
                clientEntry.Value.SendData(packet);
            }
        }

        public static void SendTCPDataToClients(Packet packet, List<int> clientIDs, int excluding = -1, bool custom = false)
        {
            packet.WriteLength();
            foreach (int clientID in clientIDs)
            {
                if (excluding == -1 || clientID != excluding)
                {
                    Server.clients[clientID].SendData(packet);
                }
            }
        }

        public static void SendTCPDataToAll(int exceptClient, Packet packet, bool custom = false)
        {
            packet.WriteLength();
            foreach (KeyValuePair<int, Client> clientEntry in Server.clients)
            {
                if (clientEntry.Key != exceptClient)
                {
                    clientEntry.Value.SendData(packet);
                }
            }
        }

        public static void Welcome(int toClient, string msg)
        {
            using (Packet packet = new Packet((int)ServerPackets.welcome))
            {
                packet.Write(msg);
                packet.Write(toClient);
                packet.Write(Entry.H3MPMinimumVersion);

                SendTCPData(toClient, packet);
            }
        }

        public static void Ping(int toClient, long time)
        {
            using (Packet packet = new Packet((int)ServerPackets.ping))
            {
                packet.Write(time);
                SendTCPData(toClient, packet);
            }
        }

        public static void HostEntries(int toClient)
        {
            Entry.Log("Sending host entries to " + toClient);
            using (Packet packet = new Packet((int)ServerPackets.hostEntries))
            {
                packet.Write(Server.hostEntries.Count);
                foreach(KeyValuePair<int, HostEntry> hostEntry in Server.hostEntries)
                {
                    packet.Write(hostEntry.Key);
                    packet.Write(hostEntry.Value.name);
                    packet.Write(hostEntry.Value.playerCount);
                    packet.Write(hostEntry.Value.limit);
                    packet.Write(hostEntry.Value.hasPassword);
                    packet.Write((byte)hostEntry.Value.modlistEnforcement);
                }

                SendTCPData(toClient, packet);
            }
        }

        public static void Modlist(int toClient, int entryID, List<string> modlist)
        {
            Entry.Log("Sending modlist to " + toClient);
            using (Packet packet = new Packet((int)ServerPackets.modlist))
            {
                packet.Write(entryID);
                if (modlist == null)
                {
                    packet.Write(false);
                }
                else
                {
                    packet.Write(true);
                    packet.Write(modlist.Count);
                    for (int i = 0; i < modlist.Count; i++)
                    {
                        packet.Write(modlist[i]);
                    }
                }

                SendTCPData(toClient, packet);
            }
        }

        public static void HostEntries()
        {
            // Send host entries to all connected clients who aren't hosting
            foreach (KeyValuePair<int, Client> client in Server.clients)
            {
                if (!client.Value.listed)
                {
                    HostEntries(client.Value.ID);
                }
            }
        }

        public static void Listed(int toClient, int ID)
        {
            using (Packet packet = new Packet((int)ServerPackets.listed))
            {
                packet.Write(ID);

                SendTCPData(toClient, packet);
            }
        }

        public static void Connect(int toClient, IPEndPoint endPoint)
        {
            using (Packet packet = new Packet((int)ServerPackets.connect))
            {
                if(endPoint == null)
                {
                    packet.Write(false);
                }
                else
                {
                    packet.Write(true);
                    packet.Write(endPoint.Address.GetAddressBytes().Length);
                    packet.Write(endPoint.Address.GetAddressBytes());
                    packet.Write(endPoint.Port);
                }

                SendTCPData(toClient, packet);
            }
        }

        public static void ConfirmConnection(int toClient, int forClient)
        {
            using (Packet packet = new Packet((int)ServerPackets.confirmConnection))
            {
                packet.Write(forClient);

                SendTCPData(toClient, packet);
            }
        }

        public static void Admin(int toClient, int key)
        {
            using (Packet packet = new Packet((int)ServerPackets.admin))
            {
                packet.Write(key);
                switch (key)
                {
                    case 0: // Client data
                        packet.Write(Server.clients.Count);
                        foreach (KeyValuePair<int, Client> entry in Server.clients)
                        {
                            packet.Write(entry.Key);
                            if (entry.Value.socket == null)
                            {
                                packet.Write("Unknown endpoint");
                            }
                            else
                            {
                                packet.Write(entry.Value.socket.Client.RemoteEndPoint.ToString());
                            }
                        }
                        break;
                    case 1: // Host entry data
                        packet.Write(Server.hostEntries.Count);
                        foreach(KeyValuePair<int,HostEntry> entry in Server.hostEntries)
                        {
                            packet.Write(entry.Key);
                            packet.Write(entry.Value.clientID);
                            packet.Write(entry.Value.endPoint.ToString());
                            packet.Write(entry.Value.name);
                            packet.Write(entry.Value.playerCount);
                            packet.Write(entry.Value.limit);
                            packet.Write(entry.Value.hasPassword);
                        }
                        break;
                    case 2: // List debug entries
                        for(int i=0; i < 12; ++i)
                        {
                            HostEntry newEntry = new HostEntry();
                            newEntry.ID = Server.hostEntryID++;
                            newEntry.name = "Debug entry " + Server.hostEntryID;
                            newEntry.playerCount = 0;
                            newEntry.limit = 0;
                            newEntry.hasPassword = false;
                            newEntry.endPoint = new IPEndPoint(0, 0);
                            newEntry.clientID = 0;

                            Server.hostEntries.Add(newEntry.ID, newEntry);
                            ServerSend.Admin(-1, 1);
                        }
                        return;
                    case 3: // Clear debug entries
                        List<int> toRemove = new List<int>();
                        foreach(KeyValuePair<int, HostEntry> entry in Server.hostEntries)
                        {
                            if(entry.Value.name.StartsWith("Debug entry"))
                            {
                                toRemove.Add(entry.Key);
                            }
                        }
                        for(int i=0; i < toRemove.Count; ++i)
                        {
                            Server.hostEntries.Remove(toRemove[i]);
                            ServerSend.Admin(-1, 1);
                        }
                        return;
                }

                if(toClient == -1)
                {
                    if(Server.adminClient != null && Server.adminClient.socket != null)
                    {
                        SendTCPData(Server.adminClient.ID, packet);
                    }
                }
                else
                {
                    SendTCPData(toClient, packet);
                }
            }
        }

        public static void Log(string s, int mode = 0 /* 0: Log, 1: Warning, 2: Error */)
        {
            if(Server.adminClient != null && Server.adminClient.socket != null)
            {
                using (Packet packet = new Packet((int)ServerPackets.log))
                {
                    packet.Write(s);
                    packet.Write((byte)mode);

                    SendTCPData(Server.adminClient.ID, packet);
                }
            }
        }

        public static void LogWarning(string s)
        {
            Log(s, 1);
        }

        public static void LogError(string s)
        {
            Log(s, 2);
        }

        public static void Unlisted(int toClient)
        {
            using (Packet packet = new Packet((int)ServerPackets.unlisted))
            {
                SendTCPData(toClient, packet);
            }
        }
    }
}
