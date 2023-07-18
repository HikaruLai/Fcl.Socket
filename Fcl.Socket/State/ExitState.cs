using System;
using Microsoft.Extensions.Logging;
using Fcl.Sockets.Handler;

namespace Fcl.Sockets.State
{
    public class ExitState : IState
    {
        private ILogger logger { get; set; }

        public ExitState(ILoggerFactory loggerFactory)
        {
            this.logger = loggerFactory.CreateLogger<ExitState>();
        }

        public void Handle(AbsClientRequestHandler clientRequestHandler)
        {
            logger.LogDebug($"Exit State...");
            clientRequestHandler.KeepService = false;
            clientRequestHandler.SocketServer.RemoveClient(clientRequestHandler.ClientNo);
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
                    // do dispose detail...
                }

                this.disposed = true;
            }
        }
        #endregion
    }
}
