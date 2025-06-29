extern alias FPSCamera;
using AlgernonCommons.Translation;
using AlgernonCommons.UI;
using AlgernonCommons.Utils;
using ColossalFramework.UI;
using FPSCamera.FPSCamera.Utils;
using IOperateIt.Settings;
using IOperateIt.Tools;
using IOperateIt.Utils;
using UnifiedUI.GUI;
using UnityEngine;

namespace IOperateIt.UI
{
    public class MainPanel : MonoBehaviour
    {

        public static MainPanel Instance { get; private set; }
        public UIPanel Panel { get; set; }
        public UIButton GetMainButton() => mainBtn ?? UUISupport.UUIButton as UIButton;

        private const float Margin = 10f;
        private const float VehicleRowHeight = 40f;

        private UIButton mainBtn;
        private UIButton spawnBtn;
        private UIButton modSettingsBtn;
        internal UIList vehicleList;
        private PreviewPanel previewPanel;

        private void Awake()
        {
            Instance = this;
            #region Main Panel
            Panel = UIView.GetAView().AddUIComponent(typeof(UIPanel)) as UIPanel;
            Panel.autoLayout = false;
            Panel.canFocus = true;
            Panel.isInteractive = true;
            Panel.atlas = UITextures.InGameAtlas;
            Panel.backgroundSprite = "UnlockingPanel2";
            Panel.width = 800f;

            var currentY = Margin;
            vehicleList = UIList.AddUIList<MainPanelRow>(Panel, Margin, currentY, 400f, 300f, VehicleRowHeight);
            var vehicleInfos = new FastList<object>();

            for (uint i = 0; i < PrefabCollection<VehicleInfo>.PrefabCount(); i++)
            {
                var vehicleInfo = PrefabCollection<VehicleInfo>.GetPrefab(i);

                if (vehicleInfo != null && !vehicleInfo.name.ToLower().Contains("trailer"))
                {
                    vehicleInfos.Add(i);
                }
            }
            vehicleList.Data = vehicleInfos;
            vehicleList.EventSelectionChanged += (component, obj) =>
            {
                if (obj is uint index)
                {
                    VehicleInfo selectedVehicle = PrefabCollection<VehicleInfo>.GetPrefab(index);
                    if (selectedVehicle != null)
                    {
                        previewPanel.SetTarget(selectedVehicle);
                        DriveController.Instance.vehicleInfo = selectedVehicle;
                        spawnBtn.isEnabled = true;
                    }
                }
            };

            previewPanel = Panel.AddUIComponent<PreviewPanel>();
            previewPanel.relativePosition = UILayout.PositionRightOf(vehicleList);

            currentY += vehicleList.height + Margin;

            spawnBtn = UIButtons.AddButton(Panel, (vehicleList.width - 200f) / 2f, currentY, Translations.Translate("SPAWNBTN_TEXT"), 200f, 40f);
            spawnBtn.isEnabled = false;
            spawnBtn.playAudioEvents = true;
            spawnBtn.eventClick += SpawnBtn_eventClick;
            modSettingsBtn = UIButtons.AddButton(Panel, previewPanel.relativePosition.x + (previewPanel.width - 200f) / 2f, currentY, Translations.Translate("MODSETTINGSBTN_TEXT"));
            modSettingsBtn.eventClick += (_, e) => FPSCamera.FPSCamera.UI.MainPanel.OpenSettingsPanel(Mod.Instance.Name);
            Panel.height = currentY + spawnBtn.height + Margin;
            Panel.Hide();

            if (ModSupport.FoundUUI)
            {
                UUISupport.UUIRegister();
                return;
            }
            #endregion

            #region Main Button
            float x = ModSettings.MainButtonPos.x, y = ModSettings.MainButtonPos.y;
            if (x < 0f || y < 0f)
            {
                var escbutton = UIView.GetAView().FindUIComponent("Esc");
                x = escbutton.absolutePosition.x;
                y = escbutton.absolutePosition.y + escbutton.height * 1.5f;

                ModSettings.MainButtonPos = new Vector2(x, y);
            }
            mainBtn = UIView.GetAView().AddUIComponent(typeof(UIButton)) as UIButton;
            mainBtn.name = "MainButton";
            mainBtn.tooltip = Translations.Translate("MAINPANELBTN_TOOLTIP");
            mainBtn.absolutePosition = new Vector3(x, y);
            mainBtn.size = new Vector2(40f, 40f);
            mainBtn.scaleFactor = .8f;
            mainBtn.pressedBgSprite = "OptionBasePressed";
            mainBtn.normalBgSprite = "OptionBase";
            mainBtn.hoveredBgSprite = "OptionBaseHovered";
            mainBtn.disabledBgSprite = "OptionBaseDisabled";
            mainBtn.normalFgSprite = "InfoIconTrafficCongestion";
            mainBtn.textColor = new Color32(255, 255, 255, 255);
            mainBtn.disabledTextColor = new Color32(7, 7, 7, 255);
            mainBtn.hoveredTextColor = new Color32(255, 255, 255, 255);
            mainBtn.focusedTextColor = new Color32(255, 255, 255, 255);
            mainBtn.pressedTextColor = new Color32(30, 30, 44, 255);
            mainBtn.eventClick += (_, m) =>
            {
                Panel.absolutePosition = new Vector3(mainBtn.absolutePosition.x +
                    (mainBtn.absolutePosition.x < Screen.width / 2f ? mainBtn.width - 10f : -Panel.width + 10f),
                                                           mainBtn.absolutePosition.y +
                (mainBtn.absolutePosition.y < Screen.height / 2f ? mainBtn.height - 15f : -Panel.height + 15f));
                Panel.isVisible = !Panel.isVisible;
            };

            //drag
            var mainBtn_drag = mainBtn.AddUIComponent<UIDragHandle>();
            mainBtn_drag.name = mainBtn.name + "_drag";
            mainBtn_drag.size = mainBtn.size;
            mainBtn_drag.relativePosition = Vector3.zero;
            mainBtn_drag.target = mainBtn;
            mainBtn_drag.transform.parent = mainBtn.transform;
            mainBtn_drag.eventMouseDown += (_, p) => Panel.isVisible = false;
            mainBtn_drag.eventMouseUp += (_, p) => { ModSettings.MainButtonPos = mainBtn.absolutePosition; ModSettings.Save(); };
            #endregion
        }

        private void OnDestory()
        {
            spawnBtn.eventClick -= SpawnBtn_eventClick;
            Destroy(Panel);
            Destroy(GetMainButton());
        }
        public bool OnEsc()
        {
            if (Panel.isVisible)
            {
                Panel.Hide();
                if (ModSupport.FoundUUI)
                {
                    (GetMainButton() as ButtonBase).IsActive = false;
                }
                return true;
            }
            return false;
        }
        private void SpawnBtn_eventClick(UIComponent component, UIMouseEventParameter eventParam)
        {
            if (DriveController.Instance?.vehicleInfo != null)
            {
                if (ToolsModifierControl.GetCurrentTool<RoadSelectTool>() == null)
                {
                    ToolsModifierControl.toolController.CurrentTool = RoadSelectTool.Instance;
                    ToolsModifierControl.SetTool<RoadSelectTool>();
                }
                else
                {
                    ToolsModifierControl.SetTool<DefaultTool>();
                }
                OnEsc();
            }
            else spawnBtn.isEnabled = false;
        }
        public void LocaleChanged()
        {
            OnDestory();
            Awake();
        }

        /// <summary>
        /// UIList row item for vehicle prefabs.
        /// </summary>
        public class MainPanelRow : UIListRow
        {
            // Layout constants - private.
            private const float VehicleSpriteSize = 40f;
            private const float SteamSpriteWidth = 26f;
            private const float SteamSpriteHeight = 16f;
            private const float ScrollMargin = 10f;

            // Vehicle name label.
            private UILabel _vehicleNameLabel;

            // Preview image.
            private UISprite _vehicleSprite;

            // Steam icon.
            private UISprite _steamSprite;

            /// <summary>
            /// Vehicle prefab.
            /// </summary>
            private VehicleInfo _info;

            /// <summary>
            /// Gets the height for this row.
            /// </summary>
            public override float RowHeight => VehicleRowHeight;

            /// <summary>
            /// Generates and displays a row.
            /// </summary>
            /// <param name="data">Object data to display.</param>
            /// <param name="rowIndex">Row index number (for background banding).</param>
            public override void Display(object data, int rowIndex)
            {
                _info = PrefabCollection<VehicleInfo>.GetPrefab((uint)data);
                // Perform initial setup for new rows.
                if (_vehicleNameLabel == null)
                {
                    // Add object name label.
                    _vehicleNameLabel = AddLabel(VehicleSpriteSize + Margin, width - Margin - VehicleSpriteSize - Margin - SteamSpriteWidth - ScrollMargin - Margin, wordWrap: true);

                    // Add preview sprite image.
                    _vehicleSprite = AddUIComponent<UISprite>();
                    _vehicleSprite.height = VehicleSpriteSize;
                    _vehicleSprite.width = VehicleSpriteSize;
                    _vehicleSprite.relativePosition = Vector2.zero;

                    // Add setam sprite.
                    _steamSprite = AddUIComponent<UISprite>();
                    _steamSprite.width = SteamSpriteWidth;
                    _steamSprite.height = SteamSpriteHeight;
                    _steamSprite.atlas = UITextures.InGameAtlas;
                    _steamSprite.spriteName = "SteamWorkshop";
                    _steamSprite.relativePosition = new Vector2(width - Margin - ScrollMargin - SteamSpriteWidth, (height - SteamSpriteHeight) / 2f);
                }

                _vehicleNameLabel.text = _info.GetUncheckedLocalizedTitle();

                _vehicleSprite.atlas = _info?.m_Atlas;
                _vehicleSprite.spriteName = _info?.m_Thumbnail;

                _steamSprite.isVisible = PrefabUtils.IsWorkshopAsset(_info);

                // Set initial background as deselected state.
                Deselect(rowIndex);
            }
        }
    }
}