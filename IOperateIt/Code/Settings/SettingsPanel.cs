extern alias FPC;
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
        private const float MediumMargin = 24f;
        private const float LargeMargin = 50f;
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
            var currentY = MediumMargin;

            #region General Group
            UISpacers.AddTitleSpacer(scrollPanel, Margin, currentY, headerWidth, Translations.Translate("SETTINGS_GROUP_GENERAL"));
            currentY += LargeMargin;

            var language_DropDown = UIDropDowns.AddPlainDropDown(scrollPanel, MediumMargin, currentY, Translations.Translate("LANGUAGE_CHOICE"), Translations.LanguageList, Translations.Index);
            language_DropDown.eventSelectedIndexChanged += (control, index) =>
            {
                Translations.Index = index;
                OptionsPanelManager<SettingsPanel>.LocaleChanged();
                UI.MainPanel.instance?.LocaleChanged();
            };
            language_DropDown.parent.relativePosition = new Vector2(MediumMargin, currentY);
            currentY += language_DropDown.parent.height + MediumMargin;

            var logging_CheckBox = UICheckBoxes.AddPlainCheckBox(scrollPanel, MediumMargin, currentY, Translations.Translate("DETAIL_LOGGING"));
            logging_CheckBox.isChecked = Logging.DetailLogging;
            logging_CheckBox.eventCheckChanged += (_, isChecked) => Logging.DetailLogging = isChecked;
            currentY += logging_CheckBox.height + MediumMargin;
            #endregion

            #region Vehicle Group
            UISpacers.AddTitleSpacer(scrollPanel, Margin, currentY, headerWidth, Translations.Translate("SETTINGS_GROUP_VEHICLE"));
            currentY += LargeMargin;

            var maxVelocity_Slider = UISliders.AddPlainSliderWithValue(scrollPanel, MediumMargin, currentY, Translations.Translate("SETTINGS_MAXVELOCITY"), 50f, 200f, 1f, ModSettings.MaxVelocity, new UISliders.SliderValueFormat(valueMultiplier: 1, roundToNearest: 1, numberFormat: "N0", suffix: " km/h"));
            maxVelocity_Slider.eventValueChanged += (_, value) => ModSettings.MaxVelocity = value;
            currentY += maxVelocity_Slider.height + SliderMargin;

            var enginePower_Slider = UISliders.AddPlainSliderWithValue(scrollPanel, MediumMargin, currentY, Translations.Translate("SETTINGS_ENGINEPOWER"), 10f, 1500f, 1f, ModSettings.EnginePower, new UISliders.SliderValueFormat(valueMultiplier: 1, roundToNearest: 10, numberFormat: "N0", suffix: " KW"));
            enginePower_Slider.eventValueChanged += (_, value) => ModSettings.EnginePower = value;
            currentY += enginePower_Slider.height + SliderMargin;

            var brakingForce_Slider = UISliders.AddPlainSliderWithValue(scrollPanel, MediumMargin, currentY, Translations.Translate("SETTINGS_BRAKINGFORCE"), 5f, 150f, 1f, ModSettings.BrakingForce, new UISliders.SliderValueFormat(valueMultiplier: 1, roundToNearest: 1, numberFormat: "N0", suffix: " KN"));
            brakingForce_Slider.eventValueChanged += (_, value) => ModSettings.BrakingForce = value;
            currentY += brakingForce_Slider.height + SliderMargin;
            #endregion

            #region Camera Group
            UISpacers.AddTitleSpacer(scrollPanel, Margin, currentY, headerWidth, Translations.Translate("SETTINGS_GROUP_CAMERA"));
            currentY += LargeMargin;

            var offset = FPC.FPSCamera.UI.OffsetSliders.AddOffsetSlidersWithValue(scrollPanel, MediumMargin, currentY, Translations.Translate("SETTINGS_OFFSET"), -20f, 20f, .1f, ModSettings.Offset);
            offset.x_Slider.eventValueChanged += (_, value) => ModSettings.Offset.x = value;
            offset.y_Slider.eventValueChanged += (_, value) => ModSettings.Offset.y = value;
            offset.z_Slider.eventValueChanged += (_, value) => ModSettings.Offset.z = value;
            currentY += offset.slidersPanel.height;

            #endregion
            #region Game Group
            UISpacers.AddTitleSpacer(scrollPanel, Margin, currentY, headerWidth, Translations.Translate("SETTINGS_GROUP_GAME"));
            currentY += LargeMargin;

            var buildingCollision_CheckBox = UICheckBoxes.AddPlainCheckBox(scrollPanel, MediumMargin, currentY, Translations.Translate("SETTINGS_BUILDINGCOLLISION"));
            buildingCollision_CheckBox.isChecked = ModSettings.BuildingCollision;
            buildingCollision_CheckBox.eventCheckChanged += (_, isChecked) => ModSettings.BuildingCollision = isChecked;
            currentY += buildingCollision_CheckBox.height + Margin;

            var vehicleCollision_CheckBox = UICheckBoxes.AddPlainCheckBox(scrollPanel, MediumMargin, currentY, Translations.Translate("SETTINGS_VEHICLECOLLISION"));
            vehicleCollision_CheckBox.isChecked = ModSettings.VehicleCollision;
            vehicleCollision_CheckBox.eventCheckChanged += (_, isChecked) => ModSettings.VehicleCollision = isChecked;
            currentY += vehicleCollision_CheckBox.height + MediumMargin;
            #endregion

            #region Keybind Group
            UISpacers.AddTitleSpacer(scrollPanel, Margin, currentY, headerWidth, Translations.Translate("SETTINGS_GROUP_KEYS"));
            currentY += LargeMargin;

            var keyUUIToggle = Utils.UUISupport.UUIKeymapping.AddKeymapping(scrollPanel, MediumMargin, currentY);
            currentY += keyUUIToggle.Panel.height + Margin;

            var keyLightToggle = OptionsKeymapping.AddKeymapping(scrollPanel, MediumMargin, currentY, Translations.Translate("SETTINGS_KEYLIGHTTOGGLE"), ModSettings.KeyLightToggle);
            currentY += keyLightToggle.Panel.height + Margin;

            var keySirenToggle = OptionsKeymapping.AddKeymapping(scrollPanel, MediumMargin, currentY, Translations.Translate("SETTINGS_KEYSIRENTOGGLE"), ModSettings.KeySirenToggle);
            currentY += keySirenToggle.Panel.height + Margin;
            #endregion
        }
    }
}
