using LightInject;

namespace Fcl.Sockets.State
{
    public class StateFactory
    {
        private static IServiceContainer Ctx;

        static StateFactory()
        {
            // ctx = Framework.Container;
        }

        public static void Init(IServiceContainer ctx)
        {
            Ctx = ctx;
        }

        public static IState GetInstance(string stateName)
        {
            return Ctx.GetInstance<IState>(stateName);
        }
    }
}
