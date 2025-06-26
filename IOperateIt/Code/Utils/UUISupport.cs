using AlgernonCommons;
using AlgernonCommons.Keybinding;
using AlgernonCommons.Translation;
using ColossalFramework;
using ColossalFramework.UI;
using IOperateIt.Settings;
using IOperateIt.UI;
using System.IO;
using UnifiedUI.API;
using UnifiedUI.Helpers;
using UnityEngine;

namespace IOperateIt.Utils
{
    internal class UUISupport
    {
        /// <summary>
        /// Property to hold the reference to the UUI button.
        /// </summary>
        internal static UIComponent UUIButton { get; set; }

        /// <summary>
        /// Gets or sets the <see cref="UnsavedInputKey"/> reference for communicating with UUI.
        /// </summary>
        internal static UnsavedInputKey UUIKey { get; set; } = new UnsavedInputKey("IOperateIt UUI Key", new Keybinding(KeyCode.D, false, true, false));

        /// <summary>
        /// Register the UUI button.
        /// </summary>
        internal static void UUIRegister()
        {
            try
            {
                Logging.Message("UUISupport: Registering UUI button");
                // Register the UUI button with specified properties
                UUIButton = UUIAPI.Register(
                    name: "MainPanelBtn",
                    groupName: null,
                    tooltip: Translations.Translate("MAINPANELBTN_TOOLTIP"),
                    texture: UUIHelpers.LoadTexture(Path.Combine(AssemblyUtils.AssemblyPath, "Resources/Textures/IOperateItIcon.png")),
                    onToggle: (value) =>
                    {
                        if (value)
                        {
                            var UUIpos = UnifiedUI.GUI.MainPanel.Instance.isVisible
                                ? UnifiedUI.GUI.MainPanel.Instance.absolutePosition
                                : Object.FindObjectOfType<UnifiedUI.GUI.FloatingButton>().absolutePosition;

                            var UUIwidth = UnifiedUI.GUI.MainPanel.Instance.isVisible
                                ? UnifiedUI.GUI.MainPanel.Instance.width
                                : Object.FindObjectOfType<UnifiedUI.GUI.FloatingButton>().width;

                            var UUIheight = UnifiedUI.GUI.MainPanel.Instance.isVisible
                                ? UnifiedUI.GUI.MainPanel.Instance.height
                                : Object.FindObjectOfType<UnifiedUI.GUI.FloatingButton>().height;
                            // Position the main panel properly based on UUI button position
                            MainPanel.instance.Panel.absolutePosition = new Vector3(
                            UUIpos.x + (UUIpos.x < Screen.width / 2f ?
                            UUIwidth - 10f : -MainPanel.instance.Panel.width + 10f),
                            UUIpos.y + (UUIpos.y < Screen.height / 2f ?
                            UUIheight - 15f : -MainPanel.instance.Panel.height + 15f));
                        }
                        // Set main panel visibility
                        MainPanel.instance.Panel.isVisible = value;
                    },
                    onToolChanged: null,
                    activationKey: UUIKey,
                    activeKeys: null);

                // Customize button appearance if it's a UIButton
                if (UUIButton is UIButton btn)
                {
                    btn.foregroundSpriteMode = UIForegroundSpriteMode.Scale;
                    btn.scaleFactor = .8f;
                    btn.textColor = new Color32(255, 255, 255, 255);
                    btn.disabledTextColor = new Color32(7, 7, 7, 255);
                    btn.hoveredTextColor = new Color32(255, 255, 255, 255);
                    btn.focusedTextColor = new Color32(255, 255, 255, 255);
                    btn.pressedTextColor = new Color32(30, 30, 44, 255);
                }
            }
            catch (System.Exception e)
            {
                Logging.Error("UUISupport: \n");
                Logging.LogException(e);
            }
        }
        internal class UnsavedInputKey : UnifiedUI.Helpers.UnsavedInputKey
        {
            public UnsavedInputKey(string keyName, Keybinding inputKey) : base(keyName, "IOperateIt", inputKey.Encode()) { }
            /// <summary>
            /// Used for setting saving.
            /// </summary>
            public Keybinding Keybinding
            {
                get => new Keybinding(Key, Control, Shift, Alt);
                set => this.value = value.Encode();
            }
            public override void OnConflictResolved() => ModSettings.Save();
        }

        internal class UUIKeymapping : OptionsKeymapping
        {
            /// <summary>
            /// Adds a UUI keymapping control.
            /// </summary>
            /// <param name="parent">Parent component.</param>
            /// <param name="xPos">Relative x-position.</param>
            /// <param name="yPos">Relative y-position.</param>
            /// <returns>New <see cref="UUIKeymapping"/> control.</returns>
            public static UUIKeymapping AddKeymapping(UIComponent parent, float xPos, float yPos)
            {
                // Basic setup.
                var newKeymapping = parent.gameObject.AddComponent<UUIKeymapping>();
                newKeymapping.Label = Translations.Translate("SETTINGS_KEYUUITOGGLE");
                newKeymapping.Binding = UUIKey.Keybinding;
                newKeymapping.Panel.relativePosition = new Vector2(xPos, yPos);

                return newKeymapping;
            }
            /// <summary>
            /// Gets or sets the mod's UUI key.
            /// </summary>
            public override InputKey KeySetting
            {
                get => UUIKey.value;
                set
                {
                    UUIKey.value = value;
                    ButtonLabel = SavedInputKey.ToLocalizedString("KEYNAME", KeySetting);
                }
            }
        }
    }

}
