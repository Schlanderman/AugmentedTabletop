using System.Threading.Tasks;
using Unity.Services.Core.Scheduler.Internal;
using Unity.Services.Multiplay;

namespace Unity.Services.Multiplayer
{
    class MultiplaySessionManager
    {
        IMultiplaySessionManagerInternal m_MultiplaySessionManager;

        readonly ISessionManager m_SessionManager;
        readonly IMultiplayService m_MultiplayService;
        readonly IActionScheduler m_ActionScheduler;

        public MultiplaySessionManager(
            ISessionManager sessionManager,
            IMultiplayService multiplayService,
            IActionScheduler actionScheduler)
        {
            m_SessionManager = sessionManager;
            m_MultiplayService = multiplayService;
            m_ActionScheduler = actionScheduler;
        }

        /// <summary>
        /// StartMultiplaySessionManagerAsync sets up a subscription to the Multiplay service to listen for server events,
        /// primarily to allow us to create the server session on the Allocated event.
        /// </summary>
        /// <returns></returns>
        public async Task<IMultiplaySessionManager> StartMultiplaySessionManagerAsync(MultiplaySessionManagerOptions options)
        {
            IMultiplayHandler gameServerHostingHandler = new MultiplayHandler(m_MultiplayService, m_ActionScheduler);
            m_MultiplaySessionManager = new MultiplaySessionHandler(m_MultiplayService, m_SessionManager, gameServerHostingHandler);
            return await m_MultiplaySessionManager.StartMultiplaySessionHandlerAsync(options);
        }
    }
}
