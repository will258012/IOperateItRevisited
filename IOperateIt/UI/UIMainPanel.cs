using ColossalFramework.UI;
using RoadNamer.Panels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using IOperateIt.Manager;
using IOperateIt.Tools;
using IOperateIt.Utils;
using UnityEngine;

namespace IOperateIt.UI
{
    public class UIMainPanel : UICustomControl
    {
        GameObject mVehicleInfoListObject;
        VehicleInfoList mVehicleInfoList;
        RoadSelectTool mRoadSelectTool;
        UIButton vehicleSelectorBtn;
        UIButton spawnMeshBtn;
        public UIMainPanel()
        {
            UIView uiView = UIView.GetAView();
            vehicleSelectorBtn = (UIButton)uiView.AddUIComponent(typeof(UIButton));

            mVehicleInfoListObject = new GameObject("RoadNamePanel");
            mVehicleInfoList = mVehicleInfoListObject.AddComponent<VehicleInfoList>();
            mVehicleInfoList.transform.parent = uiView.transform;
            mVehicleInfoList.Hide();

            vehicleSelectorBtn.text = "Vehicle Selector";
            vehicleSelectorBtn.width = 150;
            vehicleSelectorBtn.height = 30;
            vehicleSelectorBtn.normalBgSprite = "ButtonMenu";
            vehicleSelectorBtn.disabledBgSprite = "ButtonMenuDisabled";
            vehicleSelectorBtn.hoveredBgSprite = "ButtonMenuHovered";
            vehicleSelectorBtn.focusedBgSprite = "ButtonMenuFocused";
            vehicleSelectorBtn.pressedBgSprite = "ButtonMenuPressed";
            vehicleSelectorBtn.textColor = new Color32(255, 255, 255, 255);
            vehicleSelectorBtn.disabledTextColor = new Color32(7, 7, 7, 255);
            vehicleSelectorBtn.hoveredTextColor = new Color32(7, 132, 255, 255);
            vehicleSelectorBtn.focusedTextColor = new Color32(255, 255, 255, 255);
            vehicleSelectorBtn.pressedTextColor = new Color32(30, 30, 44, 255);
            vehicleSelectorBtn.eventClick += VehicleSelectorBtn_eventClick;
            vehicleSelectorBtn.relativePosition = new Vector3(330f, 20f);

            spawnMeshBtn = (UIButton)uiView.AddUIComponent(typeof(UIButton));
            spawnMeshBtn.text = "Spawn Vehicle";
            spawnMeshBtn.width = 150;
            spawnMeshBtn.height = 30;
            spawnMeshBtn.normalBgSprite = "ButtonMenu";
            spawnMeshBtn.disabledBgSprite = "ButtonMenuDisabled";
            spawnMeshBtn.hoveredBgSprite = "ButtonMenuHovered";
            spawnMeshBtn.focusedBgSprite = "ButtonMenuFocused";
            spawnMeshBtn.textColor = new Color32(255, 255, 255, 255);
            spawnMeshBtn.disabledTextColor = new Color32(7, 7, 7, 255);
            spawnMeshBtn.hoveredTextColor = new Color32(7, 132, 255, 255);
            spawnMeshBtn.focusedTextColor = new Color32(255, 255, 255, 255);
            spawnMeshBtn.eventClick += SpawnMeshBtn_eventClick;
            spawnMeshBtn.relativePosition = new Vector3(330f, 60f);

            EventBusManager.Instance().Subscribe("closeVehiclePanel", mVehicleInfoList);
            EventBusManager.Instance().Subscribe("closeAll", mVehicleInfoList);
        }

        private void SpawnMeshBtn_eventClick(UIComponent component, UIMouseEventParameter eventParam)
        {
           if( mRoadSelectTool == null)
            {
                if( !ToolsModifierControl.toolController.gameObject.GetComponent<RoadSelectTool>())
                {
                    ToolsModifierControl.toolController.gameObject.AddComponent<RoadSelectTool>();
                }
                mRoadSelectTool = ToolsModifierControl.toolController.gameObject.GetComponent<RoadSelectTool>();
                ToolsModifierControl.toolController.CurrentTool = mRoadSelectTool;
                ToolsModifierControl.SetTool<RoadSelectTool>();
            }
            else
            {
                ToolsModifierControl.toolController.CurrentTool = ToolsModifierControl.GetTool<DefaultTool>();
                ToolsModifierControl.SetTool<DefaultTool>();
                UnityEngine.Object.Destroy(mRoadSelectTool);
                mRoadSelectTool = null;
            }
        }

        private void VehicleSelectorBtn_eventClick(UIComponent component, UIMouseEventParameter eventParam)
        {
            if (mVehicleInfoList.isVisible)
            {
                mVehicleInfoList.isVisible = false;
                mVehicleInfoList.Hide();
            }
            else
            {
                mVehicleInfoList.vehicleList.DisplayAt(0);
                mVehicleInfoList.isVisible = true;
                mVehicleInfoList.Show();
            }
            
        }

        private void EnableTool()
        {
            if (mRoadSelectTool == null)
            {

            }
        }
    }
}
