#if UNITY_2023_2_OR_NEWER
using Unity.Multiplayer.Tools.NetworkProfiler.Editor.Analytics;
#endif
using Unity.Profiling.Editor;
using UnityEditor;
using UnityEngine.UIElements;

namespace Unity.Multiplayer.Tools.NetworkProfiler.Editor
{
    class NetworkDetailsViewController : ProfilerModuleViewController
    {
        readonly string m_TabName;

        readonly INetworkProfilerDataProvider m_NetworkProfilerDataProvider;
        readonly NetworkProfilerDetailsView m_NetworkProfilerDetailsView;

        public NetworkDetailsViewController(ProfilerWindow profilerWindow, string tabName)
            : base(profilerWindow)
        {
#if UNITY_2023_2_OR_NEWER
            EditorAnalytics.SendAnalytic(new ProfilerSelectedAnalytic(tabName));
#endif
            m_TabName = tabName;

            m_NetworkProfilerDataProvider = new NetworkProfilerDataProvider();
            m_NetworkProfilerDetailsView = new NetworkProfilerDetailsView();

            ProfilerWindow.SelectedFrameIndexChanged += OnSelectedFrameChanged;
        }

        protected override void Dispose(bool disposing)
        {
            if (!disposing)
            {
                return;
            }

            ProfilerWindow.SelectedFrameIndexChanged -= OnSelectedFrameChanged;

            base.Dispose(true);
        }

        protected override VisualElement CreateView()
        {
            m_NetworkProfilerDetailsView.ShowTab(m_TabName);
            m_NetworkProfilerDetailsView.PopulateView(m_NetworkProfilerDataProvider.GetDataForFrame(ProfilerWindow.selectedFrameIndex));

            return m_NetworkProfilerDetailsView;
        }

        void OnSelectedFrameChanged(long selectedFrameIndex)
        {
            // Prevent an error when starting the profiler when the game is not running and no frame is selected
            if(selectedFrameIndex != -1)
                m_NetworkProfilerDetailsView?.PopulateView(m_NetworkProfilerDataProvider.GetDataForFrame(ProfilerWindow.selectedFrameIndex));
        }
    }
}
