using System.Globalization;
using System.Net.Sockets;

namespace H3MPIndexServer
{
    public class Client
    {
        public static int dataBufferSize = 4096;

        public long ping;
        public float pingTimer;
        public bool welcomed;
        public float welcomeTimer;
        public string reportedVersion;

        public int ID;
        public TcpClient socket;
        private NetworkStream stream;
        public Packet receivedData;
        private byte[] receiveBuffer;

        private bool _listed;
        public bool listed
        {
            set { _listed = value; if (socket != null) { ServerSend.Unlisted(ID); } }
            get { return _listed; }
        }
        public int listedID;

        public Client(int ID, TcpClient socket)
        {
            this.ID = ID;
            this.socket = socket;
            socket.ReceiveBufferSize = dataBufferSize;
            socket.SendBufferSize = dataBufferSize;

            stream = socket.GetStream();
            receiveBuffer = new byte[dataBufferSize];
            stream.BeginRead(receiveBuffer, 0, dataBufferSize, ReceiveCallback, null);

            receivedData = new Packet();

            Server.clients.Add(ID, this);
            Entry.Log("New client added, sending client list to admin");
            ServerSend.Admin(-1, 0);

            ServerSend.Welcome(ID, "Welcome to index server");
        }

        private void ReceiveCallback(IAsyncResult result)
        {
            try
            {
                // Get amount of data received
                int byteLength = stream.EndRead(result);

                // If 0, connection ended
                if(byteLength == 0)
                {
                    return;
                }

                // If got data, store it for processing
                byte[] data = new byte[byteLength];
                Array.Copy(receiveBuffer, data, byteLength);

                // Process
                int handleCode = HandleData(data);
                if (handleCode > 0)
                {
                    receivedData.Reset(handleCode == 2);
                }

                // Resume reading stream
                stream.BeginRead(receiveBuffer, 0, dataBufferSize, ReceiveCallback, null);
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Entry.LogError("Client "+ID+" callback error: "+ex.Message+":\n"+ex.StackTrace);
                Console.ForegroundColor = ConsoleColor.White;

                Disconnect(0, ex);
            }
        }

        public void SendData(Packet packet)
        {
            try
            {
                if(socket != null)
                {
                    stream.BeginWrite(packet.ToArray(), 0, packet.Length(), null, null);
                }
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Entry.LogError("Client " + ID + " SendData error: " + ex.Message + ":\n" + ex.StackTrace);
                Console.ForegroundColor = ConsoleColor.White;

                Disconnect(1, ex);
            }
        }

        private int HandleData(byte[] data)
        {
            int packetLength = 0;
            bool readLength = false;

            receivedData.SetBytes(data);

            if (receivedData.UnreadLength() >= 4)
            {
                packetLength = receivedData.ReadInt();
                readLength = true;
                if (packetLength <= 0)
                {
                    return 2;
                }
            }

            while (packetLength > 0 && packetLength <= receivedData.UnreadLength())
            {
                readLength = false;
                byte[] packetBytes = receivedData.ReadBytes(packetLength);
                ThreadManager.ExecuteOnMainThread(() =>
                {
                    using (Packet packet = new Packet(packetBytes))
                    {
                        try
                        {
                            int packetID = packet.ReadInt();
                            if (Server.packetHandlers != null)
                            {
                                try
                                {
                                    Server.packetHandlers[packetID](ID, packet);
                                }
                                catch(Exception ex1)
                                {
                                    Entry.LogError("Failed to handle packet "+ packetID + " from client "+ ID + ": " + ex1.Message + "\n" + ex1.StackTrace);
                                }
                            }
                        }
                        catch(Exception ex)
                        {
                            Entry.LogError("Got incorrectly formatted packet: "+ex.Message+"\n"+ ex.StackTrace);
                        }
                    }
                });

                packetLength = 0;

                if (receivedData.UnreadLength() >= 4)
                {
                    packetLength = receivedData.ReadInt();
                    readLength = true;
                    if (packetLength <= 0)
                    {
                        return 2;
                    }
                }
            }

            if (packetLength == 0 && receivedData.UnreadLength() == 0)
            {
                return 2;
            }

            return readLength ? 1 : 0;
        }

        public void Disconnect(int code, Exception ex = null)
        {
            if(Server.adminClient == this)
            {
                Server.adminClient = null;
            }

            try
            {
                switch (code)
                {
                    case 0:
                        Entry.Log("Client " + ID + " : " + socket.Client.RemoteEndPoint + " disconnected, end of stream.");
                        break;
                    case 1:
                        Entry.Log("Client " + ID + " : " + socket.Client.RemoteEndPoint + " forcibly disconnected.");
                        break;
                    case 2:
                        Entry.Log("Client " + ID + " : " + socket.Client.RemoteEndPoint + " disconnected.");
                        break;
                    case 3:
                        Entry.Log("Client " + ID + " : " + socket.Client.RemoteEndPoint + " disconnected, timed out.");
                        break;
                    case 4:
                        Entry.Log("Client " + ID + " : " + socket.Client.RemoteEndPoint + " disconnected, did not report version.");
                        break;
                    case 5:
                        Entry.Log("Client " + ID + " : " + socket.Client.RemoteEndPoint + " disconnected, reported version: "+reportedVersion+", minimum: "+Entry.H3MPMinimumVersion);
                        break;
                }
            }
            catch (Exception)
            {
                Entry.Log("Client " + ID + " : disconnected. Could not get endpoint!.");
            }

            try
            {
                Entry.Log("\tClosing of client " + ID);
                socket.Close();
            }
            catch (Exception e)
            {
                Entry.Log("\tException caught trying to close socket of client "+ID+":\n"+e.Message+"\n"+e.StackTrace);
            }

            Entry.Log("\tNullifying client " + ID);
            stream = null!;
            receiveBuffer = null!;
            receivedData = null!;
            socket = null!;

            if (listed)
            {
                Entry.Log("\tClient " + ID+" was listed, unlisting");
                Server.hostEntries.Remove(listedID);
                ServerSend.Admin(-1, 1);
                listed = false;

                Entry.Log("Current host entry list: ");
                foreach (KeyValuePair<int, HostEntry> currentEntry in Server.hostEntries)
                {
                    Entry.Log("\t" + currentEntry.Key + ": "+ currentEntry.Value.name+ " @ " + currentEntry.Value.endPoint + " hosted by client " + currentEntry.Value.clientID);
                }
                Entry.ListClients();
            }
            Entry.Log("\tRemoving client " + ID+" from clients dict");
            Server.clients.Remove(ID);
            ServerSend.Admin(-1, 0);
            Entry.Log("\tReadding client ID " + ID + " to available list");
            Server.availableClientIDs.Add(ID);

            Entry.ListClients();
        }
    }
}
