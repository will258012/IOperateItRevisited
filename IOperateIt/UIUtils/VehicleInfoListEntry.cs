using UnityEngine;
using ColossalFramework.UI;
using IOperateIt.Manager;
using System.Text.RegularExpressions;

namespace RoadNamer.CustomUI
{
     public class VehicleInfoListEntry : UIPanel, IUIFastListRow
    {
        private UIPanel background;
        private UILabel label;
        uint vehicleInfoIndex;
        private VehicleInfo vehicleInfo;
        private string vehicleName;
        private string workshopNamePattern = @"\d{9}\.(.{0,200})_Data";
        private Regex workshopRegex;

        public override void Start()
        {
            base.Start();
            workshopRegex = new Regex(workshopNamePattern);
            isVisible = true;
            canFocus = true;
            isInteractive = true;
            width = parent.width;
            height = 40;

            background = AddUIComponent<UIPanel>();
            background.width = width;
            background.height = 40;
            background.relativePosition = Vector2.zero;
            background.zOrder = 0;

            label = this.AddUIComponent<UILabel>();
            label.textScale = 1f;
            label.size = new Vector2(width, height);
            label.textColor = new Color32(180, 180, 180, 255);
            label.relativePosition = new Vector2(0,height*0.25f);
            label.textAlignment = UIHorizontalAlignment.Left;
        }

        protected override void OnMouseDown(UIMouseEventParameter p)
        {
            base.OnMouseDown(p);
            EventBusManager.Instance().Publish("closeVehiclePanel", null);

            VehicleHolder.getInstance().setVehicleInfo(vehicleInfo);
        }
        
        public void Display(object data, bool isRowOdd)
        {
            if (data != null)
            {
                vehicleInfoIndex = (uint)(data);
                vehicleInfo = PrefabCollection<VehicleInfo>.GetPrefab(vehicleInfoIndex);
                vehicleName = PrefabCollection<VehicleInfo>.PrefabName(vehicleInfoIndex);

                if (vehicleInfo != null && background != null)
                {
                    Match m = workshopRegex.Match(vehicleName);
                    if ( m.Success )
                    {

                        label.text = m.Groups[1].ToString().Replace("_"," ");
                    }
                    else
                    {
                        label.text = vehicleName;
                    }

                    if (isRowOdd)
                    {
                        background.backgroundSprite = "UnlockingItemBackground";
                        background.color = new Color32(0, 0, 0, 128);
                    }
                    else
                    {
                        background.backgroundSprite = null;
                    }
                }
            }
        }

        public void Select(bool isRowOdd)
        {
            if (background != null)
            {
                /*background.backgroundSprite = "ListItemHighlight";
                background.color = new Color32(255, 255, 255, 255);*/
            }
        }

        public void Deselect(bool isRowOdd)
        {
            if (background != null)
            {
                if (isRowOdd)
                {
                    background.backgroundSprite = "UnlockingItemBackground";
                    background.color = new Color32(0, 0, 0, 128);
                }
                else
                {
                    background.backgroundSprite = null;
                }
            }
        }

    }
}
