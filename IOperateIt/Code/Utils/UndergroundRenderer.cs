using ColossalFramework;
using System.Collections.Generic;
using UnityEngine;

namespace IOperateIt.Utils
{
    public class UndergroundRender
    {
        private struct NetInfoBackup(NetInfo.Node[] nodes, NetInfo.Segment[] segments)
        {
            public NetInfo.Node[] nodes = nodes;
            public NetInfo.Segment[] segments = segments;
        }
        private readonly Dictionary<string, string> customUndergroundMappings = new() {
        // Some tunnel names are atypical and need to be manually mapped.
        { "HighwayRamp Tunnel", "HighwayRampElevated" },
        { "Metro Track", "Metro Track Elevated 01" },
        { "Metro Station Track", "Metro Station Track Elevated 01" },
        { "Large Oneway Road Tunnel", "Large Oneway Elevated"},
        { "Metro Station Below Ground Bypass", "Metro Station Track Elevated Bypass" },
        { "Metro Station Below Ground Dual Island", "Metro Station Track Elevated Dual Island" },
        { "Metro Station Below Ground Island","Metro Station Track Elevated Island Platform" }
        };
        private readonly Dictionary<NetInfo, NetInfoBackup> backupPrefabData = [];
        private Material backupUndergroundMaterial = null;
        public void OverridePrefabs()
        {
            // override the underground material for all vehicles.
            for (uint prefabIndex = 0; prefabIndex < PrefabCollection<VehicleInfo>.PrefabCount(); prefabIndex++)
            {
                VehicleInfo prefabVehicleInfo = PrefabCollection<VehicleInfo>.GetPrefab(prefabIndex);
                if (prefabVehicleInfo == null) continue;
                prefabVehicleInfo.m_undergroundMaterial = prefabVehicleInfo.m_material;
                prefabVehicleInfo.m_undergroundLodMaterial = prefabVehicleInfo.m_lodMaterialCombined;
                foreach (VehicleInfo.MeshInfo submesh in prefabVehicleInfo.m_subMeshes)
                {
                    if (submesh.m_subInfo)
                    {
                        VehicleInfoSub subVehicleInfo = (VehicleInfoSub)submesh.m_subInfo;
                        subVehicleInfo.m_undergroundMaterial = subVehicleInfo.m_material;
                        subVehicleInfo.m_undergroundLodMaterial = subVehicleInfo.m_lodMaterialCombined;
                    }
                }
            }

            int prefabCount = PrefabCollection<NetInfo>.PrefabCount();

            // only modify prefabs with MetroTunnels item layer or underground render layer.
            for (uint prefabIndex = 0; prefabIndex < prefabCount; prefabIndex++)
            {
                NetInfo prefabNetInfo = PrefabCollection<NetInfo>.GetPrefab(prefabIndex);
                if (prefabNetInfo == null) continue;
                NetInfo prefabReplaceInfo = prefabNetInfo;
                bool bHasUnderground = false;
                bool bForceUnderground = false;

                for (int index = 0; index < prefabReplaceInfo.m_segments.Length; index++)
                {
                    bHasUnderground |= prefabReplaceInfo.m_segments[index].m_layer == MapUtils.LAYER_UNDERGROUND;
                }

                for (int index = 0; index < prefabReplaceInfo.m_nodes.Length; index++)
                {
                    bHasUnderground |= prefabReplaceInfo.m_nodes[index].m_layer == MapUtils.LAYER_UNDERGROUND;
                }

                if (prefabNetInfo.m_class.m_layer == ItemClass.Layer.MetroTunnels)
                {
                    bool bCanReplace = true;

                    foreach (NetInfo.Segment s in prefabNetInfo.m_segments)
                    {
                        if (s.m_material && (!s.m_material.shader || s.m_material.shader.name != "Custom/Net/Metro"))
                        {
                            bCanReplace = false;
                        }
                    }

                    // replace the prefab with elevated variant if no visible meshes are found.
                    if (bCanReplace)
                    {
                        string replaceName = "";

                        // get underground to elvated mapping.
                        if (!customUndergroundMappings.TryGetValue(prefabNetInfo.name, out replaceName))
                        {
                            replaceName = prefabNetInfo.name.Replace(" Tunnel", " Elevated");
                        }

                        // find the elevated counterpart prefab to be used as a reference.
                        for (uint otherPrefabIndex = 0; otherPrefabIndex < prefabCount; otherPrefabIndex++)
                        {
                            NetInfo tmpInfo = PrefabCollection<NetInfo>.GetPrefab(otherPrefabIndex);
                            if (tmpInfo.m_class.m_layer == ItemClass.Layer.Default && tmpInfo.name == replaceName)
                            {
                                prefabReplaceInfo = tmpInfo;
                                bForceUnderground = true;
                                break;
                            }
                        }
                    }
                }


                if (bHasUnderground)
                {
                    backupPrefabData[prefabNetInfo] = new NetInfoBackup(prefabNetInfo.m_nodes, prefabNetInfo.m_segments);
                    NetInfo.Segment[] segments = new NetInfo.Segment[prefabReplaceInfo.m_segments.Length];
                    NetInfo.Node[] nodes = new NetInfo.Node[prefabReplaceInfo.m_nodes.Length];

                    for (int index = 0; index < prefabReplaceInfo.m_segments.Length; index++)
                    {
                        NetInfo.Segment newSegment = CopySegment(prefabReplaceInfo.m_segments[index]);

                        if (newSegment.m_layer == MapUtils.LAYER_UNDERGROUND)
                        {
                            // disable segment underground xray component from rendering.
                            if (newSegment.m_material && newSegment.m_material.shader && newSegment.m_material.shader.name == "Custom/Net/Metro")
                            {
                                newSegment.m_forwardForbidden = NetSegment.Flags.All;
                                newSegment.m_forwardRequired = NetSegment.Flags.None;
                                newSegment.m_backwardForbidden = NetSegment.Flags.All;
                                newSegment.m_backwardRequired = NetSegment.Flags.None;
                            }
                        }
                        // apply underground layer if the segment is being replaced from an overground component.
                        else if (bForceUnderground)
                        {
                            newSegment.m_layer = MapUtils.LAYER_UNDERGROUND;
                        }

                        segments[index] = newSegment;
                    }

                    for (int index = 0; index < prefabReplaceInfo.m_nodes.Length; index++)
                    {
                        NetInfo.Node newNode = CopyNode(prefabReplaceInfo.m_nodes[index]);

                        if (newNode.m_layer == MapUtils.LAYER_UNDERGROUND)
                        {
                            // disable node underground xray component from rendering.
                            if (newNode.m_material && newNode.m_material.shader && newNode.m_material.shader.name == "Custom/Net/Metro")
                            {
                                newNode.m_flagsForbidden = NetNode.Flags.All;
                                newNode.m_flagsRequired = NetNode.Flags.None;
                            }
                        }
                        // apply underground layer if the node is being replaced from an overground component.
                        else if (bForceUnderground)
                        {
                            newNode.m_layer = MapUtils.LAYER_UNDERGROUND;
                        }

                        newNode.m_flagsForbidden = newNode.m_flagsForbidden & ~NetNode.Flags.Underground;

                        nodes[index] = newNode;
                    }

                    prefabNetInfo.m_segments = segments;
                    prefabNetInfo.m_nodes = nodes;
                }
            }

            // replace the distant LOD material for underground and update LOD render groups.
            RenderManager rm = Singleton<RenderManager>.instance;
            backupUndergroundMaterial = rm.m_groupLayerMaterials[MapUtils.LAYER_UNDERGROUND];
            rm.m_groupLayerMaterials[MapUtils.LAYER_UNDERGROUND] = rm.m_groupLayerMaterials[MapUtils.LAYER_ROAD];
            rm.UpdateGroups(MapUtils.LAYER_UNDERGROUND);
        }

        public void RestorePrefabs()
        {
            // delete all underground vehicle materials. Cities will auto generate new ones.
            for (uint iter = 0; iter < PrefabCollection<VehicleInfo>.PrefabCount(); iter++)
            {
                VehicleInfo prefabVehicleInfo = PrefabCollection<VehicleInfo>.GetPrefab(iter);
                if (prefabVehicleInfo == null) continue;
                prefabVehicleInfo.m_undergroundMaterial = null;
                prefabVehicleInfo.m_undergroundLodMaterial = null;
                foreach (VehicleInfo.MeshInfo submesh in prefabVehicleInfo.m_subMeshes)
                {
                    if (submesh.m_subInfo)
                    {
                        VehicleInfoSub subVehicleInfo = (VehicleInfoSub)submesh.m_subInfo;
                        subVehicleInfo.m_undergroundMaterial = null;
                        subVehicleInfo.m_undergroundLodMaterial = null;
                    }
                }
            }

            // restore road prefab segment and node data to before driving.
            int prefabCount = PrefabCollection<NetInfo>.PrefabCount();
            for (uint prefabIndex = 0; prefabIndex < prefabCount; prefabIndex++)
            {
                NetInfo prefabNetInfo = PrefabCollection<NetInfo>.GetPrefab(prefabIndex);
                if (prefabNetInfo == null) continue;

                if (backupPrefabData.ContainsKey(prefabNetInfo))
                {
                    if (backupPrefabData.TryGetValue(prefabNetInfo, out NetInfoBackup backupData))
                    {
                        prefabNetInfo.m_segments = backupData.segments;
                        prefabNetInfo.m_nodes = backupData.nodes;

                        backupPrefabData.Remove(prefabNetInfo);
                    }
                }
            }

            backupPrefabData.Clear();

            // restore the distant LOD material for underground and update LOD render groups.
            RenderManager rm = RenderManager.instance;
            rm.m_groupLayerMaterials[MapUtils.LAYER_UNDERGROUND] = backupUndergroundMaterial;
            backupUndergroundMaterial = null;
            rm.UpdateGroups(MapUtils.LAYER_UNDERGROUND);
        }
        private static NetInfo.Node CopyNode(NetInfo.Node node)
        {
            NetInfo.Node retval = new NetInfo.Node();
            retval.m_mesh = node.m_mesh;
            retval.m_lodMesh = node.m_lodMesh;
            retval.m_material = node.m_material;
            retval.m_lodMaterial = node.m_lodMaterial;
            retval.m_flagsRequired = node.m_flagsRequired;
            retval.m_flagsRequired2 = node.m_flagsRequired2;
            retval.m_flagsForbidden = node.m_flagsForbidden;
            retval.m_flagsForbidden2 = node.m_flagsForbidden2;
            retval.m_connectGroup = node.m_connectGroup;
            retval.m_directConnect = node.m_directConnect;
            retval.m_emptyTransparent = node.m_emptyTransparent;
            retval.m_tagsRequired = node.m_tagsRequired;
            retval.m_nodeTagsRequired = node.m_nodeTagsRequired;
            retval.m_tagsForbidden = node.m_tagsForbidden;
            retval.m_nodeTagsForbidden = node.m_nodeTagsForbidden;
            retval.m_forbidAnyTags = node.m_forbidAnyTags;
            retval.m_minSameTags = node.m_minSameTags;
            retval.m_maxSameTags = node.m_maxSameTags;
            retval.m_minOtherTags = node.m_minOtherTags;
            retval.m_maxOtherTags = node.m_maxOtherTags;
            retval.m_nodeMesh = node.m_nodeMesh;
            retval.m_nodeMaterial = node.m_nodeMaterial;
            retval.m_combinedLod = node.m_combinedLod;
            retval.m_lodRenderDistance = node.m_lodRenderDistance;
            retval.m_requireSurfaceMaps = node.m_requireSurfaceMaps;
            retval.m_requireWindSpeed = node.m_requireWindSpeed;
            retval.m_preserveUVs = node.m_preserveUVs;
            retval.m_generateTangents = node.m_generateTangents;
            retval.m_layer = node.m_layer;

            return retval;
        }

        private static NetInfo.Segment CopySegment(NetInfo.Segment segment)
        {

            NetInfo.Segment retval = new NetInfo.Segment();
            retval.m_mesh = segment.m_mesh;
            retval.m_lodMesh = segment.m_lodMesh;
            retval.m_material = segment.m_material;
            retval.m_lodMaterial = segment.m_lodMaterial;
            retval.m_forwardRequired = segment.m_forwardRequired;
            retval.m_forwardForbidden = segment.m_forwardForbidden;
            retval.m_backwardRequired = segment.m_backwardRequired;
            retval.m_backwardForbidden = segment.m_backwardForbidden;
            retval.m_emptyTransparent = segment.m_emptyTransparent;
            retval.m_disableBendNodes = segment.m_disableBendNodes;
            retval.m_segmentMesh = segment.m_segmentMesh;
            retval.m_segmentMaterial = segment.m_segmentMaterial;
            retval.m_combinedLod = segment.m_combinedLod;
            retval.m_lodRenderDistance = segment.m_lodRenderDistance;
            retval.m_requireSurfaceMaps = segment.m_requireSurfaceMaps;
            retval.m_requireHeightMap = segment.m_requireHeightMap;
            retval.m_requireWindSpeed = segment.m_requireWindSpeed;
            retval.m_preserveUVs = segment.m_preserveUVs;
            retval.m_generateTangents = segment.m_generateTangents;
            retval.m_layer = segment.m_layer;

            return retval;
        }
        /*      there are problems with replacing LodValue. It needs to be registered with NodeManager and/or RenderManager or there will be null reference errors.
                private static NetInfo.LodValue CopyLodValue(NetInfo.LodValue value)
                {
                    NetInfo.LodValue retval = new NetInfo.LodValue();
                    retval.m_key = value.m_key;
                    retval.m_material = value.m_material;
                    retval.m_lodMin = value.m_lodMin;
                    retval.m_lodMax = value.m_lodMax;
                    retval.m_surfaceTexA = value.m_surfaceTexA;
                    retval.m_surfaceTexB = value.m_surfaceTexB;
                    retval.m_surfaceMapping = value.m_surfaceMapping;
                    retval.m_heightMap = value.m_heightMap;
                    retval.m_heightMapping = value.m_heightMapping;

                    return retval;
                }*/
    }
}


