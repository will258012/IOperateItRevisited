using ColossalFramework;
using ColossalFramework.UI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using IOperateIt.Manager;
using IOperateIt.UIUtils;
using UnityEngine;
using IOperateIt.Utils;

namespace IOperateIt.UI
{
    public class GamePanelExtender : MonoBehaviour
    {
        private bool initialized = false;

        private readonly Vector3 cameraButtonOffset = new Vector3(-100f, 8.0f, 0.0f);
        private readonly int cameraButtonSize = 24;

        VehicleManager manager;

        private CitizenVehicleWorldInfoPanel privateVehicleInfoPanel;
        private UIButton drivePrivateVehicleBtn;

        private CityServiceVehicleWorldInfoPanel serviceVehicleInfoPanel;
        private UIButton driveServiceVehicleBtn;

        private PublicTransportVehicleWorldInfoPanel publicVehicleInfoPanel;
        private UIButton publicVehicleBtn;

        private UIView uiView;

        void Awake()
        {
            manager = Singleton<VehicleManager>.instance;
            uiView = UIView.GetAView();
        }

        void OnDestroy()
        {
            Destroy(drivePrivateVehicleBtn);
            Destroy(driveServiceVehicleBtn);
            Destroy(publicVehicleBtn);
        }

        void Update()
        {
            if (!initialized) {
                privateVehicleInfoPanel = GameObject.Find("(Library) CitizenVehicleWorldInfoPanel").GetComponent<CitizenVehicleWorldInfoPanel>();
                privateVehicleInfoPanel.Find<UITextField>("VehicleName").width = 150;

                drivePrivateVehicleBtn = CreateCameraButton
                (
                    privateVehicleInfoPanel.component,
                    (component, param) =>
                    {
                        Vector3 position;
                        Vector3 rotation;
                        VehicleInfo vehicleInfo;
                        InstanceID instance = ReflectionUtils.ReadPrivate<CitizenVehicleWorldInfoPanel, InstanceID>(privateVehicleInfoPanel, "m_InstanceID");
                        vehicleInfo = instance.Vehicle != 0 ? manager.m_vehicles.m_buffer[instance.Vehicle].Info : manager.m_parkedVehicles.m_buffer[instance.ParkedVehicle].Info;
                        position = instance.Vehicle != 0 ? manager.m_vehicles.m_buffer[instance.Vehicle].GetLastFrameData().m_position : manager.m_parkedVehicles.m_buffer[instance.ParkedVehicle].m_position;
                        rotation = instance.Vehicle != 0 ? manager.m_vehicles.m_buffer[instance.Vehicle].GetLastFrameData().m_rotation.eulerAngles : manager.m_parkedVehicles.m_buffer[instance.ParkedVehicle].m_rotation.eulerAngles;

                        VehicleHolder.getInstance().setVehicleInfo(vehicleInfo);
                        VehicleHolder.getInstance().setActive(position, rotation);
                        privateVehicleInfoPanel.Hide();

                    }
                );

                serviceVehicleInfoPanel = GameObject.Find("(Library) CityServiceVehicleWorldInfoPanel").GetComponent<CityServiceVehicleWorldInfoPanel>();
                serviceVehicleInfoPanel.Find<UITextField>("VehicleName").width = 150;

                driveServiceVehicleBtn = CreateCameraButton
                (
                    serviceVehicleInfoPanel.component,
                    (component, param) =>
                    {
                        Vector3 position;
                        Vector3 rotation;
                        VehicleInfo vehicleInfo;
                        InstanceID instance = ReflectionUtils.ReadPrivate<CityServiceVehicleWorldInfoPanel, InstanceID>(serviceVehicleInfoPanel, "m_InstanceID");
                        vehicleInfo = instance.Vehicle != 0 ? manager.m_vehicles.m_buffer[instance.Vehicle].Info : manager.m_parkedVehicles.m_buffer[instance.ParkedVehicle].Info;
                        position = instance.Vehicle != 0 ? manager.m_vehicles.m_buffer[instance.Vehicle].GetLastFrameData().m_position : manager.m_parkedVehicles.m_buffer[instance.ParkedVehicle].m_position;
                        rotation = instance.Vehicle != 0 ? manager.m_vehicles.m_buffer[instance.Vehicle].GetLastFrameData().m_rotation.eulerAngles : manager.m_parkedVehicles.m_buffer[instance.ParkedVehicle].m_rotation.eulerAngles;

                        VehicleHolder.getInstance().setVehicleInfo(vehicleInfo);
                        VehicleHolder.getInstance().setActive(position, rotation);
                        serviceVehicleInfoPanel.Hide();
                    }
                );

                publicVehicleInfoPanel = GameObject.Find("(Library) PublicTransportVehicleWorldInfoPanel").GetComponent<PublicTransportVehicleWorldInfoPanel>();
                publicVehicleInfoPanel.Find<UITextField>("VehicleName").width = 150;

                publicVehicleBtn = CreateCameraButton
                (
                    publicVehicleInfoPanel.component,
                    (component, param) =>
                    {
                        Vector3 position;
                        Vector3 rotation;
                        VehicleInfo vehicleInfo;
                        InstanceID instance = ReflectionUtils.ReadPrivate<PublicTransportVehicleWorldInfoPanel, InstanceID>(publicVehicleInfoPanel, "m_InstanceID");
                        vehicleInfo = instance.Vehicle != 0 ? manager.m_vehicles.m_buffer[instance.Vehicle].Info : manager.m_parkedVehicles.m_buffer[instance.ParkedVehicle].Info;
                        position = instance.Vehicle != 0 ? manager.m_vehicles.m_buffer[instance.Vehicle].GetLastFrameData().m_position : manager.m_parkedVehicles.m_buffer[instance.ParkedVehicle].m_position;
                        rotation = instance.Vehicle != 0 ? manager.m_vehicles.m_buffer[instance.Vehicle].GetLastFrameData().m_rotation.eulerAngles : manager.m_parkedVehicles.m_buffer[instance.ParkedVehicle].m_rotation.eulerAngles;

                        VehicleHolder.getInstance().setVehicleInfo(vehicleInfo);
                        VehicleHolder.getInstance().setActive(position, rotation);
                        publicVehicleInfoPanel.Hide();

                    }
                );

                initialized = true;
            }
        }

        UIButton CreateCameraButton(UIComponent parentComponent, MouseEventHandler handler)
        {
            var button = uiView.AddUIComponent(typeof(UIButton)) as UIButton;
            button.name = "UDI Button";
            button.width = cameraButtonSize;
            button.height = cameraButtonSize;
            button.scaleFactor = 1.0f;
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
            button.eventClick += handler;
            button.AlignTo(parentComponent, UIAlignAnchor.TopRight);
            button.relativePosition += cameraButtonOffset;
            button.tooltip = "Drive this vehicle!";
            return button;
        }
    }
}
