using ColossalFramework;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using UDriveIt.Manager;
using UDriveIt.Utils;
using UnityEngine;

namespace UDriveIt.Tools
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
                                VehicleHolder.getInstance().setActive(netSegment.m_middlePosition);
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

        private void spawnVehicle(Vector3 position)
        {
            NetManager manager = Singleton<NetManager>.instance;
            ushort[] closeSegments = new ushort[10];
            int closeSegmentCount;
            float minDistance = float.PositiveInfinity;
            ushort segmentId = 0;
            float distance = float.PositiveInfinity;

            var watch = Stopwatch.StartNew();
            manager.GetClosestSegments(position, closeSegments, out closeSegmentCount);
            for (int i = 0; i < closeSegmentCount; i++)
            {
                distance = Vector3.Distance(position, manager.m_segments.m_buffer[closeSegments[i]].m_middlePosition);
                //LoggerUtils.LogToConsole(string.Format("Mindistance: {0}, distance:{1}", minDistance, distance));
                if (distance < minDistance)
                {
                    minDistance = distance;
                    segmentId = closeSegments[i];
                }
            }
            watch.Stop();

     
            LoggerUtils.LogToConsole(String.Format("Time taken:{0}", watch.ElapsedMilliseconds));
            /*for ( int i =0; i< closeSegmentCount; i++)
            {
                LoggerUtils.LogToConsole(string.Format("{0}:{1}", id, closeSegments[i]));
            }*/
            LoggerUtils.LogToConsole(string.Format("Closest Id:{0}", segmentId));
            NetSegment netSegment = manager.m_segments.m_buffer[(int)segmentId];

            VehicleInfo info = VehicleHolder.getInstance().getVehicleInfo();
            GameObject newVehicle = new GameObject("UDIVehicle");
            newVehicle.transform.parent = (Transform)null;
            newVehicle.transform.position = netSegment.m_middlePosition;
            newVehicle.transform.eulerAngles = netSegment.m_endDirection - netSegment.m_startDirection;
            newVehicle.AddComponent<MeshFilter>().mesh = info.m_mesh;
            newVehicle.AddComponent<MeshRenderer>().material = info.m_material;
            /*(this.rigidbody = this.aircraft.AddComponent<Rigidbody>();
            this.rigidbody.useGravity = false;
            this.rigidbody.mass = 300000f;
            this.rigidbody.angularDrag = 1.5f;
            this.rigidbody.drag = 0.05f;
            this.rigidbody.inertiaTensor = new Vector3(1f, 1f, 1f);
            this.rigidbody.velocity = this.aircraft.transform.forward * 50f;*/
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
