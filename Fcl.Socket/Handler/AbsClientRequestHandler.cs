using System;
using System.IO;
using System.Net.Sockets;
using Microsoft.Extensions.Logging;
using Fcl.Sockets.Server;
using Fcl.Sockets.State;

namespace Fcl.Sockets.Handler
{
    public abstract class AbsClientRequestHandler : IClientRequestHandler
    {
        protected ILogger Logger { get; set; }

        /// <summary>
        /// socket client ID numbers
        /// </summary>
        public int ClientNo { get; set; }

        /// <summary>
        /// socket client
        /// </summary>
        public Socket SocketClient { get; set; }

        /// <summary>
        /// socket server which made this handler
        /// </summary>
        public IServer SocketServer { get; set; }

        /// <summary>
        /// current active state
        /// </summary>
        public IState ServiceState { get; set; }

        /// <summary>
        /// flag to run state
        /// </summary>
        public bool KeepService { get; set; }

        /// <summary>
        /// message byte[] from client
        /// </summary>
        public byte[] MsgBytes { get; set; }

        public virtual void DoCommunicate()
        {
            this.KeepService = true;
            // go-alive
            while (this.KeepService)
            {
                this.ServiceState.Handle(this);
            }
        }

        public virtual void Cancel()
        {
            if (null != this.SocketClient)
            {
                try
                {
                    this.SocketClient.Shutdown(SocketShutdown.Both);
                    this.SocketClient.Dispose();
                    this.SocketClient = null;
                }
                catch (Exception ex)
                {
                    Logger.LogError($"Cancel Error: {ex.Message}, {ex.StackTrace}");
                }
            }
        }

        /// <summary>
        /// get message from socket client
        /// </summary>
        public virtual void Receive()
        {
            byte[] buffer = new byte[0x1000];
            MemoryStream ms = new MemoryStream();
            while (true)
            {
                int bytesRead = this.SocketClient.Receive(buffer, 0, buffer.Length, SocketFlags.None);
                if (bytesRead > 0)
                {
                    ms.Write(buffer, 0, bytesRead);
                }
                if (bytesRead == 0 || bytesRead < buffer.Length)
                {
                    this.MsgBytes = ms.ToArray();
                    return;
                }
            }
        }

        /// <summary>
        /// send message to socket client
        /// </summary>
        /// <param name="writeBytes"></param>
        public virtual void Send(byte[] writeBytes)
        {
            if (this.SocketClient.Connected)
            {
                this.SocketClient.Send(writeBytes, SocketFlags.None);
            }
        }
    }
}
