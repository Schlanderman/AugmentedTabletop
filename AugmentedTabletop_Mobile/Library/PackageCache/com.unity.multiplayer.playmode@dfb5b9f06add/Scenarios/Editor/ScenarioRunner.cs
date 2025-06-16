using System.Diagnostics.CodeAnalysis;
using Unity.Multiplayer.PlayMode.Scenarios.Editor.Api;
using Unity.Multiplayer.PlayMode.Scenarios.Editor.GraphsFoundation;
using UnityEditor;
using System;
using System.Threading;
using Unity.Multiplayer.PlayMode.Common.Editor;

namespace Unity.Multiplayer.PlayMode.Scenarios.Editor
{
    internal class ScenarioRunner : ScriptableSingleton<ScenarioRunner>
    {
        private static readonly ScenarioStatus k_DefaultScenarioState = new ScenarioStatus(ExecutionStage.None, ExecutionState.Invalid);

        [InitializeOnLoadMethod]
        private static void OnDomainReload()
        {
            if (instance.m_Scenario == null)
                return;

            // Notify all Scenario mode instances running in a Scenario
            if (instance.m_Scenario.Status.State == ScenarioState.Running)
                instance.RunOrResume();

            // Notify all Manual mode instances that are free running.
            instance.m_Scenario.ResumeFreeRunInstances();
        }

        private Scenario m_Scenario;
        private CancellationTokenSource m_CancellationTokenSource;
        private bool m_IsRunning;

        internal Scenario ActiveScenario => m_Scenario;
        internal bool IsRunning => m_IsRunning;

        internal static event Action<ScenarioStatus> StatusChanged;

        public static void LoadScenario([NotNull]Scenario scenario)
        {
            // If clearing the scenario, ensure all Free Run instances with this scenario are terminated
            if (scenario == null && instance.m_Scenario != null)
                instance.m_Scenario.TerminateAllFreeRunningInstances();

            instance.m_Scenario = scenario;
        }

        public static void StartScenario()
        {
            if (instance.m_Scenario != null && instance.m_Scenario.Status.State == ScenarioState.Running)
            {
                instance.m_CancellationTokenSource?.Cancel();
                instance.m_Scenario.StatusRefreshed -= OnStatusChanged;
                instance.m_Scenario.Reset();
                instance.m_IsRunning = false;

                throw new InvalidOperationException("There is already a scenario running. Stopping it before starting a new one.");
            }

            instance.m_Scenario.Reset();
            instance.RunOrResume();
        }

        private void RunOrResume()
        {
            instance.m_IsRunning = true;
            m_CancellationTokenSource = new CancellationTokenSource();
            instance.m_Scenario.StatusRefreshed += OnStatusChanged;
            instance.m_Scenario.RunOrResumeAsync(m_CancellationTokenSource.Token).Forget();
        }

        private static void OnStatusChanged(ScenarioStatus status)
        {
            StatusChanged?.Invoke(status);
        }

        public static ScenarioStatus GetScenarioStatus()
        {
             return instance.m_IsRunning ? instance.m_Scenario.Status : k_DefaultScenarioState;
        }

        public static void StopScenario()
        {
            AssetMonitor.Reset();

            if (instance.m_Scenario != null)
            {
                instance.m_CancellationTokenSource?.Cancel();

                // We cannot guarantee that the scenario will be stopped as it depends on the nodes implementation.
                // We assume they consume the cancellation token properly.
                instance.m_CancellationTokenSource = null;
                instance.m_IsRunning = false;
            }
            else
            {
                UnityEngine.Debug.LogWarning("Trying to stop an scenario but there is currently no scenario running.");
            }
        }
    }
}
