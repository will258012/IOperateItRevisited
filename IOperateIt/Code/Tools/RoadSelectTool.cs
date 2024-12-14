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
            if (!m_toolController.IsInsideUI && Cursor.visible)
            {
                if (RaycastRoad(out var raycastOutput))
                {
                    ushort netSegmentId = raycastOutput.m_netSegment;

                    if (netSegmentId != 0)
                    {
                        var netSegment = NetManager.instance.m_segments.m_buffer[netSegmentId];

                        if (netSegment.m_flags.IsFlagSet(NetSegment.Flags.Created))
                        {
                            var color = GetToolColor(false, false);
                            NetTool.RenderOverlay(cameraInfo, ref netSegment, color, color);
                        }
                    }
                }
            }
        }


        protected override void OnToolGUI(Event e)
        {
            if (!m_toolController.IsInsideUI && Cursor.visible)
            {
                if (RaycastRoad(out var raycastOutput))
                {
                    ushort netSegmentId = raycastOutput.m_netSegment;

                    if (netSegmentId != 0)
                    {
                        var netSegment = NetManager.instance.m_segments.m_buffer[netSegmentId];

                        if (netSegment.m_flags.IsFlagSet(NetSegment.Flags.Created))
                        {
                            if (e.type == EventType.MouseDown)
                            {
                                netSegment.GetClosestPositionAndDirection(netSegment.m_middlePosition, out _, out var dir);
                                var rotation = Quaternion.LookRotation(dir);
                                DriveController.Instance.StartDriving(netSegment.m_middlePosition, rotation);
                                ShowToolInfo(false, null, Vector3.zero);

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

        private bool RaycastRoad(out RaycastOutput raycastOutput)
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
