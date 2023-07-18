
namespace Fcl.Sockets.Client
{
    public interface IClient<T> : IDisposable
    {
        void Open();
        T Receive();
        void Send(T writeDo);
        void Close();
    }

    public interface IClient : IDisposable
    {
        void Open();
        byte[] Receive();
        void Send(byte[] writeBytes);
        void Close();
        bool IsKeepAlive();
    }
}
