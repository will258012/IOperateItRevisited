using ColossalFramework;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using IOperateIt.Manager;
using IOperateIt.Utils;
using UnityEngine;

namespace IOperateIt.Tools
{
    class RoadSelectTool: DefaultTool
    {
        protected override void Awake()
        {
            base.Awake();
        }

        protected override void OnToolGUI()
        {
            base.OnToolGUI();
        }

        protected override void OnEnable()
        {
            base.OnEnable();
        }

        protected override void OnDisable()
        {
            base.OnDisable();

        }

        protected override void OnToolUpdate()
        {
            if (m_toolController != null && !m_toolController.IsInsideUI && Cursor.visible)
            {
                RaycastOutput raycastOutput;

                if (RaycastRoad(out raycastOutput))
                {
                    ushort netSegmentId = raycastOutput.m_netSegment;

                    if (netSegmentId != 0)
                    {           

                        NetManager netManager = Singleton<NetManager>.instance;
                        NetSegment netSegment = netManager.m_segments.m_buffer[(int)netSegmentId];

                        if (netSegment.m_flags.IsFlagSet(NetSegment.Flags.Created))
                        {
                            if (Event.current.type == EventType.MouseDown /*&& Event.current.button == (int)UIMouseButton.Left*/)
                            {
                                LoggerUtils.Log(string.Format("Closest Id:{0}", netSegmentId));
                                ShowToolInfo(false, null, new Vector3());
                                VehicleInfo info = VehicleHolder.getInstance().getVehicleInfo();
                                VehicleHolder.getInstance().setActive(netSegment.m_middlePosition,Vector3.zero);

                                //unset self as tool
                                ToolsModifierControl.toolController.CurrentTool = ToolsModifierControl.GetTool<DefaultTool>();
                                ToolsModifierControl.SetTool<DefaultTool>();
                                UnityEngine.Object.Destroy(this);
                            }
                            else
                            {
                                ShowToolInfo(true, "Spawn Vehicle mesh", netSegment.m_bounds.center);
                            }
                        }
                    }
                }
            }
            else
            {
                ShowToolInfo(false, null, new Vector3());
            }
        }

        bool RaycastRoad(out RaycastOutput raycastOutput)
        {
            RaycastInput raycastInput = new RaycastInput(Camera.main.ScreenPointToRay(Input.mousePosition), Camera.main.farClipPlane);
            raycastInput.m_netService.m_service = ItemClass.Service.Road;
            raycastInput.m_netService.m_itemLayers = ItemClass.Layer.Default | ItemClass.Layer.MetroTunnels;
            raycastInput.m_ignoreSegmentFlags = NetSegment.Flags.None;
            raycastInput.m_ignoreNodeFlags = NetNode.Flags.None;
            raycastInput.m_ignoreTerrain = true;

            return RayCast(raycastInput, out raycastOutput);
        }
    }
}
