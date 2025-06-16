using System;
using System.Collections.Generic;
using Unity.Multiplayer.PlayMode.Common.Runtime;

namespace Unity.Multiplayer.Playmode.Workflow.Editor
{
    [Flags]
    enum LayoutFlags
    {
        None = 0,

        SceneHierarchyWindow = 1 << 0,
        GameView = 1 << 1,
        SceneView = 1 << 2,
        ConsoleWindow = 1 << 3,
        InspectorWindow = 1 << 4,
        EntitiesPlayModeToolsWindow = 1 << 5,
        EntitiesHierarchyWindow = 1 << 6,
    }

    static class LayoutFlagsUtil
    {
        public static LayoutFlags[] AllAsArray { get; } = (LayoutFlags[])Enum.GetValues(typeof(LayoutFlags));

        public static LayoutFlags All
        {
            get
            {
                LayoutFlags flags = LayoutFlags.None;
                var values = Enum.GetValues(typeof(LayoutFlags));
                foreach (LayoutFlags value in values)
                {
                    flags = flags | value;
                }

                return flags;
            }
        }

        // Mapping of LayoutFlags to their corresponding Qualified View Class names.
        private static readonly Dictionary<LayoutFlags, string> k_SupportedFlagsClassName =
            new Dictionary<LayoutFlags, string>()
            {
                { LayoutFlags.InspectorWindow, "UnityEditor.InspectorWindow" },
                { LayoutFlags.GameView, "UnityEditor.GameView" },
                { LayoutFlags.SceneHierarchyWindow, "UnityEditor.SceneHierarchyWindow" },
                { LayoutFlags.ConsoleWindow, "UnityEditor.ConsoleWindow" },
                { LayoutFlags.SceneView, "UnityEditor.SceneView" },
                { LayoutFlags.EntitiesPlayModeToolsWindow, "Unity.NetCode.Editor.MultiplayerPlayModeWindow" },
                { LayoutFlags.EntitiesHierarchyWindow, "Unity.Entities.Editor.HierarchyWindow"}
            };

        private static readonly HashSet<string> k_SupportedAuxiliaryViews =
            new HashSet<string>()
            {
                "Unity.Multiplayer.Playmode.Workflow.Editor.TopView",
                "UnityEditor.PopupWindow",
                "UnityEditor.FrameDebuggerWindow",
                "UnityEditor.AnnotationWindow",
                "UnityEditor.LayerVisibilityWindow",
                "UnityEditor.Snap.SnapSettingsWindow",
                "UnityEditor.Snap.GridSettingsWindow",
                "UnityEditor.Search.SearchPickerWindow",
                "UnityEditor.PropertyEditor",
                "UnityEditor.DeviceSimulation.SimulatorWindow"
            };

        // Determines if a given LayoutFlag's window panel view is supported in this unity project.
        // Ex: Netcode Entites packages may have not been included.
        public static bool IsLayoutSupported(LayoutFlags layout)
        {
            if (!k_SupportedFlagsClassName.TryGetValue(layout, out string qualifiedName))
            {
                MppmLog.Error($"Unknown layout: {layout}");
                return false;
            }

            // Construct the fully qualified class name for this layout and check if
            // this Unity project contains the package / components to support it.
            var classNameIndex = qualifiedName.LastIndexOf(".", StringComparison.Ordinal);
            string packageAssembly = qualifiedName.Substring(0, classNameIndex);
            Type layoutView = Type.GetType($"{qualifiedName}, {packageAssembly}");
            return layoutView != null;
        }

        // Determines if for a given window is one of the layout view types supported by MPPM
        internal static bool IsWindowTypeSupported(String windowType)
        {
            // Compare against all supported layouts.
            foreach (var supportedWindow in k_SupportedFlagsClassName)
            {
                if (string.Equals(windowType, supportedWindow.Value, StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            // Compare against all Secondary view controls (Top bar views).
            foreach (var supportedViews in k_SupportedAuxiliaryViews)
            {
                if (string.Equals(windowType, supportedViews, StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }

        public static LayoutFlags GetFlagForQualifiedName(string qualifiedClassName)
        {
            foreach (KeyValuePair<LayoutFlags, string> flagToClassPair in k_SupportedFlagsClassName)
            {
                if (flagToClassPair.Value.Equals(qualifiedClassName))
                {
                    return flagToClassPair.Key;
                }
            }

            MppmLog.Warning($"GetFlagForQualifiedName called with Unsupported class: {qualifiedClassName}");
            return LayoutFlags.None;
        }

        public static bool ShouldDisableDuringEditMode(LayoutFlags flag)
        {
            return flag == LayoutFlags.InspectorWindow ||
                   flag == LayoutFlags.SceneHierarchyWindow ||
                   flag == LayoutFlags.SceneView ||
                   flag == LayoutFlags.EntitiesHierarchyWindow;
        }

        public static string GenerateLayoutName(LayoutFlags layoutFlags) => $"layout_{(int)layoutFlags:0000}";
        public static void SetFlag(ref LayoutFlags flags, LayoutFlags flag, bool set)
        {
            if (set)
            {
                flags = flag | flags;
            }
            else
            {
                flags = ~flag & flags;
            }
        }

        static bool IsOneFlagInCommon(LayoutFlags flags, LayoutFlags checkingAgainstFlag)
        {
            return (flags & checkingAgainstFlag) != 0;
        }

        public static LayoutFlags[] GetAsFlagsArray(LayoutFlags flags)
        {
            var result = new List<LayoutFlags>();
            foreach (var singleFlag in AllAsArray)
            {
                if (IsOneFlagInCommon(flags, singleFlag))
                    result.Add(singleFlag);
            }

            return result.ToArray();
        }
        public static int NumFlagsSet(LayoutFlags flags)
        {
            var result = 0;
            foreach (var singleFlag in AllAsArray)
            {
                if (IsOneFlagInCommon(flags, singleFlag))
                    result++;
            }

            return result;
        }
    }
}
