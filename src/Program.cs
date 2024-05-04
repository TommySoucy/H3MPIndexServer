using H3MPIndexServer;
using System.Diagnostics;

class Entry
{
    public const int TICKS_PER_SEC = 30;
    public const int MS_PER_TICK = 1000 / TICKS_PER_SEC;
    private static bool running = false;
    public const string hash = "";
    public const string version = "1.2.1";
    public const string H3MPMinimumVersion = "1.9.9";

    static int Main(string[] args)
    {
        running = true;

        ushort port = 0;

        // Handle arguments
        for(int i=0; i < args.Length; ++i)
        {
            switch (args[i])
            {
                case "-p":
                    if(args.Length > i + 1 && ushort.TryParse(args[i+1], out ushort parsedPort))
                    {
                        port = parsedPort;
                    }
                    else
                    {
                        Entry.LogError("Port could not be parsed");
                        return 1;
                    }
                    break;
            }
        }

        if(port == 0)
        {
            Entry.Log("Port not specified");
            return 1;
        }

        Entry.Log("Starting H3MP Index Server version "+version);

        Thread mainThread = new Thread(new ThreadStart(MainThread));
        mainThread.Start();

        Server.Start(port);

        Entry.Log("Press any key to close the server");
        Console.ReadKey();

        running = false;

        return 0;
    }

    private static void MainThread()
    {
        Entry.Log("Main thread started");
        DateTime nextLoop = DateTime.Now;
        Stopwatch stopwatch = Stopwatch.StartNew();

        while (running)
        {
            while(nextLoop < DateTime.Now)
            {
                ServerLogic.Update(((float)stopwatch.ElapsedTicks) / Stopwatch.Frequency);

                stopwatch.Restart();

                nextLoop = nextLoop.AddMilliseconds(MS_PER_TICK);

                if(nextLoop > DateTime.Now)
                {
                    Thread.Sleep(nextLoop - DateTime.Now);
                }
            }
        }
    }

    public static void Log(string s)
    {
        ServerSend.Log(s);
        Console.WriteLine(DateTime.Now.ToString() + ": " + s);
    }

    public static void LogWarning(string s)
    {
        ServerSend.LogWarning(s);
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine(DateTime.Now.ToString() + ": " + s);
        Console.ForegroundColor = ConsoleColor.White;
    }

    public static void LogError(string s)
    {
        ServerSend.LogError(s);
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine(DateTime.Now.ToString() + ": "+ s);
        Console.ForegroundColor = ConsoleColor.White;
    }

    public static void ListClients()
    {
        Entry.Log("Current client list: ");
        foreach (KeyValuePair<int, Client> currentClient in Server.clients)
        {
            if (currentClient.Value.socket != null && currentClient.Value.socket.Client != null && currentClient.Value.socket.Client.RemoteEndPoint != null)
            {
                Entry.Log("\t" + currentClient.Key + " @ " + currentClient.Value.socket.Client.RemoteEndPoint);
            }
            else
            {
                Entry.Log("\t" + currentClient.Key + " @ NO SOCKET");
            }
        }
    }
}
