using ColossalFramework;
using ColossalFramework.UI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using IOperateIt.Manager;
using IOperateIt.UIUtils;
using UnityEngine;

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
                        InstanceID instance = ReflectionUtils.ReadPrivate<CitizenVehicleWorldInfoPanel, InstanceID>(privateVehicleInfoPanel, "m_InstanceID");
                        Vehicle vehicle = manager.m_vehicles.m_buffer[instance.Vehicle];
                        VehicleHolder.getInstance().setVehicleInfo(vehicle.Info);
                        VehicleHolder.getInstance().setActive(vehicle.m_frame0.m_position, vehicle.m_frame0.m_rotation.eulerAngles);
                        vehicle.Unspawn(instance.Vehicle);
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
                        InstanceID instance = ReflectionUtils.ReadPrivate<CityServiceVehicleWorldInfoPanel, InstanceID>(serviceVehicleInfoPanel, "m_InstanceID");
                        Vehicle vehicle = manager.m_vehicles.m_buffer[instance.Vehicle];
                        VehicleHolder.getInstance().setVehicleInfo(vehicle.Info);
                        VehicleHolder.getInstance().setActive(vehicle.m_frame0.m_position, vehicle.m_frame0.m_rotation.eulerAngles);
                        vehicle.Unspawn(instance.Vehicle);
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
                        InstanceID instance = ReflectionUtils.ReadPrivate<PublicTransportVehicleWorldInfoPanel, InstanceID>(publicVehicleInfoPanel, "m_InstanceID");
                        Vehicle vehicle = manager.m_vehicles.m_buffer[instance.Vehicle];
                        VehicleHolder.getInstance().setVehicleInfo(vehicle.Info);
                        VehicleHolder.getInstance().setActive(vehicle.m_frame0.m_position, vehicle.m_frame0.m_rotation.eulerAngles);
                        vehicle.Unspawn(instance.Vehicle);
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
