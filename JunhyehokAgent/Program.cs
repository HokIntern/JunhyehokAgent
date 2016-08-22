using System;
using System.Collections.Generic;
using System.IO.MemoryMappedFiles;
using System.Runtime.InteropServices;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Junhaehok;
using System.Threading;
using System.IO;
using System.Net.Sockets;
using System.Net;
using System.Diagnostics;

namespace JunhyehokAgent
{
    class Program
    {
        static void Main(string[] args)
        {
            string host = null;     //Default
            string clientPort = "40000";  //Default
            string mmfName = "JunhyehokMmf"; //Default
            string connection_type = "tcp";
            TcpServer echoc;

            //=========================GET ARGS=================================
            if (args.Length == 0)
            {
                Console.WriteLine("Format: JunhyehokAgent -mmf [MMF name] -ct [Connection Type]");
                Environment.Exit(0);
            }

            for (int i = 0; i < args.Length; i++)
            {
                switch (args[i])
                {
                    case "--help":
                        Console.WriteLine("Format: JunhyehokAgent -mmf [MMF name] -ct [Connection Type]");
                        Environment.Exit(0);
                        break;
                    case "-mmf":
                        mmfName = args[++i];
                        break;
                    case "-ct":
                        connection_type = args[++i];
                        break;
                    default:
                        Console.Error.WriteLine("ERROR: incorrect inputs \nFormat: JunhyehokAgent -mmf [MMF name] -ct [Connection Type]");
                        Environment.Exit(0);
                        break;
                }
            }

            //======================SOCKET BIND/LISTEN==========================
            /* if only given port, host is ANY */
            echoc = new TcpServer(host, clientPort);

            //======================BACKEND CONNECT===============================
            Console.WriteLine("Connecting to Backend...");
            string backendInfo = "";
            try { backendInfo = System.IO.File.ReadAllText("agentBackend.conf"); }
            catch (Exception e) { Console.WriteLine("\n" + e.Message); Environment.Exit(0); }
            Socket backendSocket = Connect(backendInfo);
            ClientHandle backend = new ClientHandle(backendSocket);
            backend.StartSequence();

            //======================INITIALIZE==================================
            Console.WriteLine("Initializing lobby and rooms...");
            ReceiveHandle recvHandle = new ReceiveHandle(backendSocket, mmfName, connection_type);

            //=====================START FRONTEND SERVER========================
            Console.WriteLine("Starting Frontend Server...");
            if (connection_type == "web")
            {
                string arg = "-cp 38080 -mmf " + mmfName;
                Process.Start("JunhyehokWebServer.exe", arg);
                //Process.Start("C:\\Users\\hokjoung\\Documents\\Visual Studio 2015\\Projects\\JunhyehokWebServer\\JunhyehokWebServer\\bin\\Release\\JunhyehokWebServer.exe", arg);
            }
            else if (connection_type == "tcp")
            {
                string arg = "-cp 30000 -mmf " + mmfName;
                Process.Start("JunhyehokServer.exe", arg);
                //Process.Start("C:\\Users\\hokjoung\\Documents\\Visual Studio 2015\\Projects\\JunhyehokServer\\JunhyehokServer\\bin\\Release\\JunhyehokServer.exe", arg);
            }
            else
            {
                Console.WriteLine("ERROR: Wrong Connection type. Exiting...");
                Environment.Exit(0);
            }

            //===================CLIENT SOCKET ACCEPT===========================
            Console.WriteLine("Accepting clients...");
            
            while (true)
            {
                Socket s = echoc.so.Accept();
                ClientHandle client = new ClientHandle(s);
                ReceiveHandle.admin = s;
                client.StartSequence();
            }
        }
        public static Socket Connect(string info)
        {
            string host;
            int port;
            string[] hostport = info.Split(':');
            host = hostport[0];
            if (!int.TryParse(hostport[1], out port))
            {
                Console.Error.WriteLine("port must be int. given: {0}", hostport[1]);
                Environment.Exit(0);
            }

            Socket so = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            IPAddress ipAddress = IPAddress.Parse(host);

            Console.WriteLine("Establishing connection to {0}:{1} ...", host, port);

            try
            {
                so.Connect(ipAddress, port);
                Console.WriteLine("Connection established.\n");
            }
            catch (Exception)
            {
                Console.WriteLine("Peer is not alive.");
            }

            return so;
        }
    }
}
