﻿using AlgernonCommons.Translation;
using ColossalFramework;
using ColossalFramework.UI;
using UnityEngine;

namespace IOperateIt.UI
{
    public class DriveButtons : MonoBehaviour
    {
        public static DriveButtons instance { get; private set; }

        private CitizenVehicleWorldInfoPanel citizenVehicleInfo_Panel;
        private UIButton citizenVehicleInfo_Button;

        private CityServiceVehicleWorldInfoPanel cityServiceVehicleInfo_Panel;
        private UIButton cityServiceVehicleInfo_Button;

        private PublicTransportVehicleWorldInfoPanel publicTransportVehicleInfo_Panel;
        private UIButton publicTransportVehicleInfo_Button;

        private void Awake()
        {
            instance = this;
            citizenVehicleInfo_Button = Initialize(ref citizenVehicleInfo_Panel);
            cityServiceVehicleInfo_Button = Initialize(ref cityServiceVehicleInfo_Panel);
            publicTransportVehicleInfo_Button = Initialize(ref publicTransportVehicleInfo_Panel);
        }

        private void Update()
        {
            UpdateButtonVisibility(citizenVehicleInfo_Panel, citizenVehicleInfo_Button);
            UpdateButtonVisibility(cityServiceVehicleInfo_Panel, cityServiceVehicleInfo_Button);
            UpdateButtonVisibility(publicTransportVehicleInfo_Panel, publicTransportVehicleInfo_Button);
        }

        private void OnDestroy()
        {
            Destroy(citizenVehicleInfo_Button);
            Destroy(cityServiceVehicleInfo_Button);
            Destroy(publicTransportVehicleInfo_Button);
        }

        public void SetEnable() => enabled = true;

        public void SetDisable() => enabled = false;

        private static UIButton Initialize<T>(ref T panel) where T : WorldInfoPanel
        {
            panel = UIView.library.Get<T>(typeof(T).Name);
            return CreateDriveButton(panel);
        }

        private static UIButton CreateDriveButton<T>(T panel) where T : WorldInfoPanel
        {
            var button = panel.component.AddUIComponent<UIButton>();
            button.name = panel.component.name + "_Drive";
            button.tooltip = Translations.Translate("DRIVEBTN_TOOLTIP");
            button.size = new Vector2(40f, 40f);
            button.scaleFactor = .8f;
            button.pressedBgSprite = "OptionBasePressed";
            button.normalBgSprite = "OptionBase";
            button.hoveredBgSprite = "OptionBaseHovered";
            button.disabledBgSprite = "OptionBaseDisabled";
            button.normalFgSprite = "InfoIconTrafficCongestion";
            button.textColor = new Color32(255, 255, 255, 255);
            button.disabledTextColor = new Color32(7, 7, 7, 255);
            button.hoveredTextColor = new Color32(255, 255, 255, 255);
            button.focusedTextColor = new Color32(255, 255, 255, 255);
            button.pressedTextColor = new Color32(30, 30, 44, 255);
            button.eventClick += (_, p) =>
            {
                var instanceID = WorldInfoPanel.GetCurrentInstanceID();
                if (instanceID.Type == InstanceType.Vehicle)
                {
                    ref var vehicle = ref Singleton<VehicleManager>.instance.m_vehicles.m_buffer[instanceID.Vehicle];
                    Color color = vehicle.Info.m_vehicleAI.GetColor(instanceID.Vehicle, ref vehicle, Singleton<InfoManager>.instance.CurrentMode, Singleton<InfoManager>.instance.CurrentSubMode);
                    DriveController.instance.StartDriving(vehicle.GetLastFramePosition(), vehicle.GetLastFrameData().m_rotation, vehicle.Info, color, true);
                    MainPanel.instance._vehicleList.FindItem<uint>(vehicle.m_infoIndex);
                }
                else if (instanceID.Type == InstanceType.ParkedVehicle)
                {
                    ref var vehicleParked = ref Singleton<VehicleManager>.instance.m_parkedVehicles.m_buffer[instanceID.ParkedVehicle];
                    Color color = vehicleParked.Info.m_vehicleAI.GetColor(instanceID.Vehicle, ref vehicleParked, Singleton<InfoManager>.instance.CurrentMode, Singleton<InfoManager>.instance.CurrentSubMode);
                    DriveController.instance.StartDriving(vehicleParked.m_position, vehicleParked.m_rotation, vehicleParked.Info, color, true);
                    MainPanel.instance._vehicleList.FindItem<uint>(vehicleParked.m_infoIndex);
                }
                panel.component.isVisible = false;
            };
            button.AlignTo(panel.component, UIAlignAnchor.BottomRight);
            button.relativePosition = new Vector2
           (
                button.relativePosition.x - 5f,
                button.relativePosition.y - 60f/*Prevent conflict with FPC's button*/);
            return button;
        }
        private static void UpdateButtonVisibility<T>(T panel, UIButton button) where T : WorldInfoPanel
        {
            if (panel.component.isVisible)
            {
                var instanceID = WorldInfoPanel.GetCurrentInstanceID();
                button.isVisible = instanceID != default;
            }
        }
    }
}
