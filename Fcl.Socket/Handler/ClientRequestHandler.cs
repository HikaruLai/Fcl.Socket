using System;
using System.Collections.Generic;
using System.Net.Sockets;
using Microsoft.Extensions.Logging;
using Fcl.Sockets.Server;
using Fcl.Sockets.State;

namespace Fcl.Sockets.Handler
{
    public class ClientRequestHandler : AbsClientRequestHandler
    {
        private readonly ILoggerFactory logfactory;

        public ClientRequestHandler(int clientNo, Socket socketClient, IServer socketServer, IState state)
        {
            this.ClientNo = clientNo;
            this.SocketClient = socketClient;
            this.SocketServer = socketServer;
            this.ServiceState = state;
            logfactory = new LoggerFactory();
            this.Logger = logfactory.CreateLogger<ClientRequestHandler>();
        }
    }
}
