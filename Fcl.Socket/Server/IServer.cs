
namespace Fcl.Sockets.Server
{
    public interface IServer : IDisposable
    {
        void Start();
        void Stop();
        void RemoveClient(int clientNo);
    }
}
