using System;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace ChatServer
{
    internal class Program
    {
        public static Mutex mutex = new Mutex();
        public static List<TcpClient> users = new List<TcpClient>();

        public static void Accept(object listener)
        {
            byte[] buf = new byte[512];
            int count;
            while (true)
            {
                TcpClient client = ((TcpListener)listener).AcceptTcpClient();

                mutex.WaitOne();
                users.Add(client);
                count = users.Count;
                mutex.ReleaseMutex();

                IPEndPoint clientAddr = (IPEndPoint)client.Client.RemoteEndPoint;
                Console.WriteLine("[Connect] " + clientAddr.Address.ToString() + ":" + clientAddr.Port.ToString());



                client.Client.Receive(buf, 512, SocketFlags.None);
                string clientName = Encoding.Default.GetString(buf);
                clientName = clientName.Replace("\0", string.Empty);
                BroadCast(Encoding.Default.GetBytes($"CONNECT|{clientName}|{count}"));



                Thread myThread = new Thread(new ParameterizedThreadStart(Recive));
                object[] args = new object[] { client, clientName };
                myThread.Start(args);
            }
        }

        public static void Recive(object arg)
        {
            Array argArray = (Array)arg;

            string clientName = (string)argArray.GetValue(1);

            TcpClient client = (TcpClient)argArray.GetValue(0);
            IPEndPoint clientAddr = (IPEndPoint)client.Client.RemoteEndPoint;

            byte[] bytes = new byte[1024];
            int retval;

            while (true)
            {
                try
                {
                    Array.Clear(bytes, 0x0, bytes.Length);
                    retval = client.Client.Receive(bytes, 1024, SocketFlags.None);
                    if (retval == 0) break;
                    Console.WriteLine("[Recive] " + clientAddr.Address.ToString() + ":" + clientAddr.Port.ToString()
                        + " / " + Encoding.UTF8.GetString(bytes));

                    BroadCast(bytes);
                }
                catch (Exception e)
                {
                    Console.WriteLine(e.Message);
                    break;
                }

            }


            int count;
            mutex.WaitOne();
            users.Remove(client);
            count = users.Count;
            mutex.ReleaseMutex();

            BroadCast(Encoding.Default.GetBytes($"DISCONNECT|{clientName}|{count}"));
            Console.WriteLine("[Disconnect] " + clientAddr.Address.ToString() + ":" + clientAddr.Port.ToString());
            client.Close();

        }
        public static void BroadCast(object arg)
        {
            byte[] message = (byte[])arg;
            mutex.WaitOne();
            try
            {
                Console.WriteLine("Send " + Encoding.Default.GetString(message));
                for (int i = 0; i < users.Count; i++)
                {
                    TcpClient client = users[i];
                    client.Client.Send(message, message.Length, SocketFlags.None);
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
            }
            mutex.ReleaseMutex();
        }
        static void Main(string[] args)
        {
            TcpListener listener = null;

            try
            {
                listener = new TcpListener(System.Net.IPAddress.Any, 9000);
                listener.Start();
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
                Environment.Exit(1);
            }
            Console.WriteLine("[Open Server]");

            Thread acceptThread = new Thread(new ParameterizedThreadStart(Accept));
            acceptThread.Start(listener);

            acceptThread.Join();
            listener.Stop();
        }
    }
}
