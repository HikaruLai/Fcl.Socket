using System.Threading;
using Microsoft.Extensions.Logging;

namespace Fcl.Sockets.Server
{
    public class SocketServer : AbsServer
    {
        public SocketServer(ILoggerFactory loggerFactory, int port, string initState, string hostName = "0.0.0.0", int backlog = 100)
        {
            this.HostName = hostName;
            this.Port = port;
            this.BackLog = backlog;
            this.InitState = initState;
            // AllDone init state is unsingaled
            this.AllDone = new ManualResetEvent(false);
            this.Logger = loggerFactory.CreateLogger<SocketServer>();
        }
    }
}
