using AlgernonCommons.Translation;
using ColossalFramework;
using UnityEngine;

namespace IOperateIt.Tools
{
    public class RoadSelectTool : ToolBase
    {
        public override void RenderOverlay(RenderManager.CameraInfo cameraInfo)
        {
            base.RenderOverlay(cameraInfo);

            if (m_toolController != null && !m_toolController.IsInsideUI && Cursor.visible)
            {
                RaycastOutput raycastOutput;

                if (RaycastRoad(out raycastOutput))
                {
                    ushort netSegmentId = raycastOutput.m_netSegment;

                    if (netSegmentId != 0)
                    {
                        var netManager = Singleton<NetManager>.instance;
                        var netSegment = netManager.m_segments.m_buffer[netSegmentId];

                        if (netSegment.m_flags.IsFlagSet(NetSegment.Flags.Created))
                        {
                            var color = GetToolColor(warning: false, false);
                            var color2 = color;
                            NetTool.RenderOverlay(cameraInfo, ref netSegment, color, color2);
                        }
                    }
                }
            }
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

                        var netManager = Singleton<NetManager>.instance;
                        var netSegment = netManager.m_segments.m_buffer[netSegmentId];

                        if (netSegment.m_flags.IsFlagSet(NetSegment.Flags.Created))
                        {
                            if (Event.current.type == EventType.MouseDown && Event.current.button == 0)
                            {
                                DriveController.Instance.StartDriving(netSegment.m_middlePosition, Quaternion.identity);
                                ShowToolInfo(false, null, new Vector3());

                                //unset self as tool
                                ToolsModifierControl.toolController.CurrentTool = ToolsModifierControl.GetTool<DefaultTool>();
                                ToolsModifierControl.SetTool<DefaultTool>();
                                Destroy(this);
                            }
                            else
                            {
                                ShowToolInfo(true, Translations.Translate("ROAD_SELECT"), netSegment.m_bounds.center);
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
