using System.Net;

namespace H3MPIndexServer
{
    public class HostEntry
    {
        public int ID;
        public int clientID;
        public IPEndPoint endPoint;
        public string name = "";
        public int playerCount;
        public int limit;
        public bool hasPassword;
        public string passwordHash;
        public int modlistEnforcement;
        public List<string> modlist;
    }
}
