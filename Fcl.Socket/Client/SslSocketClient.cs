using System;
using System.IO;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;

namespace Fcl.Sockets.Client
{
    public class SslSocketClient : IClient
    {
        #region Properties
        protected ILogger Logger { get; set; }
        public string ServerHost { get; set; }
        public int ServerPort { get; set; }
        public int SendTimeout { get; set; }
        public int ReceiveTimeout { get; set; }
        public bool KeepAlive { get; set; }
        protected Socket Client { get; set; }
        private NetworkStream networkStream { get; set; }
        private SslStream sslStream { get; set; }
        private X509CertificateCollection certs
        {
            get; set;
        }
        #endregion

        public SslSocketClient(ILoggerFactory loggerFactory, string url, string certFile, int sendTimeout = 30, int receiveTimeout = 30, bool keepAlive = true)
        {
            #region Initialize Properties
            this.Logger = loggerFactory.CreateLogger<SocketClient>();
            this.ParseURL(url);
            this.ReceiveTimeout = receiveTimeout;
            this.SendTimeout = sendTimeout;
            this.KeepAlive = keepAlive;
            if (String.IsNullOrWhiteSpace(certFile))
            {
                throw new Exception($"Cannot find certificate file!");
            }
            X509Certificate cert = X509Certificate.CreateFromCertFile(certFile);
            certs = new X509CertificateCollection();
            certs.Add(cert);
            #endregion
        }

        public SslSocketClient(ILoggerFactory loggerFactory, string serverHost, int serverPort, string certFile, int sendTimeout = 30, int receiveTimeout = 30, bool keepAlive = true)
        {
            #region Initialize Properties
            this.Logger = loggerFactory.CreateLogger<SocketClient>();
            this.ServerHost = serverHost;
            this.ServerPort = serverPort;
            this.ReceiveTimeout = receiveTimeout;
            this.SendTimeout = sendTimeout;
            this.KeepAlive = keepAlive;
            if (String.IsNullOrWhiteSpace(certFile))
            {
                new Exception($"Cannot find certificate file!");
            }
            X509Certificate cert = X509Certificate.CreateFromCertFile(certFile);
            certs = new X509CertificateCollection();
            certs.Add(cert);
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
                networkStream = new NetworkStream(this.Client, true);
                sslStream = new SslStream(networkStream, false, new RemoteCertificateValidationCallback(ValidateServerCertificate), null);
                sslStream.WriteTimeout = this.SendTimeout * 1000;
                sslStream.ReadTimeout = this.ReceiveTimeout * 1000;
                sslStream.AuthenticateAsClient(this.ServerHost, certs, SslProtocols.Tls12, true);
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
            int bytesRead = this.sslStream.Read(buffer, 0, buffer.Length);
            if (bytesRead > 0)
            {
                ms.Write(buffer, 0, bytesRead);
            }
            //sslStream can only be received once.Therefore, return it immediately
            return ms.ToArray();
        }

        public void Send(byte[] writeBytes)
        {
            if (!this.Client.Connected)
            {
                throw new SocketException((int)SocketError.NotConnected);
            }
            sslStream.Write(writeBytes);
            sslStream.Flush();
        }

        public void Close()
        {
            try
            {
                this.sslStream.Close();
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

        /// <summary>
        /// verify SSL certificate 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="certificate"></param>
        /// <param name="chain"></param>
        /// <param name="sslPolicyErrors"></param>
        /// <returns></returns>
        private bool ValidateServerCertificate(object sender, X509Certificate certificate, X509Chain chain, SslPolicyErrors sslPolicyErrors)
        {
            const SslPolicyErrors ignoredErrors =
                SslPolicyErrors.RemoteCertificateChainErrors |  // self-signed
                SslPolicyErrors.RemoteCertificateNameMismatch;  // name mismatch
            if ((sslPolicyErrors & ~ignoredErrors) == SslPolicyErrors.None)
            {
                return true;
            }
            return false;
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
