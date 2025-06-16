using System;
using Unity.Multiplayer.PlayMode.Analytics.Editor;
using UnityEditor;
using UnityEngine.Analytics;
using UnityEngine.Serialization;

namespace Unity.Multiplayer.PlayMode.Analytics.Editor
{
    [Serializable]
    internal struct ThreeDotsSelectedData : IAnalytic.IData
    {
        public const string k_ThreeDotsSelectedEventName = "multiplayer_playmode_threeDotsSelected";

        public bool IsPlayMode;
        public string OptionSelected;
    }

    [AnalyticInfo(eventName: ThreeDotsSelectedData.k_ThreeDotsSelectedEventName, vendorKey: Constants.k_VendorKey)]
    internal class AnalyticsThreeDotsSelectedEvent : AnalyticsEvent<AnalyticsThreeDotsSelectedEvent, ThreeDotsSelectedData> { }
}
