using ColossalFramework.UI;
using ICities;
using RoadNamer.Panels;
using System;
using System.Reflection;
using IOperateIt.Manager;
using IOperateIt.Tools;
using IOperateIt.UI;
using IOperateIt.Utils;
using UnityEngine;

namespace IOperateIt
{
    public class IOperateItLoading : LoadingExtensionBase
    {

        GameObject mainToolBarBtn;
        GameObject ScrollablePanelBtn;

        VehicleHolder vehicleHolder;
        public UIMainPanel UI { get; set; }
        public GamePanelExtender panelExtender { get; set; }

        public override void OnCreated(ILoading loading)
        {
            OptionsManager.Instance().LoadOptions();
            //nah, don't do anything here
        }

        public override void OnLevelLoaded(LoadMode mode)
        {

            if (mode == LoadMode.LoadGame || mode == LoadMode.NewGame || mode == LoadMode.NewMap || mode == LoadMode.LoadMap)
            {
                UIView view = UIView.GetAView();

                vehicleHolder = VehicleHolder.getInstance();

                UI = ToolsModifierControl.toolController.gameObject.AddComponent<UIMainPanel>();
                panelExtender = ToolsModifierControl.toolController.gameObject.AddComponent<GamePanelExtender>();
                
            }
        }

        public override void OnLevelUnloading()
        {
            EventBusManager.Instance().Clear();
        }

        public override void OnReleased()
        {
        }
    }
}
