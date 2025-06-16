using System;
using System.Collections.Generic;
using Unity.Multiplayer.PlayMode.Common.Editor;
using Unity.Multiplayer.PlayMode.Configurations.Editor;
using Unity.Multiplayer.PlayMode.Configurations.Editor.Gui;
using UnityEngine.UIElements;
using Unity.Multiplayer.PlayMode.Scenarios.Editor.Api;
using Unity.Multiplayer.PlayMode.Scenarios.Editor.GraphsFoundation;
using UnityEngine;

namespace Unity.Multiplayer.PlayMode.Scenarios.Editor.Views
{
    class FreeRunningStatusElement
    {
        // Names representing views
        internal const string k_MultiplayerRunningModeName = "multiplayer-mode-state-field";
        internal const string k_InstanceButtonName = "multiplayer-mode-freerun-button";

        // Text values used in controls or labels
        private const string k_MultiplayerRunningModeLabelText = "Running Mode";
        private const string k_MultiplayerRunningModeTooltipText =
            k_DropDownScenarioControlText + " means the instance will be activated/deactivated when entering/exiting play mode. " +
            k_DropDownManualControlText + " means the instance can be activated only once and reused, which can improve iteration time.";
        private const string k_InstanceButtonActivateText = "Activate";
        private const string k_InstanceButtonDeactivateText = "Deactivate";
        private const string k_DropDownScenarioControlText = "Scenario Control";
        private const string k_DropDownManualControlText = "Manual Control";
        private const string k_InstanceButtonToolTipText = "Please correct invalid scenario configurational warnings.";

        // Class names representing view styling
        private const string k_HighlightedBackgroundClassName = "instance-view-highlighted";
        private const string k_RunModeDropDownMenuClassName = "runmode-dropdown";
        private const string k_RunModeButtonClassName = "runmode-button";

        // List of Mode states to be shown in Run Mode dropdown controls.
        private readonly List<RunModeState> k_DropdownStates = new List<RunModeState>()
        {
            RunModeState.ScenarioControl,
            RunModeState.ManualControl
        };

        private readonly InstanceDescription m_InstanceDescription;
        private PlaymodeStatusElement.InstanceView m_ParentInstanceView;
        private PopupField<RunModeState> m_DropDown;
        private Button m_FreeRunButton;

        public FreeRunningStatusElement(InstanceDescription description)
        {
            m_InstanceDescription = description;

            var instance = GetInstanceForThisElement();
            if (instance != null)
                instance.AddInstanceExecutionEventListener(OnInstanceExecutionUpdate);
        }

        private void OnInstanceExecutionUpdate(Instance instance, Node node)
        {
            RefreshStatusUI();
        }

        internal void BindRunModeDropDownElement(
            VisualElement instanceContainer,
            VisualElement statusContainer,
            VisualElement freeRunButtonContainer,
            PlaymodeStatusElement.InstanceView parentInstanceView)
        {
            // Create and bind Running Mode Label
            var runLabel = new Label();
            runLabel.text = k_MultiplayerRunningModeLabelText;
            runLabel.tooltip = k_MultiplayerRunningModeTooltipText;
            instanceContainer.Add(runLabel);

            // Create and bind Running Mode Dropdown menu
            m_DropDown = new PopupField<RunModeState>() { name = k_MultiplayerRunningModeName };
            m_DropDown.choices = k_DropdownStates;
            m_DropDown.formatListItemCallback = FormatRunningModeDropDownText;
            m_DropDown.formatSelectedValueCallback = FormatRunningModeDropDownText;
            m_DropDown.tooltip = k_MultiplayerRunningModeTooltipText;
            m_DropDown.SetValueWithoutNotify(m_InstanceDescription.RunModeState);
            m_DropDown.AddToClassList(k_RunModeDropDownMenuClassName);
            statusContainer.Add(m_DropDown);

            // Create and bind the Free Running Activate / Deactivate Button
            var instanceActionButton = new Button(){ name = k_InstanceButtonName };
            instanceActionButton.RegisterCallback<ClickEvent>(OnCallToActionButtonClicked);
            freeRunButtonContainer.AddToClassList(k_RunModeButtonClassName);
            freeRunButtonContainer.Clear();
            freeRunButtonContainer.Add(instanceActionButton);

            // Also keep a reference views for hide / show purposes
            m_FreeRunButton = instanceActionButton;
            m_ParentInstanceView = parentInstanceView;

            // If the scenario config is not valid, disable the call to action button
            var currentConfig = PlayModeManager.instance.ActivePlayModeConfig as ScenarioConfig;
            var isValid = currentConfig != null && currentConfig.IsConfigurationValid(out string _);
            instanceActionButton.SetEnabled(isValid);
            instanceActionButton.tooltip = isValid ? "" : k_InstanceButtonToolTipText;

            // Register callbacks to update Instance and UI
            m_DropDown.RegisterValueChangedCallback(OnSetFreeRunningModeSelected);

            // Finally refresh the UI after binding all elements.
            UpdateUI();

            // Ensure that we update Instance Status when shown.
            instanceContainer.schedule.Execute(() =>
            {
                if (IsInstanceRunning())
                    UpdateUI();
            }).Every(1000);
        }

        private void OnSetFreeRunningModeSelected(ChangeEvent<RunModeState> evt)
        {
            m_InstanceDescription.RunModeState = evt.newValue;
            UpdateUI();
        }

        private string FormatRunningModeDropDownText(RunModeState state)
        {
            switch (state)
            {
                case RunModeState.ScenarioControl:
                    return k_DropDownScenarioControlText;
                case RunModeState.ManualControl:
                    return k_DropDownManualControlText;
            }

            throw new Exception($"Unsupported Run mode state for Instance: {state}");
        }

        private void UpdateUI()
        {
            // Only show the call-to-action button if we are in Manual Mode state.
            bool isManualModeControlled = m_InstanceDescription.RunModeState == RunModeState.ManualControl;
            m_FreeRunButton.style.display = isManualModeControlled ? DisplayStyle.Flex : DisplayStyle.None;

            // Update call-to-action button text as needed
            var isInstanceRunning = IsInstanceRunning();
            m_FreeRunButton.text = isInstanceRunning ? k_InstanceButtonDeactivateText : k_InstanceButtonActivateText;

            // Disable Dropdown if scenario is running
            bool enabledDropdown = ScenarioRunner.instance.ActiveScenario == null ||
                                   ScenarioRunner.instance.ActiveScenario.Status.State != ScenarioState.Running;
            m_DropDown.SetEnabled(enabledDropdown && !isInstanceRunning);

            // Highlight the Instance row if it is Manual Contolled
            if (isManualModeControlled)
                m_ParentInstanceView.AddToClassList(k_HighlightedBackgroundClassName);
            else
                m_ParentInstanceView.RemoveFromClassList(k_HighlightedBackgroundClassName);

            // Update Instance Status
            RefreshStatusUI();
        }

        private Instance GetInstanceForThisElement()
        {
            var currentConfig = PlayModeManager.instance.ActivePlayModeConfig as ScenarioConfig;
            if (currentConfig == null || currentConfig.Scenario == null)
                return null;

            return currentConfig.Scenario.GetInstanceByName(m_InstanceDescription.Name);
        }

        private bool IsInstanceRunning()
        {
            var instance = GetInstanceForThisElement();
            return instance != null &&
                   instance.IsFreeRunMode() &&
                   instance.HasStartedAsFreeRunning();
        }

        private void OnCallToActionButtonClicked(ClickEvent ev)
        {
            var instance = GetInstanceForThisElement();
            if (instance == null)
            {
                Debug.LogWarning("Unable to toggle instance - Please ensure Scenario configurations are valid.");
                return;
            }

            ToggleActivateCloneInstance(!IsInstanceRunning());
            UpdateUI();

            // Because the Config window update is expansive, perform the update here as a single call.
            RefreshPlayModeConfigsWindowIfShown();
        }

        private void ToggleActivateCloneInstance(bool shouldActivate)
        {
            // Sanity check, don't toggle when in invalid modes
            if (m_InstanceDescription.RunModeState == RunModeState.ScenarioControl)
            {
                Debug.LogWarning("Cannot Activate an instance while it is in Scenario Control mode.");
                return;
            }

            // Grab the Instance for toggling
            var instance = GetInstanceForThisElement();
            if (instance == null)
            {
                Debug.LogError("Free Running Status Element Error: Unable to locate runtime instance.");
                return;
            }

            // Finally start or terminate it
            if (shouldActivate)
                instance!.StartOrResumeAsFreeRunning(false).Forget();
            else
                instance!.StopAsFreeRunning();
        }

        private void RefreshPlayModeConfigsWindowIfShown()
        {
            // Grab the Play Mode Scenarios Config window if it's showing
            var windows = Resources.FindObjectsOfTypeAll<PlayModeScenariosWindow>();
            if (windows == null || windows.Length != 1)
                return;

            // Ensure Scenario Configs are disabled if there are active Free Running
            // instances to prevent modifications while they are running.
            PlayModeScenariosWindow playModeScenariosWindow = windows[0];
            DetailView element = playModeScenariosWindow.rootVisualElement
                .Query<DetailView>(DetailView.k_DetailedViewName);

            if (element != null)
                element.UpdateView();

        }

        private void RefreshStatusUI()
        {
            var instance = GetInstanceForThisElement();
            if (instance != null && instance.IsActive())
            {
                var nodeStatus = instance.GetCurrentNodeStatus();
                var currentStage = instance.GetCurrentStage();
                m_ParentInstanceView.RefreshStatusUI(currentStage, nodeStatus);
                return;
            }

            m_ParentInstanceView.RefreshStatusUI(ExecutionStage.None, new List<NodeStatus>());
        }
    }
}
