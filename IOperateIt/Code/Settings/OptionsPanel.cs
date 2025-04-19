extern alias FPSCamera;
using AlgernonCommons;
using AlgernonCommons.Keybinding;
using AlgernonCommons.Translation;
using AlgernonCommons.UI;
using FPSCamera.FPSCamera.UI;
using IOperateIt.Settings;
using UnityEngine;

namespace IOperateIt
{
    public class OptionsPanel : OptionsPanelBase
    {
        private const float Margin = 5f;
        private const float LeftMargin = 24f;
        private const float GroupMargin = 40f;
        private const float TitleMargin = 50f;
        private const float SliderMargin = 60f;

        protected override void Setup()
        {
            var headerWidth = OptionsPanelManager<OptionsPanel>.PanelWidth - (Margin * 2f);
            var currentY = LeftMargin;

            var language_DropDown = UIDropDowns.AddPlainDropDown(this, LeftMargin, currentY, Translations.Translate("LANGUAGE_CHOICE"), Translations.LanguageList, Translations.Index);
            language_DropDown.eventSelectedIndexChanged += (control, index) =>
            {
                Translations.Index = index;
                OptionsPanelManager<OptionsPanel>.LocaleChanged();
                UI.MainPanel.Instance?.LocaleChanged();
            };
            language_DropDown.parent.relativePosition = new Vector2(LeftMargin, currentY);
            currentY += language_DropDown.parent.height + LeftMargin;

            var logging_CheckBox = UICheckBoxes.AddPlainCheckBox(this, LeftMargin, currentY, Translations.Translate("DETAIL_LOGGING"));
            logging_CheckBox.isChecked = Logging.DetailLogging;
            logging_CheckBox.eventCheckChanged += (_, isChecked) => Logging.DetailLogging = isChecked;
            currentY += logging_CheckBox.height + LeftMargin;

            var maxVelocity_Slider = UISliders.AddPlainSliderWithValue(this, LeftMargin, currentY, Translations.Translate("SETTINGS_MAXVELOCITY"), 50f, 160f, 1f, ModSettings.MaxVelocity, new UISliders.SliderValueFormat(valueMultiplier: 1, roundToNearest: 1, numberFormat: "N0", suffix: "km/h"));
            maxVelocity_Slider.eventValueChanged += (_, value) => ModSettings.MaxVelocity = value;
            currentY += maxVelocity_Slider.height + SliderMargin;

            var accelerationForce_Slider = UISliders.AddPlainSliderWithValue(this, LeftMargin, currentY, Translations.Translate("SETTINGS_ACCELERATIONFORCE"), 10f, 200f, 1f, ModSettings.AccelerationForce, new UISliders.SliderValueFormat(valueMultiplier: 1, roundToNearest: 1, numberFormat: "N0", suffix: "m/s"));
            accelerationForce_Slider.eventValueChanged += (_, value) => ModSettings.AccelerationForce = value;
            currentY += accelerationForce_Slider.height + SliderMargin;

            var breakingForce_Slider = UISliders.AddPlainSliderWithIntegerValue(this, LeftMargin, currentY, Translations.Translate("SETTINGS_BREAKINGFORCE"), 10f, 200f, 1f, ModSettings.BreakingForce);
            breakingForce_Slider.eventValueChanged += (_, value) => ModSettings.BreakingForce = value;
            currentY += breakingForce_Slider.height + SliderMargin;

            var offset = OffsetSliders.AddOffsetSlidersWithValue(this, LeftMargin, currentY, Translations.Translate("SETTINGS_OFFSET"), -20f, 20f, .1f, ModSettings.Offset);
            offset.x_Slider.eventValueChanged += (_, value) => ModSettings.Offset.x = value;
            offset.y_Slider.eventValueChanged += (_, value) => ModSettings.Offset.y = value;
            offset.z_Slider.eventValueChanged += (_, value) => ModSettings.Offset.z = value;
            currentY += offset.slidersPanel.height;

            var keyUUIToggle = Utils.UUISupport.UUIKeymapping.AddKeymapping(this, LeftMargin, currentY);
            currentY += keyUUIToggle.Panel.height + Margin;

            var keyLightToggle = OptionsKeymapping.AddKeymapping(this, LeftMargin, currentY, Translations.Translate("SETTINGS_KEYLIGHTTOGGLE"), ModSettings.KeyLightToggle);
            currentY += keyLightToggle.Panel.height + Margin;
        }
    }
}
