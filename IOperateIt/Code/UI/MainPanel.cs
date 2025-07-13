extern alias FPC;
using AlgernonCommons.Translation;
using AlgernonCommons.UI;
using AlgernonCommons.Utils;
using ColossalFramework.UI;
using IOperateIt.Utils;
using IOperateIt.Settings;
using UnifiedUI.GUI;
using UnityEngine;

namespace IOperateIt.UI
{
    public class MainPanel : MonoBehaviour
    {

        public static MainPanel instance { get; private set; }
        public UIPanel Panel { get; set; }
        public UIButton GetMainButton() => _mainBtn ?? UUISupport.UUIButton as UIButton;

        private const float Margin = 10f;
        private const float VehicleRowHeight = 40f;

        private UIButton _mainBtn;
        private UIButton _modSettingsBtn;
        private UIButton _spawnBtn;
        internal UIList _vehicleList;
        private PreviewPanel _previewPanel;

        private void Awake()
        {
            instance = this;
            #region Main Panel
            Panel = UIView.GetAView().AddUIComponent(typeof(UIPanel)) as UIPanel;
            Panel.autoLayout = false;
            Panel.canFocus = true;
            Panel.isInteractive = true;
            Panel.atlas = UITextures.InGameAtlas;
            Panel.backgroundSprite = "UnlockingPanel2";
            Panel.width = 800f;

            var currentY = Margin;
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
                if ((bool)vis == true && _vehicleList.SelectedIndex >= 0)
                {
                    UpdateListEvent((uint)_vehicleList.SelectedItem);
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
            Panel.Hide();

            if (FPC.FPSCamera.Utils.ModSupport.FoundUUI)
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
            _mainBtn = UIView.GetAView().AddUIComponent(typeof(UIButton)) as UIButton;
            _mainBtn.name = "MainButton";
            _mainBtn.tooltip = Translations.Translate("MAINPANELBTN_TOOLTIP");
            _mainBtn.absolutePosition = new Vector3(x, y);
            _mainBtn.size = new Vector2(40f, 40f);
            _mainBtn.scaleFactor = .8f;
            _mainBtn.pressedBgSprite = "OptionBasePressed";
            _mainBtn.normalBgSprite = "OptionBase";
            _mainBtn.hoveredBgSprite = "OptionBaseHovered";
            _mainBtn.disabledBgSprite = "OptionBaseDisabled";
            _mainBtn.normalFgSprite = "InfoIconTrafficCongestion";
            _mainBtn.textColor = new Color32(255, 255, 255, 255);
            _mainBtn.disabledTextColor = new Color32(7, 7, 7, 255);
            _mainBtn.hoveredTextColor = new Color32(255, 255, 255, 255);
            _mainBtn.focusedTextColor = new Color32(255, 255, 255, 255);
            _mainBtn.pressedTextColor = new Color32(30, 30, 44, 255);
            _mainBtn.eventClick += (_, m) =>
            {
                Panel.absolutePosition = new Vector3(_mainBtn.absolutePosition.x +
                    (_mainBtn.absolutePosition.x < Screen.height / 2f ? _mainBtn.width - 10f : -Panel.width + 10f),
                                                           _mainBtn.absolutePosition.y +
                (_mainBtn.absolutePosition.y < Screen.height / 2f ? _mainBtn.height - 15f : -Panel.height + 15f));
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
            mainBtn_drag.eventMouseUp += (_, p) => { ModSettings.MainButtonPos = _mainBtn.absolutePosition; ModSettings.Save(); };
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
                    DriveController.instance.UpdateColor(adjustedColor, true);
                }
                else
                {
                    _previewPanel.SetTarget(selectedVehicle);
                    DriveController.instance.UpdateColor(default, false);
                }
                DriveController.instance.UpdateVehicleInfo(selectedVehicle);
                _spawnBtn.isEnabled = true;
            }
        }
        private void SpawnBtnClickEvent(UIComponent component, UIMouseEventParameter eventParam)
        {
            if (DriveController.instance.IsVehicleInfoSet())
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
