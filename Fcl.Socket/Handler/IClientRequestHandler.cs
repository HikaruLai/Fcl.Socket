
namespace Fcl.Sockets.Handler
{
    public interface IClientRequestHandler
    {
        /// <summary>
        ///  start communications between server & client
        /// </summary>
        void DoCommunicate();

        /// <summary>
        /// stop communications between server & client
        /// </summary>
        void Cancel();
    }
}
