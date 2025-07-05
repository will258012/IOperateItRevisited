using AlgernonCommons;
using ColossalFramework;
using IOperateIt.Utils;
using IOperateIt.Settings;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace IOperateIt
{
    public class ColliderContainer : MonoBehaviour
    {
        public enum ContainerType
        {
            TYPE_DEFAULT = 0,
            TYPE_VEHICLE,
        }

        public MeshCollider MeshCollider;
        public BoxCollider BoxCollider;
        public Rigidbody Rigidbody;
        public ContainerType Type = ContainerType.TYPE_DEFAULT;
        public int ID = 0;
    }
    public class CollidersManager
    {
        private const int NUM_BUILDING_COLLIDERS = 36;
        private const int NUM_VEHICLE_COLLIDERS = 48;
        private const int NUM_PARKED_VEHICLE_COLLIDERS = 32;
        private const float SCAN_DISTANCE = 50f;
        private const float COLLIDER_JUMP_DISTANCE = 10f;

        private ColliderContainer[] m_BuildingColliders;
        private ColliderContainer[] m_VehicleColliders;
        private ColliderContainer[] m_ParkedVehicleColliders;

        private class MapElem
        {
            public int updateId;
            public int index;

            public MapElem(int updateId, int index)
            {
                this.updateId = updateId;
                this.index = index;
            }
        }

        private Dictionary<ushort, MapElem> m_VehicleToColliderMap;
        private Queue<ushort> m_VehicleQueue;

        private int m_updateId = 0;

        /// <summary>
        /// Set up colliders for nearby buildings and vehicles.
        /// </summary>
        public IEnumerator InitializeColliders()
        {
            m_BuildingColliders = new ColliderContainer[NUM_BUILDING_COLLIDERS];
            m_VehicleColliders = new ColliderContainer[NUM_VEHICLE_COLLIDERS];
            m_ParkedVehicleColliders = new ColliderContainer[NUM_PARKED_VEHICLE_COLLIDERS];

            m_VehicleToColliderMap = new Dictionary<ushort, MapElem>();
            m_VehicleQueue = new Queue<ushort>();

            PhysicMaterial material = new PhysicMaterial();
            material.bounciness = 0.05f;
            material.staticFriction = 0.1f;

            for (int i = 0; i < NUM_BUILDING_COLLIDERS; i++)
            {
                GameObject gameObject = new GameObject("BuildingCollider" + i);
                gameObject.layer = MapUtils.LAYER_BUILDINGS;
                var buildingCollider = gameObject.AddComponent<ColliderContainer>();
                buildingCollider.MeshCollider = gameObject.AddComponent<MeshCollider>();
                buildingCollider.MeshCollider.convex = false;
                buildingCollider.MeshCollider.enabled = true;
                buildingCollider.MeshCollider.sharedMaterial = material;
                m_BuildingColliders[i] = buildingCollider;
                gameObject.SetActive(false);
            }

            for (int i = 0; i < NUM_VEHICLE_COLLIDERS; i++)
            {
                GameObject gameObject = new GameObject("VehicleCollider" + i);
                gameObject.layer = MapUtils.LAYER_VEHICLES;
                var vehicleCollider = gameObject.AddComponent<ColliderContainer>();
                vehicleCollider.BoxCollider = gameObject.AddComponent<BoxCollider>();
                vehicleCollider.BoxCollider.enabled = true;
                vehicleCollider.BoxCollider.sharedMaterial = material;
                vehicleCollider.Rigidbody = gameObject.AddComponent<Rigidbody>();
                vehicleCollider.Rigidbody.isKinematic = true;
                vehicleCollider.Rigidbody.interpolation = RigidbodyInterpolation.Interpolate;
                vehicleCollider.Type = ColliderContainer.ContainerType.TYPE_VEHICLE;
                m_VehicleColliders[i] = vehicleCollider;
                gameObject.SetActive(false);
            }

            for (int i = 0; i < NUM_PARKED_VEHICLE_COLLIDERS; i++)
            {
                GameObject gameObject = new GameObject("ParkedVehicleCollider" + i);
                gameObject.layer = MapUtils.LAYER_VEHICLES;
                var parkedVehicleCollider = gameObject.AddComponent<ColliderContainer>();
                parkedVehicleCollider.BoxCollider = gameObject.AddComponent<BoxCollider>();
                parkedVehicleCollider.BoxCollider.enabled = true;
                parkedVehicleCollider.BoxCollider.sharedMaterial = material;
                m_ParkedVehicleColliders[i] = parkedVehicleCollider;
                gameObject.SetActive(false);
            }
            yield return null;
        }
        private void SetVehicleCollider(ushort vehicleId, int colliderIndex, bool isParked = false)
        {
            Vector3 vehiclePosition;
            Quaternion vehicleRotation;

            if (isParked)
            {
                ref var parkedVehicle = ref Singleton<VehicleManager>.instance.m_parkedVehicles.m_buffer[vehicleId];
                if (((VehicleParked.Flags) parkedVehicle.m_flags & VehicleParked.Flags.Parking) == 0)
                {
                    vehicleRotation = parkedVehicle.m_rotation;
                    vehiclePosition = parkedVehicle.m_position;
                    m_ParkedVehicleColliders[colliderIndex].gameObject.SetActive(true);
                    m_ParkedVehicleColliders[colliderIndex].BoxCollider.size = parkedVehicle.Info.m_lodMesh.bounds.size;
                    m_ParkedVehicleColliders[colliderIndex].BoxCollider.center = new Vector3(0.0f, 0.5f * parkedVehicle.Info.m_lodMesh.bounds.size.y, 0.0f);
                    m_ParkedVehicleColliders[colliderIndex].gameObject.transform.position = vehiclePosition;
                    m_ParkedVehicleColliders[colliderIndex].gameObject.transform.rotation = vehicleRotation;

                    BoxCollider bc = m_ParkedVehicleColliders[colliderIndex].BoxCollider;
                    DebugHelper.DrawDebugBox(bc.size, bc.transform.TransformPoint(bc.center), bc.transform.rotation, Color.red);
                }
                else
                {
                    m_ParkedVehicleColliders[colliderIndex].gameObject.SetActive(false);
                }
            }
            else
            {
                ref var vehicle = ref Singleton<VehicleManager>.instance.m_vehicles.m_buffer[vehicleId];
                vehicle.GetSmoothPosition(vehicleId, out vehiclePosition, out vehicleRotation);

                if ((vehicle.m_flags & Vehicle.Flags.Inverted) > 0) // rotate 180 for inverted train cars
                {
                    vehicleRotation = new Quaternion(0, 1, 0, 0) * vehicleRotation;
                }

                if (vehicleId == m_VehicleColliders[colliderIndex].ID 
                    && Vector3.Magnitude(vehiclePosition - m_VehicleColliders[colliderIndex].Rigidbody.transform.position) < COLLIDER_JUMP_DISTANCE)
                {
                    m_VehicleColliders[colliderIndex].Rigidbody.MovePosition(vehiclePosition);
                    m_VehicleColliders[colliderIndex].Rigidbody.MoveRotation(vehicleRotation);
                }
                else
                {
                    m_VehicleColliders[colliderIndex].Rigidbody.transform.position = vehiclePosition;
                    m_VehicleColliders[colliderIndex].Rigidbody.transform.rotation = vehicleRotation;
                }

                m_VehicleColliders[colliderIndex].gameObject.SetActive(true);
                m_VehicleColliders[colliderIndex].ID = vehicleId;
                m_VehicleColliders[colliderIndex].BoxCollider.size = vehicle.Info.m_lodMesh.bounds.size;
                m_VehicleColliders[colliderIndex].BoxCollider.center = new Vector3(0.0f, 0.5f * vehicle.Info.m_lodMesh.bounds.size.y, 0.0f);

                BoxCollider bc = m_VehicleColliders[colliderIndex].BoxCollider;
                DebugHelper.DrawDebugBox(bc.size, bc.transform.TransformPoint(bc.center), bc.transform.rotation, Color.green);
                DebugHelper.DrawDebugMarker(5f, vehicle.GetLastFramePosition(), Color.green);
            }
        }
        public void UpdateColliders(Transform transform)
        {
            if (ModSettings.BuildingCollision)
                UpdateBuildingColliders(transform);
            if (ModSettings.VehicleCollision)
            {
                UpdateVehicleColliders(transform);
                UpdateParkedVehicleColliders(transform);
            }

            m_updateId++;
        }
        private void UpdateBuildingColliders(Transform transform)
        {
            Vector3 segmentVector;
            ColossalFramework.Math.Segment3 circularScanSegment;
            ushort buildingIndex;
            var hitPos = Vector3.zero;
            HashSet<ushort> hitBuildings = new HashSet<ushort>();
            Vector3 buildingPosition;
            Quaternion buildingRotation;

            for (int i = 0; i < NUM_BUILDING_COLLIDERS; i++)
            {
                float x = (float)Mathf.Cos(Mathf.Deg2Rad * (360f / NUM_BUILDING_COLLIDERS * i));
                float z = (float)Mathf.Sin(Mathf.Deg2Rad * (360f / NUM_BUILDING_COLLIDERS * i));
                segmentVector = new Vector3(x, 1f, z) * SCAN_DISTANCE;
                circularScanSegment = new ColossalFramework.Math.Segment3(transform.position,
                                                                          transform.position + segmentVector);

                Singleton<BuildingManager>.instance.RayCast(circularScanSegment, ItemClass.Service.None, ItemClass.SubService.None, ItemClass.Layer.Default, Building.Flags.None, out hitPos, out buildingIndex);
                if (hitPos != Vector3.zero)
                {
                    ref var building = ref Singleton<BuildingManager>.instance.m_buildings.m_buffer[buildingIndex];
                    if (!hitBuildings.Contains(buildingIndex) &&
                        building.Info.name != "478820060.CableStay32m_Data" && building.Info.name != "BridgePillar.CableStay32m_Data")
                    {
                        building.CalculateMeshPosition(out buildingPosition, out buildingRotation);

                        if (building.Info.m_class.m_service == ItemClass.Service.Road)
                        {
                            buildingPosition.y -= 1.0f;
                        }

                        m_BuildingColliders[i].gameObject.SetActive(true);
                        m_BuildingColliders[i].MeshCollider.sharedMesh = building.Info.m_mesh;
                        m_BuildingColliders[i].gameObject.transform.position = buildingPosition;
                        m_BuildingColliders[i].gameObject.transform.rotation = buildingRotation;
                        hitBuildings.Add(buildingIndex);
                    }
                }
            }
        }


        private void UpdateVehicleColliders(Transform transform)
        {
            int gridX = Mathf.Clamp((int)(transform.position.x / 32f + 270f), 0, 539);
            int gridZ = Mathf.Clamp((int)(transform.position.z / 32f + 270f), 0, 539);
           
            UpdateVehicleCollidersInGridSpace(gridZ * 540 + gridX);
            UpdateVehicleCollidersInGridSpace(gridZ * 540 + gridX - 1);
            UpdateVehicleCollidersInGridSpace((gridZ - 1) * 540 + gridX);
            UpdateVehicleCollidersInGridSpace(gridZ * 540 + gridX + 1);
            UpdateVehicleCollidersInGridSpace((gridZ + 1) * 540 + gridX);
            UpdateVehicleCollidersInGridSpace((gridZ - 1) * 540 + gridX - 1);
            UpdateVehicleCollidersInGridSpace((gridZ + 1) * 540 + gridX + 1);
            UpdateVehicleCollidersInGridSpace((gridZ - 1) * 540 + gridX + 1);
            UpdateVehicleCollidersInGridSpace((gridZ + 1) * 540 + gridX - 1);

            var tmpMap = new Dictionary<ushort, MapElem>(m_VehicleToColliderMap);
            foreach (var pair in tmpMap)
            {
                if (pair.Value.updateId != m_updateId)
                {
                    m_VehicleColliders[pair.Value.index].ID = 0;
                    m_VehicleToColliderMap.Remove(pair.Key);
                }
            }

            int colliderCounter = 0;
            while (colliderCounter < NUM_VEHICLE_COLLIDERS)
            {
                if (m_VehicleColliders[colliderCounter].ID == 0)
                {
                    if (m_VehicleQueue.Count == 0)
                    {
                        m_VehicleColliders[colliderCounter].gameObject.SetActive(false);
                    }
                    else
                    {
                        ushort vehicleId = m_VehicleQueue.Dequeue();
                        SetVehicleCollider(vehicleId, colliderCounter, false);
                        m_VehicleToColliderMap[vehicleId] = new MapElem(m_updateId, colliderCounter);
                    }
                }
                else
                {
                    SetVehicleCollider((ushort)m_VehicleColliders[colliderCounter].ID, colliderCounter, false);
                }
                colliderCounter++;
            }

            if (m_VehicleQueue.Count > 0)
            {
                Logging.Error("Vehicle Colliders Full!");
            }
            m_VehicleQueue.Clear();
        }

        private void UpdateVehicleCollidersInGridSpace(int index)
        {
            ushort vehicleId = Singleton<VehicleManager>.instance.m_vehicleGrid[index];
            while (vehicleId != 0)
            {
                MapElem tmpElem;
                if (m_VehicleToColliderMap.TryGetValue(vehicleId, out tmpElem))
                {
                    tmpElem.updateId = m_updateId;
                }
                else
                {
                    m_VehicleQueue.Enqueue(vehicleId);
                }
                vehicleId = Singleton<VehicleManager>.instance.m_vehicles.m_buffer[vehicleId].m_nextGridVehicle;
            }
        }

        private void UpdateParkedVehicleColliders(Transform transform)
        {
            int gridX = Mathf.Clamp((int)(transform.position.x / 32.0 + 270.0), 0, 539);
            int gridZ = Mathf.Clamp((int)(transform.position.z / 32.0 + 270.0), 0, 539);
            int offsetX = (transform.position.x / 32f + 270f) - (int)(transform.position.x / 32f + 270f) > 0.5 ? 1 : -1;
            int offsetZ = (transform.position.z / 32f + 270f) - (int)(transform.position.z / 32f + 270f) > 0.5 ? 1 : -1;
            int colliderCounter = 0;

            UpdateParkedVehicleCollidersInGridSpace(gridZ * 540 + gridX, ref colliderCounter);

            if (colliderCounter >= NUM_PARKED_VEHICLE_COLLIDERS) { Logging.Error("Parked Vehicle Colliders Full!"); return; }

            UpdateParkedVehicleCollidersInGridSpace(gridZ * 540 + gridX + offsetX, ref colliderCounter);

            if (colliderCounter >= NUM_PARKED_VEHICLE_COLLIDERS) { Logging.Error("Parked Vehicle Colliders Full!"); return; }

            UpdateParkedVehicleCollidersInGridSpace((gridZ + offsetZ) * 540 + gridX, ref colliderCounter);

            if (colliderCounter >= NUM_PARKED_VEHICLE_COLLIDERS) { Logging.Error("Parked Vehicle Colliders Full!"); return; }

            UpdateParkedVehicleCollidersInGridSpace((gridZ + offsetZ) * 540 + gridX + offsetX, ref colliderCounter);

            if (colliderCounter >= NUM_PARKED_VEHICLE_COLLIDERS) { Logging.Error("Parked Vehicle Colliders Full!"); return; }

            for (int i = colliderCounter; i < NUM_PARKED_VEHICLE_COLLIDERS; i++)
            {
                m_ParkedVehicleColliders[i].gameObject.SetActive(false);
            }
        }

        private void UpdateParkedVehicleCollidersInGridSpace(int index, ref int colliderCounter)
        {
            ushort vehicleId = Singleton<VehicleManager>.instance.m_parkedGrid[index];

            while (vehicleId != 0 && colliderCounter < NUM_PARKED_VEHICLE_COLLIDERS)
            {
                SetVehicleCollider(vehicleId, colliderCounter, true);
                vehicleId = Singleton<VehicleManager>.instance.m_parkedVehicles.m_buffer[vehicleId].m_nextGridParked;
                colliderCounter++;
            }
        }

        public IEnumerator DisableColliders()
        {
            for (int i = 0; i < NUM_BUILDING_COLLIDERS; i++)
            {
                m_BuildingColliders[i].gameObject.SetActive(false);
            }

            for (int i = 0; i < NUM_VEHICLE_COLLIDERS; i++)
            {
                m_VehicleColliders[i].gameObject.SetActive(false);
            }

            for (int i = 0; i < NUM_PARKED_VEHICLE_COLLIDERS; i++)
            {
                m_ParkedVehicleColliders[i].gameObject.SetActive(false);
            }
            yield return null;
        }
        public void DestroyColliders()
        {
            foreach (var collider in m_BuildingColliders)
            {
                if (collider != null && collider.gameObject != null)
                {
                    Object.Destroy(collider.gameObject);
                }
            }

            foreach (var collider in m_VehicleColliders)
            {
                if (collider != null && collider.gameObject != null)
                {
                    Object.Destroy(collider.gameObject);
                }
            }

            foreach (var collider in m_ParkedVehicleColliders)
            {
                if (collider != null && collider.gameObject != null)
                {
                    Object.Destroy(collider.gameObject);
                }
            }
        }
    }
}
