using System.Globalization;
using System.Net;
using System.Net.Sockets;

namespace H3MPIndexServer
{
    public class Server
    {
        public static Dictionary<int, Client> clients = new Dictionary<int, Client>();
        public static List<int> availableClientIDs = new List<int>() { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9 };
        public static Dictionary<int, HostEntry> hostEntries = new Dictionary<int, HostEntry>();
        public static int hostEntryID = 0;
        public static int latestClient = 10;
        public delegate void PacketHandler(int clientID, Packet packet);
        public static PacketHandler[] packetHandlers;

        static TcpListener listener = null;

        public static Client adminClient;

        public static void Start(ushort port)
        {
            packetHandlers = new PacketHandler[]
            {
                ServerHandle.WelcomeReceived,
                ServerHandle.Ping,
                ServerHandle.RequestHostEntries,
                ServerHandle.List,
                ServerHandle.Unlist,
                ServerHandle.Disconnect,
                ServerHandle.Join,
                ServerHandle.PlayerCount,
                ServerHandle.ConfirmConnection,
                ServerHandle.Admin,
                ServerHandle.AdminRequest,
                ServerHandle.AdminDisconnectClient,
                ServerHandle.AdminRemoveHostEntry,
                ServerHandle.RequestModlist,
            };

            listener = new TcpListener(IPAddress.Any, port);
            listener.Start();
            listener.BeginAcceptTcpClient(ConnectCallback, null);

            Entry.Log("Server started on port: " + port);
        }

        private static void ConnectCallback(IAsyncResult result)
        {
            if(listener == null)
            {
                return;
            }

            TcpClient client = listener.EndAcceptTcpClient(result);
            listener.BeginAcceptTcpClient(ConnectCallback, null);

            Entry.Log("Incoming connection from " + client.Client.RemoteEndPoint);

            ThreadManager.ExecuteOnMainThread(() =>
            {
                int newClientID = GetNewClientID();
                Client newClient = new Client(newClientID, client);

                Entry.ListClients();
            });
        }

        private static int GetNewClientID()
        {
            Entry.Log("\tGetting new client ID");
            int availableID = -1;
            bool firstIteration = true;
            do
            {
                if (!firstIteration)
                {
                    Entry.Log("\t\tCLIENT ALREADY EXISTS WITH APPARENTLY AVAILABLE ID: " + availableID);
                }
                firstIteration = false;
                // Add 10 entries to available client IDs if necessary
                if (availableClientIDs.Count == 0)
                {
                    for (int i = latestClient; i < latestClient + 10; ++i)
                    {
                        availableClientIDs.Add(i);
                    }
                    latestClient += 10;
                }

                // Get an available ID
                availableID = availableClientIDs[availableClientIDs.Count - 1];
                availableClientIDs.RemoveAt(availableClientIDs.Count - 1);
            }
            while (clients.ContainsKey(availableID));
            Entry.Log("\tGot new client ID: "+availableID);

            return availableID;
        }

        public static EndPoint GetPublicEndPoint(EndPoint currentEndPoint)
        {
            if(currentEndPoint != null)
            {
                string IP = currentEndPoint.ToString().Split(":")[0];
                if (IP.Equals("127.0.0.1") || IP.StartsWith("192.168."))
                {
                    string url = "http://ipinfo.io/ip";
                    string content = "";
                    using (HttpClient client = new HttpClient())
                    {
                        var response = client.GetAsync(url).GetAwaiter().GetResult();
                        if (response.IsSuccessStatusCode)
                        {
                            var responseContent = response.Content;
                            content = responseContent.ReadAsStringAsync().GetAwaiter().GetResult();
                        }
                    }
                    content = content.Trim();
                    string[] split = content.Split(".");
                    byte[] IPBytes = new byte[4];
                    for(int i=0; i < 4; ++i)
                    {
                        IPBytes[i] = byte.Parse(split[i]);
                    }

                    // This is a local IP, must convert to public
                    IPEndPoint IPEP = (IPEndPoint)currentEndPoint;
                    IPEP.Address = new IPAddress(IPBytes);

                    return IPEP;
                }

                return currentEndPoint;
            }

            return null;
        }
    }
}
