using Fcl.Sockets.Handler;

namespace Fcl.Sockets.State
{
    /// <summary>
    /// state interface to implement
    /// </summary>
    public interface IState : IDisposable
    {
        /// <summary>
        /// Handle state with context clientRequestHandler
        /// </summary>
        /// <param name="clientRequestHandler">context of current socket client</param>
        void Handle(AbsClientRequestHandler clientRequestHandler);
    }
}
