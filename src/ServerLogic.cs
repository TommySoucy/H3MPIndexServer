namespace H3MPIndexServer
{
    public class ServerLogic
    {
        public static void Update(float delta)
        {
            ThreadManager.UpdateMain();

            List<Client> toDisconnect = new List<Client>();
            foreach (KeyValuePair<int, Client> clientEntry in Server.clients)
            {
                bool disc = false;
                if (!clientEntry.Value.welcomed)
                {
                    clientEntry.Value.welcomeTimer += delta;

                    if (clientEntry.Value.welcomeTimer > 30)
                    {
                        toDisconnect.Add(clientEntry.Value);
                        disc = true;
                    }
                }

                if (!disc)
                {
                    clientEntry.Value.pingTimer += delta;

                    if (clientEntry.Value.pingTimer > 60)
                    {
                        Entry.Log("No action to execute on main thread!");
                        toDisconnect.Add(clientEntry.Value);
                    }
                }
            }

            if(toDisconnect.Count > 0)
            {
                for(int i = toDisconnect.Count - 1; i >= 0; --i)
                {
                    toDisconnect[i].Disconnect(3);
                }
            }
        }
    }
}
