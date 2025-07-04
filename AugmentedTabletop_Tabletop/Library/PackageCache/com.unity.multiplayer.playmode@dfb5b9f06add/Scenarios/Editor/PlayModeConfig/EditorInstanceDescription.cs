using System;
using Unity.Multiplayer.PlayMode.Editor.Bridge;
using UnityEditor;
using UnityEngine;

namespace Unity.Multiplayer.PlayMode.Scenarios.Editor
{
    [Serializable]
    class EditorInstanceDescription : InstanceDescription
    {
#if UNITY_USE_MULTIPLAYER_ROLES
        [SerializeField] internal MultiplayerRoleFlags m_Role;
#endif
        [SerializeField] string m_PlayerTag;

        [Tooltip("Starting scene of all editor instances, including main editor and all editor instances. If no scene is selected, " +
                 "editor instances will use the current scene as the initial scene.")]
        [SerializeField] [HideInInspector] private SceneAsset m_InitialScene;

        internal const string k_EditorInstanceTypeName = "EditorInstance";

#if UNITY_USE_MULTIPLAYER_ROLES
        public MultiplayerRoleFlags RoleMask
        {
            get => m_Role;
            internal set => m_Role = value;
        }
#endif

        public string PlayerTag
        {
            get => m_PlayerTag;
            set => m_PlayerTag = value;
        }

        public SceneAsset InitialScene
        {
            get => m_InitialScene;
            set => m_InitialScene = value;
        }

        [NonSerialized] public int PlayerInstanceIndex;

        public EditorInstanceDescription()
        {
            Name = "Editor";
#if UNITY_USE_MULTIPLAYER_ROLES
            m_Role = MultiplayerRoleFlags.Client;
#endif
            PlayerInstanceIndex = 0;
            m_PlayerTag = "";
        }

        internal override string InstanceTypeName => k_EditorInstanceTypeName;

        internal override string BuildTargetType => InternalUtilities.GetBuildTargetType(EditorUserBuildSettings.activeBuildTarget);
#if UNITY_USE_MULTIPLAYER_ROLES
        internal override string MultiplayerRole => m_Role.ToString();
#endif
    }
}
