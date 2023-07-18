using Microsoft.Extensions.Logging;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Fcl.Sockets.Handler;
using Fcl.Sockets.State;

namespace Fcl.Sockets.Server
{
    public abstract class AbsServer : IServer
    {
        #region Properties
        protected ILogger Logger { get; set; }

        /// <summary>
        /// thread signal for socket accept
        /// </summary>
        protected ManualResetEvent AllDone { get; set; }
        public string HostName { get; set; }
        public int Port { get; set; }
        public int BackLog { get; set; }
        public virtual string InitState { get; set; }
        private bool keepListening { get; set; }
        private ConcurrentDictionary<int, IClientRequestHandler> dicHandler = new ConcurrentDictionary<int, IClientRequestHandler>();
        private int clientNo = 0;
        private readonly object lockObj = new object();
        private Socket listener { get; set; }
        #endregion

        public virtual void Stop()
        {
            this.RemoveAllClients();
            this.AllDone.Set();
            this.keepListening = false;
        }

        public virtual void Start()
        {
            Task.Run(() => { this.BeginStart(); });
        }

        private void BeginStart()
        {
            Logger.LogDebug($"portNumber: {this.Port}");
            Logger.LogDebug($"backlog: {this.BackLog}");
            this.keepListening = true;
            // Establish the local endpoint for the socket.
            try
            {
                IPEndPoint serviceEP;
                if (this.IsIpAddr(this.HostName))
                {
                    serviceEP = new IPEndPoint(IPAddress.Parse(this.HostName), this.Port);
                }
                else
                {
                    serviceEP = new IPEndPoint
                    (
                        Dns.GetHostEntry(this.HostName).AddressList.First
                        (
                            p => p.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork
                        )
                      , this.Port
                    );
                }
                //If TCP Listner is working, stop it
                if (this.listener != null)
                {
                    try
                    {
                        this.listener.Close();
                        this.listener.Dispose();
                        this.listener = null;
                    }
                    catch (Exception ex)
                    {
                        Logger.LogWarning($"Before start server: {ex.Message}, {ex.StackTrace}");
                    }
                }

                // Create a TCP/IP socket.
                this.listener = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                // Bind the socket to the local endpoint and listen for incoming connections.
                this.listener.Bind(serviceEP);
                this.listener.Listen(this.BackLog);

                while (this.keepListening)
                {
                    try
                    {
                        // Set the event to nonsignaled state. close the gate
                        this.AllDone.Reset();

                        // Start an asynchronous socket to listen for connections.
                        Logger.LogDebug($"Accept[{this.clientNo}], Waiting for a connection...") ;
                        this.listener.BeginAccept(new AsyncCallback(AcceptCallback), this.listener);

                        // gate is closed, blocking this thread, Wait until a connection is made before continuing.
                        this.AllDone.WaitOne();
                    }
                    catch (Exception ex)
                    {                        
                        Logger.LogError($"Server Keep Listening error: {ex.Message}, {ex.StackTrace}");
                    }
                }
                // close listener
                Logger.LogDebug($"do shutdown....");
                if (this.listener != null)
                {
                    try
                    {
                        this.listener.Close();
                        this.listener.Dispose();
                        this.listener = null;
                    }
                    catch (Exception ex)
                    {
                        Logger.LogError($"shutdown error: {ex.Message}, {ex.StackTrace}");
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"Server error: {ex.Message}, {ex.StackTrace}");
            }
        }

        /// <summary>
        /// asynchronous callback delegate
        /// </summary>
        /// <param name="ar">store information about an asynchronous operation</param>
        private void AcceptCallback(IAsyncResult ar)
        {
            Socket socketClient = null;
            try
            {
                if (!this.keepListening)
                {
                    Logger.LogDebug($"keepListening status: {this.keepListening}");
                    return;
                }
                // Get the socket that handles the client request.
                // AsyncState : user-defined object that contains information about the asynchronous operation
                Socket listener = (Socket)ar.AsyncState;
                socketClient = listener.EndAccept(ar);
                // Signal the main thread to continue.
                this.AllDone.Set();
            }
            catch (Exception ex)
            {
                Logger.LogError($"AcceptCalback Error: {ex.Message}, {ex.StackTrace}");
                this.AllDone.Set();
                return;
            }
            try
            {
                ClientRequestHandler clientRequestHandler = null;
                // Create the state object.
                lock (this.lockObj)
                {
                    clientRequestHandler = new ClientRequestHandler(this.clientNo, socketClient, this, StateFactory.GetInstance(this.InitState));
                    this.dicHandler[this.clientNo++] = clientRequestHandler;
                    Task.Run(() => clientRequestHandler.DoCommunicate());
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"AcceptCalback Error: {ex.Message}, {ex.StackTrace}");
            }
        }

        private bool IsIpAddr(string strAddr)
        {
            string validIpAddressRegex =
                @"^(([0-9]|[1-9][0-9]|1[0-9]{2}|2[0-4][0-9]|25[0-5])\.){3}([0-9]|[1-9][0-9]|1[0-9]{2}|2[0-4][0-9]|25[0-5])$";
            return (Regex.IsMatch(strAddr, validIpAddressRegex));
        }

        private bool IsHostName(string hostName)
        {
            string validHostNameRegex = @"^(([a-zA-Z]|[a-zA-Z][a-zA-Z0-9\-]*[a-zA-Z0-9])\.)*([A-Za-z]|[A-Za-z][A-Za-z0-9\-]*[A-Za-z0-9])$";
            return (Regex.IsMatch(hostName, validHostNameRegex));
        }

        /// <summary>
        ///   Remove a handler by it's client no.
        /// </summary>
        /// <param name="clientNo"></param>
        public virtual void RemoveClient(int clientNo)
        {
            lock (this.lockObj)
            {
                if (this.dicHandler.ContainsKey(clientNo))
                {
                    try
                    {
                        Logger.LogDebug($">> RemoveClient:[{clientNo}]...");
                        (this.dicHandler[clientNo]).Cancel();
                        this.dicHandler.TryRemove(clientNo, out IClientRequestHandler cr);
                    }
                    catch (Exception ex)
                    {
                        Logger.LogError($"RemoveClient Error: {ex.Message}, {ex.StackTrace}");
                    }
                }
            }
        }
        
        private void RemoveAllClients()
        {
            int[] clientList = new int[this.dicHandler.Count];
            this.dicHandler.Keys.CopyTo(clientList, 0);
            foreach (int clientNo in clientList)
            {
                try
                {
                    this.RemoveClient(clientNo);
                }
                catch (Exception ex)
                {
                    Logger.LogError(ex.StackTrace);
                }
            }
        }

        #region Dispose method
        protected bool disposed = false;
        // Implement IDisposable.
        // Do not make this method virtual.
        // A derived class should not be able to override this method.
        public void Dispose()
        {
            this.Dispose(true);
            GC.SuppressFinalize(this);
        }

        // Dispose(bool disposing) executes in two distinct scenarios.
        // If disposing equals true, the method has been called directly
        // or indirectly by a user's code. Managed and unmanaged resources
        // can be disposed.
        // If disposing equals false, the method has been called by the
        // runtime from inside the finalizer and you should not reference
        // other objects. Only unmanaged resources can be disposed.
        protected virtual void Dispose(bool disposing)
        {
            // Check to see if Dispose has already been called.
            if (!this.disposed)
            {
                // If disposing equals true, dispose all managed and unmanaged resources.
                if (disposing)
                {
                    if (this.listener != null)
                    {
                        this.listener.Close();
                        this.listener.Dispose();
                        this.listener = null;
                    }
                }
                // Call the appropriate methods to clean up unmanaged resources here.
                // If disposing is false, only the following code is executed.

                // Note disposing has been done.
                this.disposed = true;
            }
        }

        // Use C# destructor syntax for finalization code.
        // This destructor will run only if the Dispose method does not get called.
        // It gives your base class the opportunity to finalize.
        // Do not provide destructors in types derived from this class.
        ~AbsServer()
        {
            this.Dispose(false);
        }
        #endregion
    }
}
