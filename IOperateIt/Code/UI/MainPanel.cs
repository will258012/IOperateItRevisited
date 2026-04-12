extern alias FPC;
using AlgernonCommons.Translation;
using AlgernonCommons.UI;
using AlgernonCommons.Utils;
using ColossalFramework.UI;
using IOperateIt.Settings;
using IOperateIt.Utils;
using UnifiedUI.GUI;
using UnityEngine;

namespace IOperateIt.UI
{
    public class MainPanel : MonoBehaviour
    {

        public static MainPanel Instance { get; private set; }
        public UIPanel Panel { get; set; }
        public UIButton GetMainButton() => _mainBtn ?? UUISupport.UUIButton as UIButton;
        /// <summary>
        /// Gets or sets the panel's last saved position.
        /// </summary>
        public static Vector3 SavedPanelPosition { get; set; } = DefaultPosition;
        /// <summary>
        /// Gets or sets the button's last saved position.
        /// </summary>
        public static Vector3 SavedButtonPosition { get; set; } = DefaultPosition;

        public static Vector3 DefaultPosition => Vector3.left;

        private const float Margin = 10f;
        private const float VehicleRowHeight = 40f;
        
        private const float CloseButtonSize = 35f;
        private const float MainButtonSize = 40f;
        private const float TitleHeight = 13f;

        private UIButton _mainBtn;
        private UIButton _modSettingsBtn;
        private UIButton _spawnBtn;
        internal UIList _vehicleList;
        private PreviewPanel _previewPanel;

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

            var currentY = CloseButtonSize + Margin;
            _vehicleList = UIList.AddUIList<MainPanelRow>(Panel, Margin, currentY, 400f, 320f, VehicleRowHeight);
            var vehicleInfos = new FastList<object>();

            for (uint i = 0; i < PrefabCollection<VehicleInfo>.PrefabCount(); i++)
            {
                var vehicleInfo = PrefabCollection<VehicleInfo>.GetPrefab(i);

                if (vehicleInfo != null && !vehicleInfo.name.ToLower().Contains("trailer"))
                {
                    vehicleInfos.Add(i);
                }
            }
            _vehicleList.Data = vehicleInfos;
            _vehicleList.EventSelectionChanged += (component, obj) =>
            {
                if (Panel.isVisible && obj is uint index)
                {
                    UpdateListEvent(index);
                }
            };

            Panel.eventVisibilityChanged += (component, vis) =>
            {
                if (vis == true && _vehicleList.SelectedIndex >= 0)
                {
                    UpdateListEvent((uint)_vehicleList.SelectedItem);
                }
                else
                {
                    SavedPanelPosition = Panel.absolutePosition;
                    ModSettings.Save();
                }
            };

            _previewPanel = Panel.AddUIComponent<PreviewPanel>();
            _previewPanel.relativePosition = UILayout.PositionRightOf(_vehicleList);

            currentY += _vehicleList.height + Margin;

            _spawnBtn = UIButtons.AddButton(Panel, (_vehicleList.width - 200f) / 2f, currentY, Translations.Translate("SPAWNBTN_TEXT"), 200f, 40f);
            _spawnBtn.isEnabled = false;
            _spawnBtn.playAudioEvents = true;
            _spawnBtn.eventClick += SpawnBtnClickEvent;
            _modSettingsBtn = UIButtons.AddButton(Panel, _previewPanel.relativePosition.x + (_previewPanel.width - 200f) / 2f, currentY, Translations.Translate("MODSETTINGSBTN_TEXT"));
            _modSettingsBtn.eventClick += (_, e) => FPC.FPSCamera.UI.MainPanel.OpenSettingsPanel(Mod.Instance.Name);
            Panel.height = currentY + _spawnBtn.height + Margin;

            // Title
            {
                // Drag bar.
                var dragHandle = Panel.AddUIComponent<UIDragHandle>();
                dragHandle.size = Panel.size;
                dragHandle.relativePosition = Vector3.zero;
                dragHandle.target = Panel;
                dragHandle.SendToBack();

                // Title label.
                var titleLabel = UILabels.AddLabel(Panel, CloseButtonSize, TitleHeight, Translations.Translate("MAINPANELBTN_TOOLTIP"), Panel.width - CloseButtonSize - CloseButtonSize, alignment: UIHorizontalAlignment.Center);
                titleLabel.SendToBack();

                // Close button.
                var closeButton = Panel.AddUIComponent<UIButton>();
                closeButton.relativePosition = new Vector2(Panel.width - CloseButtonSize, 2);
                closeButton.atlas = UITextures.InGameAtlas;
                closeButton.normalBgSprite = "buttonclose";
                closeButton.hoveredBgSprite = "buttonclosehover";
                closeButton.pressedBgSprite = "buttonclosepressed";
                closeButton.eventClick += (c, p) => OnEsc();
            }

            Panel.Hide();

            if (FPC.FPSCamera.Utils.ModSupport.FoundUUI)
            {
                UUISupport.UUIRegister();
                return;
            }
            #endregion

            #region Main Button
            float x = SavedButtonPosition.x, y = SavedButtonPosition.y;
            if (x < 0f || y < 0f)
            {
                var escbutton = UIView.GetAView().FindUIComponent("Esc");
                x = escbutton.absolutePosition.x;
                y = escbutton.absolutePosition.y + escbutton.height * 1.5f + MainButtonSize + Margin;

                SavedButtonPosition = new Vector2(x, y);
            }
            _mainBtn = UIView.GetAView().AddUIComponent(typeof(UIButton)) as UIButton;
            _mainBtn.name = "MainButton";
            _mainBtn.tooltip = Translations.Translate("MAINPANELBTN_TOOLTIP");
            _mainBtn.absolutePosition = new Vector3(x, y);
            _mainBtn.size = new Vector2(MainButtonSize, MainButtonSize);
            _mainBtn.scaleFactor = .8f;

            _mainBtn.atlas = DriveButtonAtlas.Atlas;
            _mainBtn.pressedBgSprite = DriveButtonAtlas.BgPressed;
            _mainBtn.normalBgSprite = DriveButtonAtlas.Bg;
            _mainBtn.hoveredBgSprite = DriveButtonAtlas.BgHovered;
            _mainBtn.disabledBgSprite = DriveButtonAtlas.BgDisabled;
            _mainBtn.normalFgSprite = DriveButtonAtlas.Fg;

            _mainBtn.textColor = new Color32(255, 255, 255, 255);
            _mainBtn.disabledTextColor = new Color32(7, 7, 7, 255);
            _mainBtn.hoveredTextColor = new Color32(255, 255, 255, 255);
            _mainBtn.focusedTextColor = new Color32(255, 255, 255, 255);
            _mainBtn.pressedTextColor = new Color32(30, 30, 44, 255);
            _mainBtn.eventClick += (_, m) =>
            {
                if (!Panel.isVisible) LoadPanelPosition();

                Panel.isVisible = !Panel.isVisible;
            };

            //drag
            var mainBtn_drag = _mainBtn.AddUIComponent<UIDragHandle>();
            mainBtn_drag.name = _mainBtn.name + "_drag";
            mainBtn_drag.size = _mainBtn.size;
            mainBtn_drag.relativePosition = Vector3.zero;
            mainBtn_drag.target = _mainBtn;
            mainBtn_drag.transform.parent = _mainBtn.transform;
            mainBtn_drag.eventMouseDown += (_, p) => Panel.isVisible = false;
            mainBtn_drag.eventMouseUp += (_, p) => { SavedButtonPosition = _mainBtn.absolutePosition; ModSettings.Save(); };
            #endregion
        }

        private void OnDestory()
        {
            _spawnBtn.eventClick -= SpawnBtnClickEvent;
            Destroy(Panel);
            Destroy(GetMainButton());
        }
        public bool OnEsc()
        {
            if (Panel.isVisible)
            {
                Panel.Hide();
                if (FPC.FPSCamera.Utils.ModSupport.FoundUUI)
                {
                    (GetMainButton() as ButtonBase).IsActive = false;
                }
                return true;
            }
            return false;
        }
        public void LoadPanelPosition()
        {
            if (Panel == null) return;
            var view = UIView.GetAView();

            Panel.absolutePosition = SavedPanelPosition.x >= 0f
                ? SavedPanelPosition
                : new Vector3(Mathf.Floor((view.fixedWidth - Panel.width) / 2), Mathf.Floor((view.fixedHeight - Panel.height) / 2));

            // Ensure panel is fully visible on screen (in case of e.g. UI scaling changes).
            float clampedXpos = Mathf.Clamp(Panel.absolutePosition.x, 0f, view.fixedWidth - Panel.width);
            float clampedYpos = Mathf.Clamp(Panel.absolutePosition.y, 0f, view.fixedHeight - Panel.height);
            Panel.absolutePosition = new Vector2(clampedXpos, clampedYpos);
        }

        private void UpdateListEvent(uint index)
        {
            VehicleInfo selectedVehicle = PrefabCollection<VehicleInfo>.GetPrefab(index);
            if (selectedVehicle != null)
            {
                if (selectedVehicle.name == "Forest Forwarder 01") // The Forest Forwarder has blinking alpha set for some reason
                {
                    Color adjustedColor = selectedVehicle.m_color0;
                    adjustedColor.a = 0;
                    _previewPanel.SetTarget(selectedVehicle, adjustedColor, true);
                    DriveController.Instance.UpdateColor(adjustedColor, true);
                }
                else
                {
                    _previewPanel.SetTarget(selectedVehicle);
                    DriveController.Instance.UpdateColor(default, false);
                }
                DriveController.Instance.UpdateVehicleInfo(selectedVehicle);
                _spawnBtn.isEnabled = true;
            }
        }
        private void SpawnBtnClickEvent(UIComponent component, UIMouseEventParameter eventParam)
        {
            if (DriveController.Instance.IsVehicleInfoSet())
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
            else _spawnBtn.isEnabled = false;
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
