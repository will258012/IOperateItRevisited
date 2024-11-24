extern alias FPSCamera;
using AlgernonCommons.Translation;
using ColossalFramework.UI;
using FPSCamera.FPSCamera.Cam.Controller;
using FPSCamera.FPSCamera.Utils;
using UnityEngine;

namespace IOperateIt.UI
{
    public class DriveButtons : MonoBehaviour
    {

        private void Awake()
        {
            citizenVehicleInfo_Button = Initialize(ref citizenVehicleInfo_Panel);
            cityServiceVehicleInfo_Button = Initialize(ref cityServiceVehicleInfo_Panel);
            publicTransportVehicleInfo_Button = Initialize(ref publicTransportVehicleInfo_Panel);
            FPSCamController.Instance.OnCameraEnabled += SetDisable;
            FPSCamController.Instance.OnCameraDisabled += SetEnable;
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
            FPSCamController.Instance.OnCameraEnabled -= SetDisable;
            FPSCamController.Instance.OnCameraDisabled -= SetEnable;
        }

        private void SetEnable() => enabled = true;

        private void SetDisable() => enabled = false;

        private UIButton Initialize<T>(ref T panel) where T : WorldInfoPanel
        {
            panel = UIView.library.Get<T>(typeof(T).Name);
            return CreateDriveButton(panel);
        }

        private UIButton CreateDriveButton<T>(T panel) where T : WorldInfoPanel
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
                var instanceID = GetPanelInstanceID(panel);
                if (instanceID.Type == InstanceType.Vehicle)
                {
                    var vehicle = VehicleManager.instance.m_vehicles.m_buffer[instanceID.Vehicle];
                    DriveController.Instance.StartDriving(vehicle.GetLastFramePosition(), vehicle.GetLastFrameData().m_rotation, vehicle.Info);
                    MainPanel.Instance._vehicleList.FindItem(vehicle.Info);
                }
                else if (instanceID.Type == InstanceType.ParkedVehicle)
                {
                    var vehicleParked = VehicleManager.instance.m_parkedVehicles.m_buffer[instanceID.ParkedVehicle];
                    DriveController.Instance.StartDriving(vehicleParked.m_position, vehicleParked.m_rotation, vehicleParked.Info);
                    MainPanel.Instance._vehicleList.FindItem(vehicleParked.Info);
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
        private void UpdateButtonVisibility<T>(T panel, UIButton button) where T : WorldInfoPanel
        {
            if (panel.component.isVisible)
            {
                var instanceID = GetPanelInstanceID(panel);
                button.isVisible = instanceID != default;
            }
        }

        private InstanceID GetPanelInstanceID<T>(T panel) where T : WorldInfoPanel => AccessUtils.GetFieldValue<InstanceID>(panel, "m_InstanceID");
        private CitizenVehicleWorldInfoPanel citizenVehicleInfo_Panel;
        private UIButton citizenVehicleInfo_Button;

        private CityServiceVehicleWorldInfoPanel cityServiceVehicleInfo_Panel;
        private UIButton cityServiceVehicleInfo_Button;

        private PublicTransportVehicleWorldInfoPanel publicTransportVehicleInfo_Panel;
        private UIButton publicTransportVehicleInfo_Button;

    }
}
