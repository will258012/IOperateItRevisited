using ColossalFramework;
using UnityEngine;

namespace IOperateIt.Utils
{
    public class MapUtils : ToolBase
    {
        public const string LAYER_UNDERGROUND_NAME = "MetroTunnels";
        public const string LAYER_BUILDINGS_NAME = "Buildings";
        public const string LAYER_VEHICLES_NAME = "Vehicles";
        public const string LAYER_CITIZENS_NAME = "Citizens";
        public const string LAYER_IGNORE_NAME = "Ignore";
        public const string LAYER_ROAD_NAME = "Road";

        public static readonly int LAYER_UNDERGROUND = LayerMask.NameToLayer(LAYER_UNDERGROUND_NAME); // underground render layer.
        public static readonly int LAYER_BUILDINGS = LayerMask.NameToLayer(LAYER_BUILDINGS_NAME); // building render layer.
        public static readonly int LAYER_VEHICLES = LayerMask.NameToLayer(LAYER_VEHICLES_NAME); // vehicle render layer.
        public static readonly int LAYER_CITIZENS = LayerMask.NameToLayer(LAYER_CITIZENS_NAME); // pedestrian render layer.
        public static readonly int LAYER_IGNORE = LayerMask.NameToLayer(LAYER_IGNORE_NAME); // ignore render layer.
        public static readonly int LAYER_ROAD = LayerMask.NameToLayer(LAYER_ROAD_NAME); // road render layer.

        public enum CollisionTypes
        {
            Ground = 0,
            Water = 1,
            Collider = 2,
            Road = 3,
        }

        private const float ROAD_RAYCAST_UPPER = 1.5f;
        private const float ROAD_RAYCAST_LOWER = -7.5f;
        private const float ROAD_VALID_LANE_DIST_MULT = 1.25f;
        private const float ROAD_VALID_LANE_H_DIFF = 0.75f;
        public static bool RayCast(RaycastInput rayCastInput, out RaycastOutput result)
        {
            result = default;
            // Perform a single raycast if no offset is provided
            if (ToolBase.RayCast(rayCastInput, out var result2))
            {
                result = result2;
                return true;
            }
            else
            {
                return false;
            }
        }

        public static RaycastInput GetRaycastInput(Vector3 position)
            => GetRaycastInput(position, -100f, 100f);

        public static RaycastInput GetRaycastInput(Vector3 position, float min, float max)
            => GetRaycastInput(position + Vector3.up * max, Vector3.down, max - min);

        public static RaycastInput GetRaycastInput(Vector3 position, Vector3 dir, float dist, bool ignoreTerrain = true)
        {
            var input = new RaycastInput(new Ray(position, dir), dist);
            input.m_ignoreTerrain = ignoreTerrain;
            return input;
        }


        public static float CalculateHeight(Vector3 position, float objectHeight, out CollisionTypes collisionType, bool ignoreColliders = false)
        {
            ushort segmentId = 0;
            RaycastInput input;
            RaycastOutput output;
            Vector3 roadPos;

            collisionType = CollisionTypes.Ground;

            float height = Singleton<TerrainManager>.instance.SampleDetailHeightSmooth(position);
            float tmpHeight = Singleton<TerrainManager>.instance.WaterLevel(new Vector2(position.x, position.z));

            if (tmpHeight > height)
            {
                collisionType = CollisionTypes.Water;
                height = tmpHeight;
            }

            if (!ignoreColliders
                && Physics.Raycast(position + Vector3.up * objectHeight, Vector3.down, out var hitInfo, objectHeight - ROAD_RAYCAST_LOWER, LayerMask.GetMask(MapUtils.LAYER_VEHICLES_NAME, MapUtils.LAYER_BUILDINGS_NAME))
                && hitInfo.point.y > height)
            {
                collisionType = CollisionTypes.Collider;
                height = hitInfo.point.y;
            }

            input = GetRaycastInput(position, ROAD_RAYCAST_LOWER, objectHeight + ROAD_RAYCAST_UPPER); // Configure raycast input parameters.
            input.m_ignoreTerrain = true;
            input.m_ignoreSegmentFlags = NetSegment.Flags.Deleted |
                                         NetSegment.Flags.Collapsed |
                                         NetSegment.Flags.Flooded;

            input.m_netService.m_service = ItemClass.Service.Road;  // General roads
            input.m_netService.m_itemLayers = ItemClass.Layer.Default |
                                              ItemClass.Layer.MetroTunnels;
            input.m_netService2.m_service = ItemClass.Service.None;

            if (RayCast(input, out output) && output.m_netSegment != 0)
            {
                height = Mathf.Max(height, output.m_hitPos.y);
                segmentId = output.m_netSegment;
            }

            input.m_netService.m_service = ItemClass.Service.PublicTransport; // For tracks
            input.m_netService.m_itemLayers = ItemClass.Layer.Default |
                                              ItemClass.Layer.MetroTunnels;
            input.m_netService2.m_service = ItemClass.Service.Beautification; // For paths, quays, pedestrian bridges
            input.m_netService2.m_itemLayers = ItemClass.Layer.Default |
                                               ItemClass.Layer.MetroTunnels;

            if (RayCast(input, out output) && output.m_netSegment != 0)
            {
                // Retry without beautification if an unrendered segment is found
                if (!Singleton<NetManager>.instance.m_segments.m_buffer[output.m_netSegment].Info.m_requireSegmentRenderers)
                {
                    input.m_netService2.m_service = ItemClass.Service.None;
                    RayCast(input, out output);
                }

                if (output.m_netSegment != 0)
                {
                    ref var segment = ref Singleton<NetManager>.instance.m_segments.m_buffer[output.m_netSegment];

                    if (segment.Info.m_requireSegmentRenderers && (segmentId == 0 || height < output.m_hitPos.y))
                    {
                        height = Mathf.Max(height, output.m_hitPos.y);
                        segmentId = output.m_netSegment;
                    }
                }
            }

            // If a road was found, try to find a lane and find the precise height.
            if (segmentId != 0)
            {
                float offset = 0f;
                int lane = 0;
                ref var segment = ref Singleton<NetManager>.instance.m_segments.m_buffer[segmentId];

                if (GetClosestLanePositionFiltered(ref segment, position, out roadPos, out offset, out lane, true))
                {
                    height = roadPos.y;

                    // special case ignore setting road status for gravel. Use ground instead.
                    if ((segment.Info.m_setVehicleFlags & Vehicle.Flags.OnGravel) > 0)
                    {
                        collisionType = CollisionTypes.Ground;
                    }
                    else
                    {
                        collisionType = CollisionTypes.Road;
                    }

                    if (offset == 0f || offset == 1f)
                    {
                        ushort nodeId = offset == 0f ? segment.m_startNode : segment.m_endNode;
                        ref var node = ref Singleton<NetManager>.instance.m_nodes.m_buffer[nodeId];
                        if (node.CountSegments() > 1)
                        {
                            GetClosestLanePositionOnNodeFiltered(nodeId, segmentId, (ushort)lane, position, ref roadPos);
                            height = roadPos.y;
                        }
                    }
                }
            }

            return height;
        }

        private static void GetClosestLanePositionOnNodeFiltered(ushort currNodeId, ushort currSegmentId, ushort currLaneId, Vector3 inPos, ref Vector3 currClosest)
        {
            int iter = 0;
            ref var node = ref NetManager.instance.m_nodes.m_buffer[currNodeId];
            float currClosestDist = Vector3.Magnitude(currClosest - inPos);
            ushort currClosestSegment = currSegmentId;
            bool transitionNode = (node.m_flags & NetNode.Flags.Transition) > 0;
            float currLaneVOffset = NetManager.instance.m_segments.m_buffer[currSegmentId].Info.m_lanes[currLaneId].m_verticalOffset;
            float lowest = 10000.0f;

            while (iter < 8) // Cities only supports 8 segments per node.
            {
                ushort altSegmentId = node.GetSegment(iter);

                if (altSegmentId != ushort.MaxValue && altSegmentId != default)
                {
                    ref var tmpSegment = ref NetManager.instance.m_segments.m_buffer[altSegmentId];

                    if (transitionNode)
                    {
                        foreach (var laneObj in tmpSegment.Info.m_lanes)
                        {
                            lowest = Mathf.Min(lowest, laneObj.m_verticalOffset);
                        }
                    }

                    if (altSegmentId != currSegmentId)
                    {
                        Vector3 roadPos;
                        float offset;
                        int lane;

                        if (GetClosestLanePositionFiltered(ref tmpSegment, inPos, out roadPos, out offset, out lane, true))
                        {
                            var horzOffset = inPos - roadPos;
                            horzOffset.y = 0;
                            float tmpDist = Vector3.Magnitude(horzOffset);
                            if (offset != 0 && offset != 1)
                            {
                                if (tmpDist < ROAD_VALID_LANE_DIST_MULT * tmpSegment.Info.m_lanes[lane].m_width)
                                {
                                    currClosest = roadPos;
                                    return;
                                }
                            }
                            else if (tmpDist < currClosestDist)
                            {
                                currClosestSegment = altSegmentId;
                                currClosestDist = tmpDist;
                                currClosest = roadPos;
                                currLaneVOffset = tmpSegment.Info.m_lanes[lane].m_verticalOffset;
                            }
                        }
                    }
                }
                iter++;
            }

            if (transitionNode)
            {
                var closestDir = Singleton<NetManager>.instance.m_segments.m_buffer[currClosestSegment].GetDirection(currNodeId);
                currClosest.y += Mathf.Lerp(0.0f, lowest - currLaneVOffset, Mathf.Clamp(Vector3.Dot(currClosest - inPos, Vector3.Normalize(closestDir)) * 0.5f, 0.0f, 1.0f));
            }
        }

        private static bool GetClosestLanePositionFiltered(ref NetSegment segmentIn, Vector3 posIn, out Vector3 posOut, out float offsetOut, out int laneIndex, bool smooth = default)
        {
            uint lane = segmentIn.m_lanes;
            float dist = 10000.0f;
            bool found = false;
            int index = 0;
            laneIndex = -1;
            NetInfo.LaneType type;
            var lanePos = new Vector3[segmentIn.Info.m_lanes.Length];

            posOut = Vector3.zero;
            offsetOut = -1f;

            while (lane != 0)
            {
                type = segmentIn.Info.m_lanes[index].m_laneType;

                if (type != NetInfo.LaneType.None)
                {
                    Singleton<NetManager>.instance.m_lanes.m_buffer[lane].GetClosestPosition(posIn, out var posTmp, out var offsetTmp);
                    if ((offsetTmp != 0f && offsetTmp != 1f) || ((type & NetInfo.LaneType.Pedestrian) == 0))
                    {
                        float distTmp = Vector3.Magnitude(posTmp - posIn);
                        lanePos[index] = posTmp;

                        if (distTmp < dist)
                        {
                            dist = distTmp;
                            posOut = posTmp;
                            offsetOut = offsetTmp;
                            laneIndex = index;
                            found = true;
                        }
                    }
                }
                lane = Singleton<NetManager>.instance.m_lanes.m_buffer[lane].m_nextLane;
                index++;
            }

            if (smooth && found)
            {
                var dist2D = posOut - posIn;
                var posAdj = Vector3.zero;
                bool adjFound = false;

                dist = 10000.0f;
                dist2D.y = 0.0f;

                for (int adjLane = 0; adjLane < lanePos.Length; adjLane++)
                {
                    var posAdjTmp = lanePos[adjLane];

                    if (adjLane != laneIndex && posAdjTmp != Vector3.zero && Mathf.Abs(posAdjTmp.y - posOut.y) < ROAD_VALID_LANE_H_DIFF)
                    {
                        var dist2DTmp = posAdjTmp - posIn;
                        float distTmp = Vector3.Magnitude(posAdjTmp - posIn);
                        dist2DTmp.y = 0.0f;
                        if (Vector3.Dot(dist2D, dist2DTmp) < 0.0f && distTmp < dist)
                        {
                            dist = distTmp;
                            posAdj = posAdjTmp;
                            adjFound = true;
                        }
                    }
                }

                if (adjFound)
                {
                    var distAdj2D = posAdj - posIn;
                    distAdj2D.y = 0.0f;
                    float dist2DM = Vector3.Magnitude(dist2D);
                    float distAdj2DM = Vector3.Magnitude(distAdj2D);
                    posOut = Vector3.Lerp(posOut, posAdj, dist2DM / (dist2DM + distAdj2DM));
                }
            }

            return found;
        }
    }
}