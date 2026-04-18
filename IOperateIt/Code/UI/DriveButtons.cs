using AlgernonCommons;
using AlgernonCommons.Translation;
using AlgernonCommons.UI;
using ColossalFramework;
using ColossalFramework.UI;
using System;
using UnityEngine;

namespace IOperateIt.UI;

public class DriveButtons : MonoBehaviour
{
    public static DriveButtons Instance { get; private set; }

    private CitizenVehicleWorldInfoPanel citizenVehicleInfo_Panel;
    private UIButton citizenVehicleInfo_Button;

    private CityServiceVehicleWorldInfoPanel cityServiceVehicleInfo_Panel;
    private UIButton cityServiceVehicleInfo_Button;

    private PublicTransportVehicleWorldInfoPanel publicTransportVehicleInfo_Panel;
    private UIButton publicTransportVehicleInfo_Button;

    private RaceVehicleWorldInfoPanel raceVehicleInfo_Panel;
    private UIButton raceVehicleInfo_Button;

    private float nextUpdateTime;
    private const float updateInterval = .25f;

    private void Awake()
    {
        try
        {
            Instance = this;
            citizenVehicleInfo_Button = Initialize(ref citizenVehicleInfo_Panel);
            cityServiceVehicleInfo_Button = Initialize(ref cityServiceVehicleInfo_Panel);
            publicTransportVehicleInfo_Button = Initialize(ref publicTransportVehicleInfo_Panel);
            raceVehicleInfo_Button = Initialize(ref raceVehicleInfo_Panel);
        }
        catch (Exception e)
        {
            Logging.LogException(e, "Failed to Initialize drive buttons");
        }
    }

    private void Update()
    {
        try
        {
            if (Time.time < nextUpdateTime)
            {
                return;
            }
            nextUpdateTime = Time.time + updateInterval;

            UpdateButtonVisibility(citizenVehicleInfo_Panel, citizenVehicleInfo_Button);
            UpdateButtonVisibility(cityServiceVehicleInfo_Panel, cityServiceVehicleInfo_Button);
            UpdateButtonVisibility(publicTransportVehicleInfo_Panel, publicTransportVehicleInfo_Button);
            UpdateButtonVisibility(raceVehicleInfo_Panel, raceVehicleInfo_Button);
        }
        catch (Exception e)
        {
            Logging.LogException(e, "Failed to update drive buttons");
            OnDestroy();
        }
    }

    private void OnDestroy()
    {
        Destroy(citizenVehicleInfo_Button);
        Destroy(cityServiceVehicleInfo_Button);
        Destroy(publicTransportVehicleInfo_Button);
        Destroy(raceVehicleInfo_Button);
    }

    public void SetEnable() => enabled = true;

    public void SetDisable() => enabled = false;

    private static UIButton Initialize<T>(ref T panel) where T : WorldInfoPanel, new()
    {
        panel = UIView.library.Get<T>(typeof(T).Name);
        return CreateDriveButton(panel);
    }

    private static UIButton CreateDriveButton<T>(T panel) where T : WorldInfoPanel, new()
    {
        var button = panel.component.AddUIComponent<UIButton>();
        button.name = panel.component.name + "_Drive";
        button.tooltip = Translations.Translate("DRIVEBTN_TOOLTIP");
        button.size = new Vector2(40f, 40f);
        button.scaleFactor = .8f;
        
        button.atlas = DriveButtonAtlas.Atlas;
        button.pressedBgSprite = DriveButtonAtlas.BgPressed;
        button.normalBgSprite = DriveButtonAtlas.Bg;
        button.hoveredBgSprite = DriveButtonAtlas.BgHovered;
        button.disabledBgSprite = DriveButtonAtlas.BgDisabled;
        button.normalFgSprite = DriveButtonAtlas.Fg;

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
                DriveController.Instance.StartDriving(vehicle.GetLastFramePosition(), vehicle.GetLastFrameData().m_rotation, vehicle.Info, color, true);
                MainPanel.Instance._vehicleList.FindItem<uint>(vehicle.m_infoIndex);
            }
            else if (instanceID.Type == InstanceType.ParkedVehicle)
            {
                ref var vehicleParked = ref Singleton<VehicleManager>.instance.m_parkedVehicles.m_buffer[instanceID.ParkedVehicle];
                Color color = vehicleParked.Info.m_vehicleAI.GetColor(instanceID.Vehicle, ref vehicleParked, Singleton<InfoManager>.instance.CurrentMode, Singleton<InfoManager>.instance.CurrentSubMode);
                DriveController.Instance.StartDriving(vehicleParked.m_position, vehicleParked.m_rotation, vehicleParked.Info, color, true);
                MainPanel.Instance._vehicleList.FindItem<uint>(vehicleParked.m_infoIndex);
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
    private static void UpdateButtonVisibility<T>(T panel, UIButton button) where T : WorldInfoPanel, new()
    {
        if (panel.component.isVisible)
        {
            var instanceID = WorldInfoPanel.GetCurrentInstanceID();
            button.isVisible = instanceID != default;
        }
    }
}
public static class DriveButtonAtlas
{
    public const string Fg = "IOperateItIcon";
    public const string FgPath = "Resources/" + Fg + ".png";

    public const string Bg = "OptionBase";
    public const string BgPressed = "OptionBasePressed";
    public const string BgHovered = "OptionBaseHovered";
    public const string BgDisabled = "OptionBaseDisabled";
    public static UITextureAtlas Atlas
    {
        get
        {
            if (field == null)
            {
                int index = 0;
                var textures = new Texture2D[5];
                var names = new string[5];

                // Loaded custom textures
                names[index] = Fg;
                textures[index++] = DriveCommonLoadTexture(Fg);

                // Existing core game textures
                names[index] = Bg;
                textures[index++] = DriveCommonRipTexture(Bg);

                names[index] = BgPressed;
                textures[index++] = DriveCommonRipTexture(BgPressed);

                names[index] = BgHovered;
                textures[index++] = DriveCommonRipTexture(BgHovered);

                names[index] = BgDisabled;
                textures[index++] = DriveCommonRipTexture(BgDisabled);

                field = UITextures.CreateSpriteAtlas(Fg + "_Atlas", 1024, textures, names);
            }
            return field;
        }
    }
    // Hack to use Algernon load texture
    private static Texture2D DriveCommonLoadTexture(string name) => UITextures.LoadCursor(name + ".png").m_texture;

    // Rip texture from the default global atlas
    private static Texture2D DriveCommonRipTexture(string name)
    {
        foreach (var si in UIView.GetAView().defaultAtlas.sprites)
        {
            if (si.name == name)
            {
                return si.texture;
            }
        }
        return null;
    }
}