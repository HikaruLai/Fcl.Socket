using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;

namespace Fcl.Sockets.Client
{
    public class SocketClient : IClient
    {
        #region Properties
        protected ILogger Logger { get; set; }
        public string ServerHost { get; set; }
        public int ServerPort { get; set; }
        public int SendTimeout { get; set; }
        public int ReceiveTimeout { get; set; }
        public bool KeepAlive { get; set; }
        protected Socket Client { get; set; }
        #endregion

        public SocketClient(ILoggerFactory loggerFactory, string url, int sendTimeout = 30, int receiveTimeout = 30, bool keepAlive = true)
        {
            #region Initialize Properties
            this.Logger = loggerFactory.CreateLogger<SocketClient>();
            this.ParseURL(url);
            this.ReceiveTimeout = receiveTimeout;
            this.SendTimeout = sendTimeout;
            this.KeepAlive = keepAlive;
            #endregion
        }

        public SocketClient(ILoggerFactory loggerFactory, string serverHost, int serverPort, int sendTimeout = 30, int receiveTimeout = 30, bool keepAlive = true)
        {
            #region Initialize Properties
            this.Logger = loggerFactory.CreateLogger<SocketClient>();
            this.ServerHost = serverHost;
            this.ServerPort = serverPort;
            this.ReceiveTimeout = receiveTimeout;
            this.SendTimeout = sendTimeout;
            this.KeepAlive = keepAlive;
            #endregion
        }

        public bool IsKeepAlive()
        {
            return this.KeepAlive;
        }

        public void Open()
        {
            try
            {
                if (null != Client)
                {
                    this.Client.Close();
                }
                IPEndPoint remoteEP;
                this.Client = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                // use only ip address
                remoteEP = new IPEndPoint(IPAddress.Parse(this.ServerHost), this.ServerPort);
                this.Client.Connect(remoteEP);
                this.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.SendTimeout, this.SendTimeout * 1000);
                this.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReceiveTimeout, this.ReceiveTimeout * 1000);
                this.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
                if (IsKeepAlive())
                {
                    this.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.KeepAlive, true);
                    uint dummy = 0;
                    // marshal the equivalent of the native structure into a byte array
                    byte[] inOptionValues = new byte[Marshal.SizeOf(dummy) * 3];
                    // turn keep-alive on
                    BitConverter.GetBytes((uint)1).CopyTo(inOptionValues, 0);
                    // set amount of time without activity before sending a keep-alive
                    BitConverter.GetBytes((uint)5000).CopyTo(inOptionValues, Marshal.SizeOf(dummy));
                    // set keep-alive interval
                    BitConverter.GetBytes((uint)5000).CopyTo(inOptionValues, Marshal.SizeOf(dummy) * 2);
                    // write SIO_VALS to Socket IOControl
                    this.Client.IOControl(IOControlCode.KeepAliveValues, inOptionValues, null);
                }
            }
            catch (SocketException ex)
            {
                Logger.LogDebug("[SocketClient][Open] Error:" + ex.Message);
            }
            catch (Exception ex)
            {
                Logger.LogError($"Exception Error: {ex.Message}, {ex.StackTrace}");
            }
        }

        public byte[] Receive()
        {
            byte[] buffer = new byte[0x1000]; // blocks of 1K.
            MemoryStream ms = new MemoryStream();
            int bytesRead = 0;
            while (true)
            {
                bytesRead = this.Client.Receive(buffer, 0, buffer.Length, SocketFlags.None);
                if (bytesRead > 0)
                {
                    ms.Write(buffer, 0, bytesRead);
                }
                if (bytesRead == 0 || bytesRead < buffer.Length)
                {
                    return ms.ToArray();
                }
            }
        }

        public void Send(byte[] writeBytes)
        {
            if (IsKeepAlive() && !CheckIsConnected())
            {
                Logger.LogDebug("Getting Server Disconnected...  Trying to Reconnect...");
                this.Open();
            }
            this.Client.Send(writeBytes, SocketFlags.None);
        }

        public void Close()
        {
            try
            {
                if (!this.Client.Connected)
                    return;
                // Release the socket.
                this.Client.Shutdown(SocketShutdown.Both);
                this.Client.Dispose();
                this.Client = null;
            }
            catch (Exception ex)
            {
                Logger.LogDebug("Close Exception:{0}, {1}", ex.Message, ex.StackTrace);
            }
        }

        private bool CheckIsConnected()
        {
            byte[] testByte = new byte[1];
            int bytesRead = 0;
            try
            {
                if (this.Client.Connected && this.Client.Poll(0, SelectMode.SelectRead))
                {
                    if (this.Client.Available == 0)
                    {
                        return false;
                    }
                    Logger.LogDebug("Poll OK...");
                    bytesRead = this.Client.Receive(testByte, SocketFlags.Peek);
                    if (bytesRead == 1)
                    {
                        Logger.LogDebug("Peek OK....");
                        return true;
                    }
                    else
                    {
                        //bytesRead == 0
                        Logger.LogDebug("Peek fail...");
                        return false;
                    }
                }
                return false;
            }
            catch (Exception ex)
            {
                // re-send
                Logger.LogError($"{ex.Message}, {ex.StackTrace}");
                return false;
            }
        }

        private void ParseURL(string url)
        {
            string pattern = @"^tcp://(?'IP'[^:]+):(?'PORT'.*)$";
            Regex re = new Regex(pattern);
            Match ma = re.Match(url);
            if (ma.Success)
            {
                this.ServerHost = ma.Groups["IP"].Value;
                this.ServerPort = Convert.ToInt32(ma.Groups["PORT"].Value);
            }
            else
            {
                string errStr = string.Format("URL match fail:[{0}]", url);
                Logger.LogError(errStr);
                throw new Exception(errStr);
            }
        }

        #region Dispose method
        protected bool disposed = false;

        public void Dispose()
        {
            this.Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            // Check to see if Dispose has already been called.
            if (!this.disposed)
            {
                // If disposing equals true, dispose all managed and unmanaged resources.
                if (disposing)
                {
                    if (this.Client != null)
                    {
                        this.Close();
                    }
                }
                this.disposed = true;
            }
        }
        #endregion
    }
}
