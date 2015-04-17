using System;
using Windows.Foundation;
using Windows.Networking;
using Windows.Networking.Sockets;
using Windows.Storage.Streams;
using System.Diagnostics;

namespace RobotApp
{
    public static class NetworkCmd
    {
        // if no host, be client, otherwise be a host
        private static String hostName = "";
        private const String hostPort = "8027";

        public static void NetworkInit(String host)
        {
            hostName = host;
            if (hostName.Length > 0) 
            {
                InitConnectionToHost(); 
            }
            else 
            {
                if (listener == null) StartListener(); 
            }
        }

        public static ulong msLastSendTime;

        static String ctrlStringToSend;
        public static void SendCommandToRobot(String stringToSend)
        {
            ctrlStringToSend = stringToSend + ".";
            if (hostName.Length > 0) PostSocketWrite(ctrlStringToSend);
            Debug.WriteLine("Sending: " + ctrlStringToSend);
        }


        #region ----- host connection ----
        static StreamSocketListener listener;
        public static async void StartListener()
        {
            try
            {
                listener = new StreamSocketListener();
                listener.ConnectionReceived += OnConnection;
                await listener.BindServiceNameAsync(hostPort);
                Debug.WriteLine("Listening on {0}", hostPort);
            }
            catch (Exception e)
            {
                Debug.WriteLine("StartListener() - Unable to bind listener. " + e.Message);
            }
        }

        static async void OnConnection(StreamSocketListener sender, StreamSocketListenerConnectionReceivedEventArgs args)
        {
            try
            {
                if (MainPage.isRobot)
                {
                    DataReader reader = new DataReader(args.Socket.InputStream);
                    String str = "";
                    while (true)
                    {
                        uint len = await reader.LoadAsync(1);
                        if (len > 0)
                        {
                            byte b = reader.ReadByte();
                            str += Convert.ToChar(b);
                            if (b == '.')
                            {
                                Debug.WriteLine("Network Received: '{0}'", str);
                                Controllers.ParseCtrlMessage(str);
                                break;
                            }
                        }
                    }
                }
                else
                {
                    String lastStringSent;
                    while (true)
                    {
                        DataWriter writer = new DataWriter(args.Socket.OutputStream);
                        lastStringSent = ctrlStringToSend;
                        writer.WriteString(lastStringSent);
                        await writer.StoreAsync();
                        msLastSendTime = (ulong)(MainPage.stopwatch.ElapsedMilliseconds);

                        // re-send periodically
                        ulong msStart = (ulong)(MainPage.stopwatch.ElapsedMilliseconds);
                        for (; ;)
                        {
                            ulong msCurrent = (ulong)(MainPage.stopwatch.ElapsedMilliseconds);
                            if ((msCurrent - msStart) > 3000) break;
                            if (lastStringSent.CompareTo(ctrlStringToSend) != 0) break;
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Debug.WriteLine("OnConnection() - " + e.Message);
            }
        }
        #endregion

        #region ----- client connection -----
        static StreamSocket socket;
        static bool socketIsConnected;
        private static async void InitConnectionToHost()
        {
            try
            {
                if (socket != null)
                {
                    socket.Dispose();
                    socket = null;
                    socketIsConnected = false;
                }
                socket = new StreamSocket();

                HostName hostNameObj = new HostName(hostName);
                await socket.ConnectAsync(hostNameObj, hostPort);
                Debug.WriteLine("Connected to {0}:{1}.", hostNameObj, hostPort);
                socketIsConnected = true;

                if (MainPage.isRobot) PostSocketRead(1024);
            }
            catch (Exception ex)
            {
                Debug.WriteLine("InitConnectionToHost() - " + ex.Message);
            }
        }

        public static void OnDataReadCompletion(uint bytesRead, DataReader readPacket)
        {
            if (readPacket == null)
            {
                Debug.WriteLine("DataReader is null");
                return;
            }
            uint buffLen = readPacket.UnconsumedBufferLength;

            if (buffLen == 0)
            {
                // buflen==0 - assume server closed socket
                Debug.WriteLine("Attempting to disconnect and reconnecting to the server");
                InitConnectionToHost();
                return;
            }

            string message = readPacket.ReadString(buffLen);
            Debug.WriteLine("Network Received (b={0},l={1}): '{2}'", bytesRead, buffLen, message);

            Controllers.ParseCtrlMessage(message);

            PostSocketRead(1024);
        }

        static DataReader readPacket;
        static void PostSocketRead(int length)
        {
            if (socket == null || !socketIsConnected)
            {
                Debug.WriteLine("Rd: Socket not connected yet.");
                return;
            }

            try
            {
                var readBuf = new Windows.Storage.Streams.Buffer((uint)length);
                var readOp = socket.InputStream.ReadAsync(readBuf, (uint)length, InputStreamOptions.Partial);
                readOp.Completed = (IAsyncOperationWithProgress<IBuffer, uint> asyncAction, AsyncStatus asyncStatus) =>
                {
                    switch (asyncStatus)
                    {
                        case AsyncStatus.Completed:
                        case AsyncStatus.Error:
                            try
                            {
                                IBuffer localBuf = asyncAction.GetResults();
                                uint bytesRead = localBuf.Length;
                                readPacket = DataReader.FromBuffer(localBuf);
                                OnDataReadCompletion(bytesRead, readPacket);
                            }
                            catch (Exception exp)
                            {
                                Debug.WriteLine("Read operation failed:  " + exp.Message);
                            }
                            break;
                        case AsyncStatus.Canceled:
                            break;
                    }
                };
            }
            catch (Exception exp)
            {
                Debug.WriteLine("Failed to post a Read - " + exp.Message);
            }
        }

        static async void PostSocketWrite(string writeStr)
        {
            if (socket == null || !socketIsConnected)
            {
                Debug.WriteLine("Wr: Socket not connected yet.");
                return;
            }

            try
            {
                DataWriter writer = new DataWriter(socket.OutputStream);
                writer.WriteString(writeStr);
                await writer.StoreAsync();
                msLastSendTime = (ulong)(MainPage.stopwatch.ElapsedMilliseconds);
            }
            catch (Exception exp)
            {
                Debug.WriteLine("Failed to Write - " + exp.Message);
            }
        }

        #endregion

    }
}
