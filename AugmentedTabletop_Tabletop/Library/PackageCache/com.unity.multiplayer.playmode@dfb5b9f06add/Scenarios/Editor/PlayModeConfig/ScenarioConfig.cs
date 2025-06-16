using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine.Assertions;
using Unity.Multiplayer.PlayMode.Scenarios.Editor.GraphsFoundation;
using Unity.Multiplayer.Playmode.Workflow.Editor;
using Unity.Multiplayer.PlayMode.Editor.Bridge;
using Unity.Multiplayer.PlayMode.Configurations.Editor;
using UnityEngine;
using UnityEngine.UIElements;
using Unity.Multiplayer.PlayMode.Configurations.Editor.Gui;
using UnityEditor.SceneManagement;
using UnityEngine.Serialization;
using Unity.Multiplayer.PlayMode.Analytics.Editor;
using Unity.Multiplayer.PlayMode.Scenarios.Editor.Api;
using Unity.Multiplayer.Playmode.VirtualProjects.Editor;
using UnityEditor;

namespace Unity.Multiplayer.PlayMode.Scenarios.Editor
{
    [CreatePlayModeConfigurationMenu("Scenario Configuration", "NewPlayModeScenario")]
    class ScenarioConfig : PlayModeConfig, ISerializationCallbackReceiver
    {
        public static readonly ReadOnlyCollection<string> k_RequiredPackagesForRemoteInstances = new List<string>()
        {
            "com.unity.services.multiplayer@1.0.0",
        }.AsReadOnly();

        [SerializeField] private bool m_EnableEditors = true;
        [SerializeField] private MainEditorInstanceDescription m_MainEditorInstance = new();

        [Tooltip("Initial Editor Instances when entering playmode. Editor Instances will only have limited authoring capabilities.")]
        [SerializeField] private List<VirtualEditorInstanceDescription> m_EditorInstances = new();

        [Tooltip("Local Instances are builds that will run on the same machine as the editor.")]
        [SerializeField] private List<LocalInstanceDescription> m_LocalInstances = new();

        [Tooltip("Remote Instances are builds that will get deployed to UGS and will run there.")]
        [SerializeField] private List<RemoteInstanceDescription> m_RemoteInstances = new();

        [SerializeField] private bool m_OverridePort;
        [SerializeField] private ushort m_Port;

        private Scenario m_Scenario;

        public Scenario Scenario => m_Scenario;
        public MainEditorInstanceDescription EditorInstance => m_MainEditorInstance;
        public ReadOnlyCollection<VirtualEditorInstanceDescription> VirtualEditorInstances => m_EditorInstances.AsReadOnly();
        public ReadOnlyCollection<LocalInstanceDescription> LocalInstances => m_LocalInstances.AsReadOnly();
        public ReadOnlyCollection<RemoteInstanceDescription> RemoteInstances => m_RemoteInstances.AsReadOnly();

        public override bool SupportsPauseAndStep => true;

        // The following section is for upgrading from 1.0.0-pre.2 to 1.0.0-pre.3.
        // Because m_MainEditorInstance was serialized as reference we need to manually copy the old values to the new instance.
        [SerializeReference, FormerlySerializedAs("m_MainEditorInstance")] private MainEditorInstanceDescription m_MainEditorInstanceObsolete;

        public void OnBeforeSerialize() { }

        public void OnAfterDeserialize()
        {
            if (m_MainEditorInstanceObsolete != null)
            {
                var serialized = JsonUtility.ToJson(m_MainEditorInstanceObsolete);
                JsonUtility.FromJsonOverwrite(serialized, m_MainEditorInstance);
                m_MainEditorInstanceObsolete = null;
            }
        }
        // End upgrade section.

        public List<InstanceDescription> GetAllInstances()
        {
            var instances = new List<InstanceDescription>();

            if (m_EnableEditors)
            {
                Assert.IsNotNull(m_MainEditorInstance);
                m_MainEditorInstance.Name = MultiplayerPlaymode.PlayerOne.Name;
                instances.Add(m_MainEditorInstance);
                for (var i = 0; i < m_EditorInstances.Count; i++)
                {
                    var playerIndex = i + 1;// Main editor is PlayerInstanceIndex 0
                    m_EditorInstances[i].PlayerInstanceIndex = playerIndex;
                    m_EditorInstances[i].Name = MultiplayerPlaymode.Players[playerIndex].Name;
                    instances.Add(m_EditorInstances[i]);
                }
            }

            instances.AddRange(m_LocalInstances);
            instances.AddRange(m_RemoteInstances);
            return instances;
        }

        internal InstanceDescription GetInstanceDescriptionByName(string instanceName)
        {
            var instances = GetAllInstances();
            foreach (var instance in instances)
            {
                if (instance.Name.Equals(instanceName))
                    return instance;
            }

            return null;
        }

        [InitializeOnLoadMethod]
        private static void RegisterAnalyticsOnPlayModeEnteredEvents()
        {
            EditorApplication.playModeStateChanged += UpdateAnalyticsOnPlayModeEnteredFromMppmEvent;
            EditorApplication.playModeStateChanged += SendEnterPlayModeOnTagsAppliedEvent;
        }

        private static void SendEnterPlayModeOnTagsAppliedEvent(PlayModeStateChange state)
        {
            if (VirtualProjectsEditor.IsClone) return;

            var players = MultiplayerPlaymode.Players;
            if (state != PlayModeStateChange.EnteredPlayMode) return;

            foreach (var player in players)
            {
                if (player.PlayerState == PlayerState.Launched && player.Tags.Length > 0)
                {
                    AnalyticsOnTagsAppliedEvent.Send(new OnTagsAppliedData()
                    {
                        PlayerName = player.Name,
                        TagsCount = player.Tags.Length,
                        TagNames = player.Tags.ToArray(),
                        IsFromScenario = PlayModeManager.instance.ActivePlayModeConfig is ScenarioConfig
                    });
                }
            }
        }

        private static void UpdateAnalyticsOnPlayModeEnteredFromMppmEvent(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.EnteredPlayMode)
            {
                if (PlayModeManager.instance.ActivePlayModeConfig is ScenarioConfig || VirtualProjectsEditor.IsClone)
                {
                    return;
                }

                List<UnityPlayer> launchedPlayers = new List<UnityPlayer>();
                var players = MultiplayerPlaymode.Players;
                if (MultiplayerPlaymode.Players != null)
                {
                    foreach (var player in players)
                    {
                        //filter all launched virtual players
                        if (player.PlayerState == PlayerState.Launched && player.m_TimeSinceStartingLaunch != DateTime.MinValue)
                        {
                            launchedPlayers.Add(player);
                        }
                    }
                }

                var vpCount = launchedPlayers.Count;
                if (vpCount > 0)
                {
                    AnalyticsEnterPlayModeFromMppmEvent.Send(new EnterPlayModeFromMppmData()
                    {
                        VirtualPlayerCount = vpCount,
                        CloneWindowErrorCount = GetTotalErrorCountForVirtualPlayers(launchedPlayers)
                    });
                }
            }
        }

        private static int GetTotalErrorCountForVirtualPlayers(List<UnityPlayer> players)
        {
            int errorCount = 0;
            foreach (var player in players)
            {
                var logs = MultiplayerPlaymodeLogUtility.PlayerLogs(player.PlayerIdentifier).LogCounts;
                var errorCountForPlayer = logs.Errors;
                errorCount += errorCountForPlayer;

            }
            return errorCount;
        }

        public override void OnConfigSelected()
        {
            // When a config is selected, initialize and load it into Scenario Runner.
            CreateAndLoadScenario();
        }

        public override void OnConfigDeselected()
        {
            // Clear any loaded scenario from the scenario runner
            m_Scenario = null;
            ScenarioRunner.LoadScenario(null);
        }

        private void CreateAndLoadScenario()
        {
            if (!IsConfigurationValid(out string _))
                return;

            // Create the Scenario and load it into the runner
            m_Scenario = CreateScenario();
            ScenarioRunner.LoadScenario(m_Scenario);
        }

        protected virtual Scenario CreateScenario()
        {
            return ScenarioFactory.CreateScenario(name, GetAllInstances());
        }

        private void OnValidate()
        {
            // Avoid re-creating the scenario if the scenario is running
            if (ScenarioRunner.GetScenarioStatus().State == ScenarioState.Running)
                return;

            // Scenario Validation triggered by a domain reload are not considered.
            if (InternalUtilities.IsDomainReloadRequested() ||
                EditorApplication.isCompiling ||
                EditorApplication.isUpdating)
            {
                return;
            }

            // If this is the selected config, re-load it into ScenarioRunner
            if (PlayModeManager.instance.ActivePlayModeConfig is ScenarioConfig config && config == this)
                CreateAndLoadScenario();
        }

        public override Task ExecuteStartAsync(CancellationToken cancellationToken)
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return Task.FromCanceled(new CancellationToken(true));

            // Quick Sanity check.
            if (m_Scenario == null)
                Debug.LogError("Attempted to start Scenario with none set.");

            ScenarioRunner.StartScenario();

            LaunchingScenarioWindow.OnScenarioStarted(this);

            return Task.CompletedTask;
        }

        public override void ExecuteStop()
        {
            ScenarioRunner.StopScenario();
            var state = ScenarioRunner.GetScenarioStatus();
            if (state.State == Api.ScenarioState.Failed)
            {
                if (state.Errors != null)
                {
                    var errorMessages = state.Errors.Select(e => e.Message).ToList();
                    var errors = string.Join("\n\t -> ", errorMessages);
                    Debug.LogError($"Scenario failed with error:\n\t -> {errors}");
                }
                else
                    Debug.LogError($"Scenario failed with unknown error.");
            }
        }

        public override VisualElement CreateTopbarUI() => new MultiplayerPlayModeStatusButton(this);
        public override Texture2D Icon => Icons.GetImage(Icons.ImageName.PlayModeScenario);

        public override string Description
        {
            get
            {
                var summary = "\n1 Editor instance\n";

                var localInstanceCount = m_LocalInstances.Count;
                if (localInstanceCount > 0)
                    summary += $"{localInstanceCount} Local instance{(localInstanceCount > 1 ? "s" : "")}\n";
                var remoteInstanceCount = m_RemoteInstances.Count;
                if (remoteInstanceCount > 0)
                    summary += $"{remoteInstanceCount} Remote instance{(remoteInstanceCount > 1 ? "s" : "")}\n";

                return (base.Description + summary).Trim('\n');
            }
        }

        public override bool IsConfigurationValid(out string reasonForInvalidConfiguration)
        {
            reasonForInvalidConfiguration = "";

            // Check if instance's tags are valid and clear them if not.
            var allInstances = GetAllInstances().Distinct();
            foreach (var instance in allInstances)
            {
                if (instance is EditorInstanceDescription editorInstance)
                {
                    var currTag = editorInstance.PlayerTag;
                    if (!string.IsNullOrEmpty(currTag) && !MultiplayerPlaymode.PlayerTags.Contains(currTag))
                        editorInstance.PlayerTag = "";
                }
            }

            // Check if instance names are unique
            List<string> takenNames = new List<string>();
            bool containsTakenName = false;
            allInstances = GetAllInstances().Distinct();

            if (allInstances.Count() == 0)
            {
                reasonForInvalidConfiguration = "Scenario must have at least one instance.";
                return false;
            }

            foreach (var instance in allInstances)
            {
                if (takenNames.Contains(instance.Name))
                {
                    reasonForInvalidConfiguration = "Instance names must be unique.";
                    containsTakenName = true;
                    break;
                }
                takenNames.Add(instance.Name);
            }

            // Check if local mobile device instances have a device selected that is unique
            List<LocalInstanceDescription> localMobileDevices = new List<LocalInstanceDescription>();
            foreach (var instance in allInstances)
            {
                if (instance is LocalInstanceDescription localInstance)
                {
                    if (localInstance.BuildProfile != null)
                    {
                        if (InternalUtilities.IsAndroidBuildTarget(localInstance.BuildProfile))
                            localMobileDevices.Add(localInstance);
                    }
                }
            }

            var localMobileDevicesSelected = localMobileDevices.All(instance => instance != null && instance.BuildProfile != null && !string.IsNullOrEmpty(instance.AdvancedConfiguration.DeviceID));
            if (!localMobileDevicesSelected)
                reasonForInvalidConfiguration += "\nLocal mobile device instance(s) must have a device selected.";

            List<string> takenIDs = new List<string>();
            bool containsTakenDeviceID = false;
            foreach (var instance in localMobileDevices)
            {
                if (takenIDs.Contains(instance.AdvancedConfiguration.DeviceID))
                {
                    reasonForInvalidConfiguration = "Device must be associated with only a single instance.";
                    containsTakenDeviceID = true;
                    break;
                }
                takenIDs.Add(instance.AdvancedConfiguration.DeviceID);
            }

            // Check if local build targets are supported for building
            var localBuildTargetsAreSupported = m_LocalInstances.Count == 0 | m_LocalInstances.All(instance => instance != null && instance.BuildProfile != null && InternalUtilities.IsBuildProfileSupported(instance.BuildProfile));
            if (!localBuildTargetsAreSupported)
                reasonForInvalidConfiguration += "\nLocal instance(s) have incorrect build target";

            // Check if local build targets are supported to run.
            // This is necessary because if for example Linux Build Support is available but we are running on Windows, we can build but we cannot start the instance.
            var localBuildTargetsCanRunOnPlatform = m_LocalInstances.Count == 0 | m_LocalInstances.All(instance => instance != null && instance.BuildProfile != null && InternalUtilities.BuildProfileCanRunOnCurrentPlatform(instance.BuildProfile));
            if (!localBuildTargetsCanRunOnPlatform)
                reasonForInvalidConfiguration += "\nLocal instance(s) buildtarget cannot run on current platform.";

            // Check if we have the correct packages installed for running a remote server.
            if (!PackagesForRemoteDeployInstalled(out var missingPacks) && m_RemoteInstances.Count > 0)
                reasonForInvalidConfiguration += "\nPackages are missing:\n" + string.Join("\n", missingPacks);

            // Check if remote build targets are supported to be build.
            var remoteBuildTargetsCorrect = m_RemoteInstances.Count == 0 | m_RemoteInstances.All(instance =>
            {
                return instance != null && instance.BuildProfile != null &&
                       InternalUtilities.IsBuildProfileSupported(instance.BuildProfile) &&
                       !InternalUtilities.IsAndroidBuildTarget(instance.BuildProfile);
            });
            if (!remoteBuildTargetsCorrect)
                reasonForInvalidConfiguration += "\nRemote instance(s) have incorrect build target.";

            // Check if we have more than one server role.
            var configHasMoreServerInstances = ConfigurationHasMaxOneServer();
            if (!configHasMoreServerInstances)
                reasonForInvalidConfiguration += "\nOnly one Server Role is allowed per Configuration.";

            reasonForInvalidConfiguration = reasonForInvalidConfiguration.Trim('\n');
            return localBuildTargetsAreSupported && remoteBuildTargetsCorrect && localBuildTargetsCanRunOnPlatform &&
                   configHasMoreServerInstances && localMobileDevicesSelected && !containsTakenName &&
                   !containsTakenDeviceID;
        }

        bool ConfigurationHasMaxOneServer()
        {
#if UNITY_USE_MULTIPLAYER_ROLES
            var allInstances = GetAllInstances();
            var serverCount = allInstances.Count(instance => ScenarioFactory.GetRoleForInstance(instance).HasFlag(MultiplayerRoleFlags.Server));
            return serverCount < 2;
#else
            return true;
#endif
        }

        public static bool PackagesForRemoteDeployInstalled(out List<string> missingPacks)
        {
            missingPacks = new List<string>();

            foreach (var packIds in k_RequiredPackagesForRemoteInstances)
            {
                var nameParts = packIds.Split('@');
                var packageName = nameParts[0];
                var requiredVersion = nameParts[1];
                var packInfo = UnityEditor.PackageManager.PackageInfo.FindForPackageName(packageName);
                var packageIsInstalled = false;
                if (packInfo != null)
                {
                    var installedVersion = packInfo.version;
                    packageIsInstalled = IsPackageVersionCompatible(installedVersion, requiredVersion);
                }

                var packInstalled = packInfo != null && packageIsInstalled;
                if (!packInstalled)
                    missingPacks.Add(packIds);
            }
            return missingPacks.Count == 0;
        }

        private static bool IsPackageVersionCompatible(string installedVersion, string requiredVersion)
        {
            SplitPackageVersion(installedVersion, out var installedMajor, out var installedMinor, out var installedPatch, out var installedPre, out var isInstalledPre);
            SplitPackageVersion(requiredVersion, out var requiredMajor, out var requiredMinor, out var requiredPatch, out var requiredPre, out var isRequiredPre);

            if (installedMajor < requiredMajor)
                return false;

            if (installedMajor > requiredMajor)
                return true;

            if (installedMinor < requiredMinor)
                return false;

            if (installedMinor > requiredMinor)
                return true;

            if (installedPatch < requiredPatch)
                return false;

            if (installedPatch > requiredPatch)
                return true;

            if (isInstalledPre && !isRequiredPre)
                return false;

            if (isInstalledPre && isRequiredPre && installedPre < requiredPre)
                return false;

            return true;
        }

        private static void SplitPackageVersion(string version, out int major, out int minor, out int patch, out int pre, out bool isPre)
        {
            var regex = new System.Text.RegularExpressions.Regex(@"(\d+)\.(\d+)\.(\d+)(?:-pre\.(\d+))?");
            var match = regex.Match(version);

            if (!match.Success)
                throw new ArgumentException($"Invalid version string: {version}");

            major = int.Parse(match.Groups[1].Value);
            minor = int.Parse(match.Groups[2].Value);
            patch = int.Parse(match.Groups[3].Value);

            isPre = match.Groups[4].Success;
            pre = isPre ? int.Parse(match.Groups[4].Value) : -1;
        }
    }
}
