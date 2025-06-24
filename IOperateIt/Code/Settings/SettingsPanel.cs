using AlgernonCommons;
using AlgernonCommons.Keybinding;
using AlgernonCommons.Translation;
using AlgernonCommons.UI;
using ColossalFramework.UI;
using IOperateIt.Settings;
using UnityEngine;

namespace IOperateIt
{
    public class SettingsPanel : OptionsPanelBase
    {
        private const float Margin = 5f;
        private const float LeftMargin = 24f;
        private const float GroupMargin = 40f;
        private const float TitleMargin = 50f;
        private const float SliderMargin = 60f;

        protected override void Setup()
        {
            var scrollPanel = AddUIComponent<UIScrollablePanel>();
            scrollPanel.relativePosition = new Vector2(0, Margin);
            scrollPanel.autoSize = false;
            scrollPanel.autoLayout = false;
            scrollPanel.width = width - 15f;
            scrollPanel.height = height - 15f;
            scrollPanel.clipChildren = true;
            scrollPanel.builtinKeyNavigation = true;
            scrollPanel.scrollWheelDirection = UIOrientation.Vertical;
            scrollPanel.eventVisibilityChanged += (_, isShow) => { if (isShow) scrollPanel.Reset(); };
            UIScrollbars.AddScrollbar(this, scrollPanel);

            var headerWidth = OptionsPanelManager<SettingsPanel>.PanelWidth - (Margin * 2f);
            var currentY = LeftMargin;

            #region General Group
            UISpacers.AddTitleSpacer(scrollPanel, Margin, currentY, headerWidth, Translations.Translate("SETTINGS_GROUP_GENERAL"));
            currentY += TitleMargin;

            var language_DropDown = UIDropDowns.AddPlainDropDown(scrollPanel, LeftMargin, currentY, Translations.Translate("LANGUAGE_CHOICE"), Translations.LanguageList, Translations.Index);
            language_DropDown.eventSelectedIndexChanged += (control, index) =>
            {
                Translations.Index = index;
                OptionsPanelManager<SettingsPanel>.LocaleChanged();
                UI.MainPanel.Instance?.LocaleChanged();
            };
            language_DropDown.parent.relativePosition = new Vector2(LeftMargin, currentY);
            currentY += language_DropDown.parent.height + LeftMargin;

            var logging_CheckBox = UICheckBoxes.AddPlainCheckBox(scrollPanel, LeftMargin, currentY, Translations.Translate("DETAIL_LOGGING"));
            logging_CheckBox.isChecked = Logging.DetailLogging;
            logging_CheckBox.eventCheckChanged += (_, isChecked) => Logging.DetailLogging = isChecked;
            currentY += logging_CheckBox.height + LeftMargin;
            #endregion

            #region Vehicle Group
            UISpacers.AddTitleSpacer(scrollPanel, Margin, currentY, headerWidth, Translations.Translate("SETTINGS_GROUP_VEHICLE"));
            currentY += TitleMargin;

            var maxVelocity_Slider = UISliders.AddPlainSliderWithValue(scrollPanel, LeftMargin, currentY, Translations.Translate("SETTINGS_MAXVELOCITY"), 50f, 160f, 1f, ModSettings.MaxVelocity, new UISliders.SliderValueFormat(valueMultiplier: 1, roundToNearest: 1, numberFormat: "N0", suffix: " km/h"));
            maxVelocity_Slider.eventValueChanged += (_, value) => ModSettings.MaxVelocity = value;
            currentY += maxVelocity_Slider.height + SliderMargin;

            var enginePower_Slider = UISliders.AddPlainSliderWithValue(scrollPanel, LeftMargin, currentY, Translations.Translate("SETTINGS_ENGINEPOWER"), 50f, 750f, 1f, ModSettings.EnginePower, new UISliders.SliderValueFormat(valueMultiplier: 1, roundToNearest: 10, numberFormat: "N0", suffix: " KW"));
            enginePower_Slider.eventValueChanged += (_, value) => ModSettings.EnginePower = value;
            currentY += enginePower_Slider.height + SliderMargin;

            var brakingForce_Slider = UISliders.AddPlainSliderWithValue(scrollPanel, LeftMargin, currentY, Translations.Translate("SETTINGS_BRAKINGFORCE"), 5f, 40f, 1f, ModSettings.BrakingForce, new UISliders.SliderValueFormat(valueMultiplier: 1, roundToNearest: 1, numberFormat: "N0", suffix: " KN"));
            brakingForce_Slider.eventValueChanged += (_, value) => ModSettings.BrakingForce = value;
            currentY += brakingForce_Slider.height + SliderMargin;
            #endregion

            #region Camera Group
            UISpacers.AddTitleSpacer(scrollPanel, Margin, currentY, headerWidth, Translations.Translate("SETTINGS_GROUP_CAMERA"));
            currentY += TitleMargin;

            var cameraX_Slider = UISliders.AddPlainSliderWithValue(scrollPanel, LeftMargin, currentY, Translations.Translate("SETTINGS_OFFSET_X"), -20f, 20f, 0.1f, ModSettings.Offset.x, new UISliders.SliderValueFormat(valueMultiplier: 1, roundToNearest: 0.1f, numberFormat: "N"));
            cameraX_Slider.eventValueChanged += (_, value) => ModSettings.Offset.x = value;
            currentY += cameraX_Slider.height + SliderMargin;

            var cameraY_Slider = UISliders.AddPlainSliderWithValue(scrollPanel, LeftMargin, currentY, Translations.Translate("SETTINGS_OFFSET_Y"), -20f, 20f, 0.1f, ModSettings.Offset.y, new UISliders.SliderValueFormat(valueMultiplier: 1, roundToNearest: 0.1f, numberFormat: "N"));
            cameraY_Slider.eventValueChanged += (_, value) => ModSettings.Offset.y = value;
            currentY += cameraY_Slider.height + SliderMargin;

            var cameraZ_Slider = UISliders.AddPlainSliderWithValue(scrollPanel, LeftMargin, currentY, Translations.Translate("SETTINGS_OFFSET_Z"), -20f, 20f, 0.1f, ModSettings.Offset.z, new UISliders.SliderValueFormat(valueMultiplier: 1, roundToNearest: 0.1f, numberFormat: "N"));
            cameraZ_Slider.eventValueChanged += (_, value) => ModSettings.Offset.z = value;
            currentY += cameraZ_Slider.height + SliderMargin;
            #endregion

            #region Game Group
            UISpacers.AddTitleSpacer(scrollPanel, Margin, currentY, headerWidth, Translations.Translate("SETTINGS_GROUP_GAME"));
            currentY += TitleMargin;

            var buildingCollision_CheckBox = UICheckBoxes.AddPlainCheckBox(scrollPanel, LeftMargin, currentY, Translations.Translate("SETTINGS_BUILDINGCOLLISION"));
            buildingCollision_CheckBox.isChecked = ModSettings.BuildingCollision;
            buildingCollision_CheckBox.eventCheckChanged += (_, isChecked) => ModSettings.BuildingCollision = isChecked;
            currentY += buildingCollision_CheckBox.height + Margin;

            var vehicleCollision_CheckBox = UICheckBoxes.AddPlainCheckBox(scrollPanel, LeftMargin, currentY, Translations.Translate("SETTINGS_VEHICLECOLLISION"));
            vehicleCollision_CheckBox.isChecked = ModSettings.VehicleCollision;
            vehicleCollision_CheckBox.eventCheckChanged += (_, isChecked) => ModSettings.VehicleCollision = isChecked;
            currentY += vehicleCollision_CheckBox.height + Margin;
            #endregion

            #region Keybind Group
            UISpacers.AddTitleSpacer(scrollPanel, Margin, currentY, headerWidth, Translations.Translate("SETTINGS_GROUP_KEYS"));
            currentY += TitleMargin;

            var keyUUIToggle = Utils.UUISupport.UUIKeymapping.AddKeymapping(scrollPanel, LeftMargin, currentY);
            currentY += keyUUIToggle.Panel.height + Margin;

            var keyLightToggle = OptionsKeymapping.AddKeymapping(scrollPanel, LeftMargin, currentY, Translations.Translate("SETTINGS_KEYLIGHTTOGGLE"), ModSettings.KeyLightToggle);
            currentY += keyLightToggle.Panel.height + Margin;

            var keySirenToggle = OptionsKeymapping.AddKeymapping(scrollPanel, LeftMargin, currentY, Translations.Translate("SETTINGS_KEYSIRENTOGGLE"), ModSettings.KeySirenToggle);
            currentY += keySirenToggle.Panel.height + Margin;
            #endregion
        }
    }
}
