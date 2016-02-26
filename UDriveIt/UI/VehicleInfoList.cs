using ColossalFramework.UI;
using RoadNamer.CustomUI;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace RoadNamer.Panels
{
    public class VehicleInfoList : UIPanel
    {
        protected RectOffset m_UIPadding = new RectOffset(5, 5, 5, 5);

        private int titleOffset = 40;
        private UITitleBar m_panelTitle;
        public UIFastList vehicleList = null;

        private Vector2 offset = Vector2.zero;

        public override void Awake()
        {
            this.isInteractive = true;
            this.enabled = true;
            this.width = 350;
            this.height = 300;

            base.Awake();
        }

        public override void Start()
        {
            base.Start();

            m_panelTitle = this.AddUIComponent<UITitleBar>();
            m_panelTitle.title = "Vehicle Types";
            //m_panelTitle.iconAtlas = SpriteUtilities.GetAtlas("RoadNamerIcons");
            //m_panelTitle.iconSprite = "ToolbarFGIcon";
            m_panelTitle.m_closeActions.Add("closeAll");

            CreatePanelComponents();

            this.relativePosition = new Vector3(Mathf.Floor((GetUIView().fixedWidth - width) / 2) + width, Mathf.Floor((GetUIView().fixedHeight - height) / 2 ) - height - m_UIPadding.top);
            this.backgroundSprite = "MenuPanel2";
            this.atlas = CustomUI.UIUtils.GetAtlas("Ingame");
        }

        private void CreatePanelComponents()
        {
            vehicleList = UIFastList.Create<VehicleInfoListEntry>(this);
            vehicleList.size = new Vector2(this.width-m_UIPadding.left-m_UIPadding.right, (this.height-titleOffset- m_UIPadding.top-m_UIPadding.bottom));
            vehicleList.canSelect = false;
            vehicleList.relativePosition = new Vector2(m_UIPadding.left, titleOffset + m_UIPadding.top);
            vehicleList.rowHeight = 40f;
            vehicleList.rowsData.Clear();
            vehicleList.selectedIndex = -1;

            RefreshList();
        }

        public void RefreshList()
        {
            vehicleList.rowsData.Clear();
            foreach (VehicleCollection vehicleCollection in FindObjectsOfType<VehicleCollection>())
            {
                if( vehicleCollection.name.Equals("Residential Low"))
                {
                    foreach (VehicleInfo info in vehicleCollection.m_prefabs)
                    {
                        vehicleList.rowsData.Add(info);
                    }
                }
            }
            vehicleList.DisplayAt(0);
            vehicleList.selectedIndex = 0;
        }

        public void onReceiveEvent(string eventName, object eventData)
        {
            string message = eventData as string;
            switch (eventName)
            {
                case "forceupdateroadnames":
                    RefreshList();
                    break;
                case "closeUsedNamePanel":
                    Hide();
                    break;
                case "closeAll":
                    Hide();
                    break;
                default:
                    break;
            }
        }
    }

 
}
