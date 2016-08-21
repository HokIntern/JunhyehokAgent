using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using Junhaehok;
using static Junhaehok.Packet;
using static Junhaehok.HhhHelper;
using System.IO.MemoryMappedFiles;
using System.Runtime.InteropServices;
using System.Threading;
using System.IO;
using System.Diagnostics;

namespace JunhyehokAgent
{
    class ReceiveHandle
    {
        ClientHandle client;
        Packet recvPacket;

        public static Socket admin;
        public static bool adminAlive;
        public static Socket backend;
        static string mmfName;
        static MemoryMappedFile mmf;
        static MemoryMappedFile mmfIpx;
        readonly Header NoResponseHeader = new Header(ushort.MaxValue, 0);
        readonly Packet NoResponsePacket = new Packet(new Header(ushort.MaxValue, 0), null);

        public ReceiveHandle(Socket backendSocket, string mmfNombre)
        {
            backend = backendSocket;
            mmfName = mmfNombre;
            //Initialize MMF
            mmf = MemoryMappedFile.CreateOrOpen(mmfName, Marshal.SizeOf(typeof(AAServerInfoResponse)));
            mmfIpx = MemoryMappedFile.CreateOrOpen(mmfName + "IPX", 1);
            //Initialize Lock
            bool mutexCreated;
            Mutex mutex = new Mutex(true, "MMF_IPC" + mmfName, out mutexCreated);
            mutex.ReleaseMutex();
            Mutex mutexIpx = new Mutex(true, "MMF_IPX" + mmfName, out mutexCreated);
            //Write 1 to file to indicate started
            byte[] start = { 1 };
            using (var accessor = mmfIpx.CreateViewAccessor(0, start.Length))
            {
                // Write to MMF
                accessor.WriteArray<byte>(0, start, 0, start.Length);
            }
            mutexIpx.ReleaseMutex();
            adminAlive = true;
        }

        public ReceiveHandle(ClientHandle client, Packet recvPacket)
        {
            this.client = client;
            this.recvPacket = recvPacket;
        }
        //==========================================SERVER_START 1200===========================================
        //==========================================SERVER_START 1200===========================================
        //==========================================SERVER_START 1200===========================================
        public Packet ResponseServerStart(Packet recvPacket)
        {
            if (!adminAlive)
            {
                //Write 1 to file to indicate started
                byte[] start = { 1 };
                Mutex mutex = Mutex.OpenExisting("MMF_IPX" + mmfName);
                mutex.WaitOne();
                using (var accessor = mmfIpx.CreateViewAccessor(0, start.Length))
                {
                    // Write to MMF
                    accessor.WriteArray<byte>(0, start, 0, start.Length);
                }
                mutex.ReleaseMutex();
                string arg = "-cp 30000 -mmf " + mmfName;
                Process.Start("C:\\Users\\hokjoung\\Documents\\Visual Studio 2015\\Projects\\JunhyehokServer\\JunhyehokServer\\bin\\Release\\JunhyehokServer.exe", arg);
                adminAlive = true;
            }
            return NoResponsePacket;
        }
        //=========================================SERVER_RESTART 1240==========================================
        //=========================================SERVER_RESTART 1240==========================================
        //=========================================SERVER_RESTART 1240==========================================
        public Packet ResponseServerRestart(Packet recvPacket)
        {
            Packet throwaway;
            if (adminAlive)
            {
                throwaway = ResponseServerStop(recvPacket);
                adminAlive = false;
            }
            Thread.Sleep(500);
            throwaway = ResponseServerStart(recvPacket);
            return NoResponsePacket;
        }
        //==========================================SERVER_STOP 1270============================================
        //==========================================SERVER_STOP 1270============================================
        //==========================================SERVER_STOP 1270============================================
        public Packet ResponseServerStop(Packet recvPacket)
        {
            if (adminAlive)
            {
                byte[] stop = { 0 };
                Mutex mutex = Mutex.OpenExisting("MMF_IPX" + mmfName);
                mutex.WaitOne();

                // Create Accessor to MMF
                using (var accessor = mmfIpx.CreateViewAccessor(0, stop.Length))
                {
                    // Write to MMF
                    accessor.WriteArray<byte>(0, stop, 0, stop.Length);
                }
                mutex.ReleaseMutex();
                adminAlive = false;
            }
            return NoResponsePacket;
        }
        //==========================================SERVER_INFO 1300============================================
        //==========================================SERVER_INFO 1300============================================
        //==========================================SERVER_INFO 1300============================================
        public Packet ResponseServerInfo(Packet recvPacket)
        {
            AAServerInfoResponse aaServerInfoResp = new AAServerInfoResponse();
            byte[] aaServerInfoRespBytes = new byte[Marshal.SizeOf(aaServerInfoResp)];

            // Create named MMF
            Console.WriteLine("[MEMORYMAPPED FILE] Reading from MMF: ({0})...", mmfName);
            // Create accessor to MMF
            using (var accessor = mmf.CreateViewAccessor(0, Marshal.SizeOf(aaServerInfoResp)))
            {
                // Wait for the Lock
                Mutex mutex = Mutex.OpenExisting("MMF_IPC" + mmfName);
                mutex.WaitOne();

                // Read from MMF
                accessor.ReadArray<byte>(0, aaServerInfoRespBytes, 0, aaServerInfoRespBytes.Length);
                mutex.ReleaseMutex();
            }

            return new Packet(new Header(Code.SERVER_INFO_SUCCESS, (ushort)aaServerInfoRespBytes.Length), aaServerInfoRespBytes);
            //aaServerInfoResp = (AAServerInfoResponse)Serializer.ByteToStructure(aaServerInfoRespBytes, typeof(AAServerInfoResponse));
        }
        //============================================RANKINGS 1400=============================================
        //============================================RANKINGS 1400=============================================
        //============================================RANKINGS 1400=============================================
        public Packet ResponseRankings(Packet recvPacket)
        {
            Header reqHeader = new Header(Code.RANKINGS, 0);
            Packet reqPacket = new Packet(reqHeader, null);
            backend.SendBytes(reqPacket);
            return NoResponsePacket;
        }
        //======================================RANKINGS_SUCCESS 1402===========================================
        //======================================RANKINGS_SUCCESS 1402===========================================
        //======================================RANKINGS_SUCCESS 1402===========================================
        public Packet ResponseRankingsSuccess(Packet recvPacket)
        {
            return ForwardPacket(recvPacket);
        }

        //=============================================SWITCH CASE============================================
        //=============================================SWITCH CASE============================================
        //=============================================SWITCH CASE============================================
        public Packet GetResponse()
        {
            Packet responsePacket = new Packet();
            string remoteHost = ((IPEndPoint)client.So.RemoteEndPoint).Address.ToString();
            string remotePort = ((IPEndPoint)client.So.RemoteEndPoint).Port.ToString();
            bool debug = true;

            if (debug && recvPacket.header.code != Code.HEARTBEAT && recvPacket.header.code != Code.HEARTBEAT_SUCCESS && recvPacket.header.code != ushort.MaxValue - 1)
            {
                Console.WriteLine("\n[Client] {0}:{1}", remoteHost, remotePort);
                Console.WriteLine("==RECEIVED: \n" + PacketDebug(recvPacket));
            }

            switch (recvPacket.header.code)
            {
                //------------No action from client----------
                case ushort.MaxValue - 1:
                    responsePacket = new Packet(new Header(Code.HEARTBEAT, 0), null);
                    break;

                //------------SERVER---------
                case Code.SERVER_INFO:
                    responsePacket = ResponseServerInfo(recvPacket);
                    break;
                case Code.SERVER_START:
                    responsePacket = ResponseServerStart(recvPacket);
                    break;
                case Code.SERVER_RESTART:
                    responsePacket = ResponseServerRestart(recvPacket);
                    break;
                case Code.SERVER_STOP:
                    responsePacket = ResponseServerStop(recvPacket);
                    break;
                //-----------RANKINGS--------
                case Code.RANKINGS:
                    responsePacket = ResponseRankings(recvPacket);
                    break;
                case Code.RANKINGS_SUCCESS:
                    responsePacket = ResponseRankingsSuccess(recvPacket);
                    break;

                default:
                    if (debug)
                        Console.WriteLine("Unknown code: {0}\n", recvPacket.header.code);
                    break;
            }

            //===============Build Response/Set Surrogate/Return================
            if (debug && responsePacket.header.code != ushort.MaxValue && responsePacket.header.code != Code.HEARTBEAT && responsePacket.header.code != Code.HEARTBEAT_SUCCESS)
            {
                Console.WriteLine("\n[Client] {0}:{1}", remoteHost, remotePort);
                Console.WriteLine("==SEND: \n" + PacketDebug(responsePacket));
            }

            return responsePacket;
        }
        private Packet ForwardPacket(Packet recvPacket)
        {
            admin.SendBytes(recvPacket);
            return NoResponsePacket;
        }
    }
}
