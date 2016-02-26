using ColossalFramework.UI;
using ICities;
using RoadNamer.Panels;
using System;
using System.Reflection;
using UDriveIt.Manager;
using UDriveIt.Tools;
using UDriveIt.UI;
using UDriveIt.Utils;
using UnityEngine;

namespace UDriveIt
{
    public class UDriveItLoading : LoadingExtensionBase
    {

        GameObject mainToolBarBtn;
        GameObject ScrollablePanelBtn;

        VehicleHolder vehicleHolder;
        public UIMainPanel UI { get; set; }

        public override void OnCreated(ILoading loading)
        {
            //nah, don't do anything here
        }

        public override void OnLevelLoaded(LoadMode mode)
        {
            DebugOutputPanel.AddMessage(ColossalFramework.Plugins.PluginManager.MessageType.Message, "Loaded");

            if (mode == LoadMode.LoadGame || mode == LoadMode.NewGame || mode == LoadMode.NewMap || mode == LoadMode.LoadMap)
            {
                DebugOutputPanel.AddMessage(ColossalFramework.Plugins.PluginManager.MessageType.Message, "Loaded2");

                UIView view = UIView.GetAView();
                UITabstrip tabStrip;

                vehicleHolder = VehicleHolder.getInstance();

                UI = ToolsModifierControl.toolController.gameObject.AddComponent<UIMainPanel>();

            }
        }

        private void roadSegmentBtn_eventClick(UIComponent component, UIMouseEventParameter eventParam)
        {
            ToolController toolController = ToolsModifierControl.toolController;

            if (toolController != null)
            {
                if (toolController.GetComponent<RoadSelectTool>() == null)
                {
                    ToolsModifierControl.toolController.gameObject.AddComponent<RoadSelectTool>();

                    //I stole this from Traffic++ for now, until I can figure some things out. Quick fix!
                    FieldInfo toolControllerField = typeof(ToolController).GetField("m_tools", BindingFlags.Instance | BindingFlags.NonPublic);
                    if (toolControllerField != null)
                        toolControllerField.SetValue(toolController, toolController.GetComponents<ToolBase>());
                    FieldInfo toolModifierDictionary = typeof(ToolsModifierControl).GetField("m_Tools", BindingFlags.Static | BindingFlags.NonPublic);
                    if (toolModifierDictionary != null)
                        toolModifierDictionary.SetValue(null, null); // to force a refresh
                }

                RoadSelectTool roadSelectTool = ToolsModifierControl.SetTool<RoadSelectTool>();

                if (roadSelectTool == null)
                {
                    LoggerUtils.Log("Tool failed to initialise!");
                }
                else
                {
                }
            }
        }

        public override void OnLevelUnloading()
        {
        }

        public override void OnReleased()
        {
        }
    }
}
