extern alias FPC;
using AlgernonCommons.Translation;
using AlgernonCommons.UI;
using AlgernonCommons.Utils;
using ColossalFramework;
using ColossalFramework.PlatformServices;
using ColossalFramework.UI;
using IOperateIt.Settings;
using IOperateIt.Utils;
using System.Collections.Generic;
using System.Linq;
using UnifiedUI.GUI;
using UnityEngine;

namespace IOperateIt.UI
{
    public class MainPanel : MonoBehaviour
    {

        public static MainPanel Instance { get; private set; }
        public UIPanel Panel { get; set; }
        public UIButton GetMainButton() => mainBtn ?? UUISupport.UUIButton as UIButton;
        /// <summary>
        /// Gets or sets the panel's last saved position.
        /// </summary>
        public static Vector3 SavedPanelPosition { get; set; } = DefaultPosition;
        /// <summary>
        /// Gets or sets the button's last saved position.
        /// </summary>
        public static Vector3 SavedButtonPosition { get; set; } = DefaultPosition;

        public static Vector3 DefaultPosition => Vector3.left;

        public int ColorIndex
        {
            get;
            set => field = (int)Mathf.Repeat(value, 4);
        }

        private const float Margin = 10f;
        private const float VehicleRowHeight = 40f;

        private const float CloseButtonSize = 35f;
        private const float MainButtonSize = 40f;
        private const float TitleHeight = 13f;

        private UIButton mainBtn;
        private UIButton modSettingsBtn;
        private UIButton spawnBtn;
        internal UIList vehicleList;
        private PreviewPanel previewPanel;
        private UILabel noResultsLabel;
        private UIButton colorChangeBtn;
        internal readonly Dictionary<uint, VehicleInfo> prefabData = new();
        private readonly FastList<object> originalData = new();
        private readonly FastList<object> filteredData = new();
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
            vehicleList = UIList.AddUIList<MainPanelRow>(Panel, Margin, currentY, 400f, 320f, VehicleRowHeight);
            var vehicleInfos = new FastList<object>();

            for (uint i = 0; i < PrefabCollection<VehicleInfo>.PrefabCount(); i++)
            {
                var vehicleInfo = PrefabCollection<VehicleInfo>.GetPrefab(i);

                if (vehicleInfo != null && !vehicleInfo.name.ToLower().Contains("trailer"))
                {
                    vehicleInfos.Add(i);
                    originalData.Add(i);
                    prefabData.Add(i, PrefabCollection<VehicleInfo>.GetPrefab(i));
                }
            }
            vehicleList.Data = vehicleInfos;

            vehicleList.EventSelectionChanged += (component, obj) =>
            {
                if (Panel.isVisible)
                {
                    if (obj is uint index)
                        UpdateListEvent(index);
                    else OnClearSelection();
                }
            };

            Panel.eventVisibilityChanged += (component, vis) =>
            {
                if (vis == true)
                {
                    if (vehicleList.SelectedIndex >= 0)
                        UpdateListEvent((uint)vehicleList.SelectedItem);
                    else OnClearSelection();
                }
                else
                {
                    SavedPanelPosition = Panel.absolutePosition;
                    ModSettings.Save();
                }
            };
            noResultsLabel = UILabels.AddLabel(Panel, vehicleList.relativePosition.x, vehicleList.relativePosition.y + vehicleList.height * 0.5f, Translations.Translate("NO_RESULTS"), width: vehicleList.width, alignment: UIHorizontalAlignment.Center);
            noResultsLabel.isVisible = false;

            previewPanel = Panel.AddUIComponent<PreviewPanel>();
            previewPanel.relativePosition = UILayout.PositionRightOf(vehicleList);
            previewPanel.enabled = false;

            currentY += vehicleList.height + Margin;

            spawnBtn = UIButtons.AddButton(Panel, (vehicleList.width - 200f) / 2f, currentY, Translations.Translate("SPAWNBTN_TEXT"), 200f, 40f);
            spawnBtn.isEnabled = false;
            spawnBtn.playAudioEvents = true;
            spawnBtn.eventClick += SpawnBtnClickEvent;

            modSettingsBtn = UIButtons.AddButton(Panel, previewPanel.relativePosition.x + (previewPanel.width - 200f) / 2f, currentY, Translations.Translate("MODSETTINGSBTN_TEXT"));
            modSettingsBtn.eventClick += (_, e) => FPC.FPSCamera.UI.MainPanel.OpenSettingsPanel(Mod.Instance.Name);

            colorChangeBtn = Panel.AddUIComponent<UIButton>();
            DriveButtons.SetButtonProperties(colorChangeBtn);
            colorChangeBtn.relativePosition = new Vector3(previewPanel.relativePosition.x, currentY);
            colorChangeBtn.name = name + nameof(colorChangeBtn);
            colorChangeBtn.tooltip = Translations.Translate("COLORCHANGEBTN_TOOLTIP");
            colorChangeBtn.pressedBgSprite = "ToolbarIconZoningPressed";
            colorChangeBtn.normalBgSprite = "ToolbarIconZoning";
            colorChangeBtn.hoveredBgSprite = "ToolbarIconZoningHovered";
            colorChangeBtn.disabledBgSprite = "ToolbarIconZoningDisabled";
            colorChangeBtn.isEnabled = false;
            colorChangeBtn.playAudioEvents = true;
            colorChangeBtn.eventClick += OnColorChangeClick;

            Panel.height = currentY + spawnBtn.height + Margin;

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

                // Search Bar
                var searchBar = SearchBar.Add(Panel, Margin, 2, tooltip: Translations.Translate("SEARCHBARBTN_TOOLTIP"), width: Panel.width);
                searchBar.OnAnimating += (value) =>
                {
                    titleLabel.opacity = 1f - value;
                };
                searchBar.OnSearchStarted += (value) =>
                {
                    filteredData.Clear();
                    if (ulong.TryParse(value, out ulong targetId) && targetId <= 9999999999)// workshop id
                    {
                        foreach (var kvp in prefabData)
                        {
                            if (!PrefabUtils.IsWorkshopAsset(kvp.Value)) continue;
                            if (kvp.Value.name.Contains(targetId.ToString()))
                                filteredData.Add(kvp.Key);
                        }
                    }
                    else // text search
                    {
                        char[] chars = value.Where(c => !char.IsWhiteSpace(c)).Distinct().ToArray();
                        if (chars.Length == 0) return;

                        foreach (var kvp in prefabData)
                        {
                            string title = kvp.Value.GetUncheckedLocalizedTitle();
                            string name = kvp.Value.name;
                            bool allMatch = true;

                            foreach (var kw in chars)
                            {
                                if (!title.ToLower().Contains(kw.ToString().ToLower()) && !name.ToLower().Contains(kw.ToString().ToLower()))
                                {
                                    allMatch = false;
                                    break;
                                }
                            }

                            if (allMatch)
                            {
                                filteredData.Add(kvp.Key);
                            }
                        }
                    }
                    if (filteredData.m_size == 0)
                        noResultsLabel.isVisible = true;
                    vehicleList.Data = filteredData;
                };

                searchBar.OnClearResults += () =>
                {
                    vehicleList.Data = originalData;
                    filteredData.Clear();
                    foreach (var item in originalData)
                        filteredData.Add(item);
                    noResultsLabel.isVisible = false;
                };
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
            mainBtn = UIView.GetAView().AddUIComponent(typeof(UIButton)) as UIButton;
            DriveButtons.SetButtonProperties(mainBtn);
            mainBtn.name = "MainButton";
            mainBtn.tooltip = Translations.Translate("MAINPANELBTN_TOOLTIP");
            mainBtn.absolutePosition = new Vector3(x, y);
            mainBtn.size = new Vector2(MainButtonSize, MainButtonSize);

            mainBtn.atlas = DriveButtonAtlas.Atlas;
            mainBtn.pressedBgSprite = DriveButtonAtlas.BgPressed;
            mainBtn.normalBgSprite = DriveButtonAtlas.Bg;
            mainBtn.hoveredBgSprite = DriveButtonAtlas.BgHovered;
            mainBtn.disabledBgSprite = DriveButtonAtlas.BgDisabled;
            mainBtn.normalFgSprite = DriveButtonAtlas.Fg;
            mainBtn.eventClick += (_, m) =>
            {
                if (!Panel.isVisible) LoadPanelPosition();

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
            mainBtn_drag.eventMouseUp += (_, p) => { SavedButtonPosition = mainBtn.absolutePosition; ModSettings.Save(); };
            #endregion
        }

        private void OnClearSelection()
        {
            spawnBtn?.isEnabled = false;
            colorChangeBtn?.isEnabled = false;
            previewPanel.enabled = false;
        }

        private void OnDestory()
        {
            spawnBtn.eventClick -= SpawnBtnClickEvent;
            prefabData.Clear();
            originalData.Clear();
            filteredData.Clear();
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
                Color color = default;
                switch (ColorIndex)
                {
                    case 0: color = selectedVehicle.m_color0; break;
                    case 1: color = selectedVehicle.m_color1; break;
                    case 2: color = selectedVehicle.m_color2; break;
                    case 3: color = selectedVehicle.m_color3; break;
                }

                color.a = 0;
                previewPanel.enabled = true;
                previewPanel.SetTarget(selectedVehicle, color, true);
                DriveController.Instance.UpdateColor(color, true);
                DriveController.Instance.UpdateVehicleInfo(selectedVehicle);

                spawnBtn.isEnabled = true;
                colorChangeBtn.isEnabled = true;
            }
            else OnClearSelection();
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
            else spawnBtn.isEnabled = false;
        }
        private void OnColorChangeClick(UIComponent component, UIMouseEventParameter p)
        {
            if (vehicleList.SelectedIndex < 0) return;
            ColorIndex++;
            UpdateListEvent((uint)vehicleList.SelectedItem);
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
            private UILabel vehicleNameLabel;

            // Preview image.
            private UISprite vehicleSprite;

            // Steam icon.
            private UISprite steamSprite;

            /// <summary>
            /// Vehicle prefab.
            /// </summary>
            private VehicleInfo info;

            private ulong workshopId;
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
                info = Instance.prefabData[(uint)data];
                // Perform initial setup for new rows.
                if (vehicleNameLabel == null)
                {
                    // Add object name label.
                    vehicleNameLabel = AddLabel(VehicleSpriteSize + Margin, width - Margin - VehicleSpriteSize - Margin - SteamSpriteWidth - ScrollMargin - Margin, wordWrap: true);

                    // Add preview sprite image.
                    vehicleSprite = AddUIComponent<UISprite>();
                    vehicleSprite.height = VehicleSpriteSize;
                    vehicleSprite.width = VehicleSpriteSize;
                    vehicleSprite.relativePosition = Vector2.zero;

                    // Add setam sprite.
                    steamSprite = AddUIComponent<UISprite>();
                    steamSprite.width = SteamSpriteWidth;
                    steamSprite.height = SteamSpriteHeight;
                    steamSprite.atlas = UITextures.InGameAtlas;
                    steamSprite.spriteName = "SteamWorkshop";
                    steamSprite.relativePosition = new Vector2(width - Margin - ScrollMargin - SteamSpriteWidth, (height - SteamSpriteHeight) / 2f);
                }
                vehicleNameLabel.text = info.GetUncheckedLocalizedTitle();
                vehicleNameLabel.tooltip = info.name.SplitUppercase() != info.GetUncheckedLocalizedTitle() ? info.name : null;
                vehicleSprite.atlas = info?.m_Atlas;
                vehicleSprite.spriteName = info?.m_Thumbnail;

                steamSprite.isVisible = PrefabUtils.IsWorkshopAsset(info);
                if (steamSprite.isVisible)
                {
                    steamSprite.tooltip = Translations.Translate("STEAMSPRITE_TOOLTIP");
                    if (ulong.TryParse(info.name.Split('.').FirstOrDefault(), out ulong workshopId))
                    {
                        this.workshopId = workshopId;
                        steamSprite.eventClick -= OnSteamSpriteClick;
                        steamSprite.eventClick += OnSteamSpriteClick;
                    }
                }

                // Set initial background as deselected state.
                Deselect(rowIndex);
            }
            private void OnSteamSpriteClick(UIComponent component, UIMouseEventParameter p)
            {
                p.Use();
                if (PlatformService.IsOverlayEnabled())
                    PlatformService.ActivateGameOverlayToWorkshopItem(new PublishedFileId(workshopId));
                else
                    Application.OpenURL($"https://steamcommunity.com/sharedfiles/filedetails/?id={workshopId}");
            }
        }
    }
}
