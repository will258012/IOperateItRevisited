using ColossalFramework;
using System.Collections.Generic;
using UnityEngine;

namespace IOperateIt.Utils
{
    public class UndergroundRenderer
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
        private Material roadRenderMaterial = null;

        public UndergroundRenderer()
        {
            roadRenderMaterial = RenderManager.instance.m_groupLayerMaterials[MapUtils.LAYER_ROAD];
        }
        public void OverridePrefabs()
        {
            // override the underground material for all vehicles.
            for (uint prefabIndex = 0; prefabIndex < PrefabCollection<VehicleInfo>.PrefabCount(); prefabIndex++)
            {
                var prefabVehicleInfo = PrefabCollection<VehicleInfo>.GetPrefab(prefabIndex);
                if (prefabVehicleInfo == null) continue;
                prefabVehicleInfo.m_undergroundMaterial = prefabVehicleInfo.m_material;
                prefabVehicleInfo.m_undergroundLodMaterial = prefabVehicleInfo.m_lodMaterialCombined;
                foreach (var submesh in prefabVehicleInfo.m_subMeshes)
                {
                    if (submesh.m_subInfo)
                    {
                        var subVehicleInfo = submesh.m_subInfo as VehicleInfoSub;
                        subVehicleInfo.m_undergroundMaterial = subVehicleInfo.m_material;
                        subVehicleInfo.m_undergroundLodMaterial = subVehicleInfo.m_lodMaterialCombined;
                    }
                }
            }

            // override the underground material for all pedestrians.
            Singleton<CitizenManager>.instance.m_properties.m_undergroundShader = Shader.Find("Custom/Citizens/Citizen/Default");
            for (uint prefabIndex = 0; prefabIndex < PrefabCollection<CitizenInfo>.PrefabCount(); prefabIndex++)
            {
                var prefabCitizenInfo = PrefabCollection<CitizenInfo>.GetPrefab(prefabIndex);
                if (prefabCitizenInfo == null) continue;
                prefabCitizenInfo.m_undergroundLodMaterial = prefabCitizenInfo.m_lodMaterialCombined;
                prefabCitizenInfo.m_instancePool?.m_buffer?.Clear();
                prefabCitizenInfo.m_instancePool?.m_freeInstances?.Clear();
            }

            int prefabCount = PrefabCollection<NetInfo>.PrefabCount();

            // only modify prefabs with MetroTunnels item layer or underground render layer.
            for (uint prefabIndex = 0; prefabIndex < prefabCount; prefabIndex++)
            {
                var prefabNetInfo = PrefabCollection<NetInfo>.GetPrefab(prefabIndex);
                if (prefabNetInfo == null) continue;
                var prefabReplaceInfo = prefabNetInfo;
                bool bHasUndergroundLayer = false;
                bool bHasUndergroundVisibleNode = false;
                bool bOnlyUndergroundXray = true;
                bool bReplaceFound = false;

                for (int index = 0; index < prefabReplaceInfo.m_segments.Length; index++)
                {
                    bool bUndergroundLayer = prefabReplaceInfo.m_segments[index].m_layer == MapUtils.LAYER_UNDERGROUND;
                    bool bXray = prefabReplaceInfo.m_segments[index].m_material?.shader?.name == "Custom/Net/Metro";
                    bHasUndergroundLayer |= bUndergroundLayer;
                    bOnlyUndergroundXray &= bUndergroundLayer && bXray;
                }

                for (int index = 0; index < prefabReplaceInfo.m_nodes.Length; index++)
                {
                    bool bUndergroundLayer = prefabReplaceInfo.m_nodes[index].m_layer == MapUtils.LAYER_UNDERGROUND;
                    bool bXray = prefabReplaceInfo.m_nodes[index].m_material?.shader?.name == "Custom/Net/Metro";
                    bHasUndergroundLayer |= bUndergroundLayer;
                    bOnlyUndergroundXray &= bUndergroundLayer && bXray;
                    bHasUndergroundVisibleNode |=
                        (!EnumExtensions.IsFlagSet(NetNode.Flags.Underground, prefabReplaceInfo.m_nodes[index].m_flagsForbidden) ||
                        EnumExtensions.IsFlagSet(NetNode.Flags.Underground, prefabReplaceInfo.m_nodes[index].m_flagsRequired))
                        && !prefabReplaceInfo.m_nodes[index].m_emptyTransparent
                        && !bXray;
                }

                if (bOnlyUndergroundXray && prefabNetInfo.m_class.m_layer == ItemClass.Layer.MetroTunnels)
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
                        var tmpInfo = PrefabCollection<NetInfo>.GetPrefab(otherPrefabIndex);
                        if (tmpInfo != null && tmpInfo.m_class?.m_layer == ItemClass.Layer.Default && tmpInfo.name == replaceName)
                        {
                            prefabReplaceInfo = tmpInfo;
                            bReplaceFound = true;
                            break;
                        }
                    }
                }

                if (bHasUndergroundLayer)
                {
                    backupPrefabData[prefabNetInfo] = new NetInfoBackup(prefabNetInfo.m_nodes, prefabNetInfo.m_segments);

                    {
                        var segments = new NetInfo.Segment[prefabReplaceInfo.m_segments.Length];
                        for (int index = 0; index < prefabReplaceInfo.m_segments.Length; index++)
                        {
                            var newSegment = CopySegment(prefabReplaceInfo.m_segments[index]);
                            if (newSegment.m_layer == MapUtils.LAYER_UNDERGROUND)
                            {
                                // disable segment underground xray component from rendering.
                                if (newSegment.m_material?.shader?.name == "Custom/Net/Metro")
                                {
                                    newSegment.m_forwardForbidden = NetSegment.Flags.All;
                                    newSegment.m_forwardRequired = NetSegment.Flags.None;
                                    newSegment.m_backwardForbidden = NetSegment.Flags.All;
                                    newSegment.m_backwardRequired = NetSegment.Flags.None;
                                }
                            }
                            // apply underground layer if the segment is being replaced from an overground component.
                            else if (bReplaceFound)
                            {
                                newSegment.m_layer = MapUtils.LAYER_UNDERGROUND;
                            }
                            segments[index] = newSegment;
                        }
                        prefabNetInfo.m_segments = segments;
                    }

                    {
                        var nodes = new NetInfo.Node[prefabReplaceInfo.m_nodes.Length];
                        for (int index = 0; index < prefabReplaceInfo.m_nodes.Length; index++)
                        {
                            var newNode = CopyNode(prefabReplaceInfo.m_nodes[index]);
                            if (newNode.m_layer == MapUtils.LAYER_UNDERGROUND)
                            {
                                // disable node underground xray component from rendering.
                                if (newNode.m_material?.shader?.name == "Custom/Net/Metro")
                                {
                                    newNode.m_flagsForbidden = NetNode.Flags.All;
                                    newNode.m_flagsRequired = NetNode.Flags.None;
                                }
                            }
                            // apply underground layer if the node is being replaced from an overground component.
                            else if (bReplaceFound)
                            {
                                newNode.m_layer = MapUtils.LAYER_UNDERGROUND;
                            }
                            else if (!bHasUndergroundVisibleNode)
                            {
                                newNode.m_flagsForbidden = newNode.m_flagsForbidden & ~NetNode.Flags.Underground;
                            }
                            nodes[index] = newNode;
                        }
                        prefabNetInfo.m_nodes = nodes;
                    }
                }
            }

            // replace the distant LOD material for underground and update LOD render groups.
            var rm = RenderManager.instance;
            rm.m_groupLayerMaterials[MapUtils.LAYER_UNDERGROUND] = roadRenderMaterial;
            rm.UpdateGroups(MapUtils.LAYER_UNDERGROUND);
        }

        public void RestorePrefabs()
        {
            // delete all underground vehicle materials. Cities will auto generate new ones.
            for (uint iter = 0; iter < PrefabCollection<VehicleInfo>.PrefabCount(); iter++)
            {
                var prefabVehicleInfo = PrefabCollection<VehicleInfo>.GetPrefab(iter);
                if (prefabVehicleInfo == null) continue;
                prefabVehicleInfo.m_undergroundMaterial = null;
                prefabVehicleInfo.m_undergroundLodMaterial = null;
                foreach (var submesh in prefabVehicleInfo.m_subMeshes)
                {
                    if (submesh.m_subInfo)
                    {
                        var subVehicleInfo = (VehicleInfoSub)submesh.m_subInfo;
                        subVehicleInfo.m_undergroundMaterial = null;
                        subVehicleInfo.m_undergroundLodMaterial = null;
                    }
                }
            }

            // restore the underground material for all pedestrians. Cities will auto generate new ones.
            Singleton<CitizenManager>.instance.m_properties.m_undergroundShader = Shader.Find("Custom/Citizens/Citizen/Underground");
            for (uint prefabIndex = 0; prefabIndex < PrefabCollection<CitizenInfo>.PrefabCount(); prefabIndex++)
            {
                var prefabCitizenInfo = PrefabCollection<CitizenInfo>.GetPrefab(prefabIndex);
                if (prefabCitizenInfo == null) continue;
                prefabCitizenInfo.m_undergroundLodMaterial = null;
                prefabCitizenInfo.m_instancePool?.m_buffer?.Clear();
                prefabCitizenInfo.m_instancePool?.m_freeInstances?.Clear();
            }

            // restore road prefab segment and node data to before driving.
            int prefabCount = PrefabCollection<NetInfo>.PrefabCount();
            for (uint prefabIndex = 0; prefabIndex < prefabCount; prefabIndex++)
            {
                var prefabNetInfo = PrefabCollection<NetInfo>.GetPrefab(prefabIndex);
                if (prefabNetInfo == null) continue;

                if (backupPrefabData.ContainsKey(prefabNetInfo))
                {
                    if (backupPrefabData.TryGetValue(prefabNetInfo, out var backupData))
                    {
                        prefabNetInfo.m_segments = backupData.segments;
                        prefabNetInfo.m_nodes = backupData.nodes;

                        backupPrefabData.Remove(prefabNetInfo);
                    }
                }
            }

            backupPrefabData.Clear();

            // restore the distant LOD material for underground and update LOD render groups.
            var rm = Singleton<RenderManager>.instance;
            rm.m_groupLayerMaterials[MapUtils.LAYER_UNDERGROUND] = backupUndergroundMaterial;
            rm.UpdateGroups(MapUtils.LAYER_UNDERGROUND);
        }

        private static NetInfo.Node CopyNode(NetInfo.Node node) =>
            new()
            {
                m_mesh = node.m_mesh,
                m_lodMesh = node.m_lodMesh,
                m_material = node.m_material,
                m_lodMaterial = node.m_lodMaterial,
                m_flagsRequired = node.m_flagsRequired,
                m_flagsRequired2 = node.m_flagsRequired2,
                m_flagsForbidden = node.m_flagsForbidden,
                m_flagsForbidden2 = node.m_flagsForbidden2,
                m_connectGroup = node.m_connectGroup,
                m_directConnect = node.m_directConnect,
                m_emptyTransparent = node.m_emptyTransparent,
                m_tagsRequired = node.m_tagsRequired,
                m_nodeTagsRequired = node.m_nodeTagsRequired,
                m_tagsForbidden = node.m_tagsForbidden,
                m_nodeTagsForbidden = node.m_nodeTagsForbidden,
                m_forbidAnyTags = node.m_forbidAnyTags,
                m_minSameTags = node.m_minSameTags,
                m_maxSameTags = node.m_maxSameTags,
                m_minOtherTags = node.m_minOtherTags,
                m_maxOtherTags = node.m_maxOtherTags,
                m_nodeMesh = node.m_nodeMesh,
                m_nodeMaterial = node.m_nodeMaterial,
                m_combinedLod = node.m_combinedLod,
                m_lodRenderDistance = node.m_lodRenderDistance,
                m_requireSurfaceMaps = node.m_requireSurfaceMaps,
                m_requireWindSpeed = node.m_requireWindSpeed,
                m_preserveUVs = node.m_preserveUVs,
                m_generateTangents = node.m_generateTangents,
                m_layer = node.m_layer
            };

        private static NetInfo.Segment CopySegment(NetInfo.Segment segment) =>
            new()
            {
                m_mesh = segment.m_mesh,
                m_lodMesh = segment.m_lodMesh,
                m_material = segment.m_material,
                m_lodMaterial = segment.m_lodMaterial,
                m_forwardRequired = segment.m_forwardRequired,
                m_forwardForbidden = segment.m_forwardForbidden,
                m_backwardRequired = segment.m_backwardRequired,
                m_backwardForbidden = segment.m_backwardForbidden,
                m_emptyTransparent = segment.m_emptyTransparent,
                m_disableBendNodes = segment.m_disableBendNodes,
                m_segmentMesh = segment.m_segmentMesh,
                m_segmentMaterial = segment.m_segmentMaterial,
                m_combinedLod = segment.m_combinedLod,
                m_lodRenderDistance = segment.m_lodRenderDistance,
                m_requireSurfaceMaps = segment.m_requireSurfaceMaps,
                m_requireHeightMap = segment.m_requireHeightMap,
                m_requireWindSpeed = segment.m_requireWindSpeed,
                m_preserveUVs = segment.m_preserveUVs,
                m_generateTangents = segment.m_generateTangents,
                m_layer = segment.m_layer
            };
    }
}


