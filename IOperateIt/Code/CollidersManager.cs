using AlgernonCommons;
using ColossalFramework;
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
        private const int NUM_VEHICLE_COLLIDERS = 32;
        private const int NUM_PARKED_VEHICLE_COLLIDERS = 32;
        private const float SCAN_DISTANCE = 50f;
        private const float COLLIDER_JUMP_DISTANCE = 10f;

        private ColliderContainer[] _BuildingColliders;
        private ColliderContainer[] _VehicleColliders;
        private ColliderContainer[] _ParkedVehicleColliders;
        /// <summary>
        /// Set up colliders for nearby buildings and vehicles.
        /// </summary>
        public IEnumerator InitializeColliders()
        {
            _BuildingColliders = new ColliderContainer[NUM_BUILDING_COLLIDERS];
            _VehicleColliders = new ColliderContainer[NUM_VEHICLE_COLLIDERS];
            _ParkedVehicleColliders = new ColliderContainer[NUM_PARKED_VEHICLE_COLLIDERS];

            PhysicMaterial material = new PhysicMaterial();
            material.bounciness = 0.05f;
            material.staticFriction = 0.1f;

            for (int i = 0; i < NUM_BUILDING_COLLIDERS; i++)
            {
                GameObject gameObject = new GameObject("BuildingCollider" + i);
                var buildingCollider = gameObject.AddComponent<ColliderContainer>();
                buildingCollider.MeshCollider = gameObject.AddComponent<MeshCollider>();
                buildingCollider.MeshCollider.convex = false;
                buildingCollider.MeshCollider.enabled = true;
                buildingCollider.MeshCollider.sharedMaterial = material;
                _BuildingColliders[i] = buildingCollider;
                gameObject.SetActive(false);
            }

            for (int i = 0; i < NUM_VEHICLE_COLLIDERS; i++)
            {
                GameObject gameObject = new GameObject("VehicleCollider" + i);
                var vehicleCollider = gameObject.AddComponent<ColliderContainer>();
                vehicleCollider.BoxCollider = gameObject.AddComponent<BoxCollider>();
                vehicleCollider.BoxCollider.enabled = true;
                vehicleCollider.BoxCollider.sharedMaterial = material;
                vehicleCollider.Rigidbody = gameObject.AddComponent<Rigidbody>();
                vehicleCollider.Rigidbody.isKinematic = true;
                vehicleCollider.Rigidbody.interpolation = RigidbodyInterpolation.Interpolate;
                vehicleCollider.Type = ColliderContainer.ContainerType.TYPE_VEHICLE;
                _VehicleColliders[i] = vehicleCollider;
                gameObject.SetActive(false);
            }

            for (int i = 0; i < NUM_PARKED_VEHICLE_COLLIDERS; i++)
            {
                GameObject gameObject = new GameObject("ParkedVehicleCollider" + i);
                var parkedVehicleCollider = gameObject.AddComponent<ColliderContainer>();
                parkedVehicleCollider.BoxCollider = gameObject.AddComponent<BoxCollider>();
                parkedVehicleCollider.BoxCollider.enabled = true;
                parkedVehicleCollider.BoxCollider.sharedMaterial = material;
                _ParkedVehicleColliders[i] = parkedVehicleCollider;
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
                vehicleRotation = parkedVehicle.m_rotation;
                vehiclePosition = parkedVehicle.m_position;
                _ParkedVehicleColliders[colliderIndex].gameObject.SetActive(true);
                _ParkedVehicleColliders[colliderIndex].BoxCollider.size = parkedVehicle.Info.m_mesh.bounds.size;
                _ParkedVehicleColliders[colliderIndex].gameObject.transform.position = vehiclePosition;
                _ParkedVehicleColliders[colliderIndex].gameObject.transform.rotation = vehicleRotation;
            }
            else
            {
                ref var vehicle = ref Singleton<VehicleManager>.instance.m_vehicles.m_buffer[vehicleId];
                vehicle.GetSmoothPosition(vehicleId, out vehiclePosition, out vehicleRotation);

                if (vehicleId == _VehicleColliders[colliderIndex].ID 
                    && Vector3.Magnitude(vehiclePosition - _VehicleColliders[colliderIndex].Rigidbody.transform.position) < COLLIDER_JUMP_DISTANCE)
                {
                    _VehicleColliders[colliderIndex].Rigidbody.MovePosition(vehiclePosition);
                    _VehicleColliders[colliderIndex].Rigidbody.MoveRotation(vehicleRotation);
                }
                else
                {
                    _VehicleColliders[colliderIndex].Rigidbody.transform.position = vehiclePosition;
                    _VehicleColliders[colliderIndex].Rigidbody.transform.rotation = vehicleRotation;
                }

                _VehicleColliders[colliderIndex].gameObject.SetActive(true);
                _VehicleColliders[colliderIndex].ID = vehicleId;
                _VehicleColliders[colliderIndex].BoxCollider.size = vehicle.Info.m_lodMesh.bounds.size;
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

                        _BuildingColliders[i].gameObject.SetActive(true);
                        _BuildingColliders[i].MeshCollider.sharedMesh = building.Info.m_mesh;
                        _BuildingColliders[i].gameObject.transform.position = buildingPosition;
                        _BuildingColliders[i].gameObject.transform.rotation = buildingRotation;
                        hitBuildings.Add(buildingIndex);
                    }
                }
            }
        }


        private void UpdateVehicleColliders(Transform transform)
        {
            int gridX = Mathf.Clamp((int)(transform.position.x / 32f + 270f), 0, 539);
            int gridZ = Mathf.Clamp((int)(transform.position.z / 32f + 270f), 0, 539);
            int offsetX = (transform.position.x / 32f + 270f) - (int)(transform.position.x / 32f + 270f) > 0.5 ? 1 : -1;
            int offsetZ = (transform.position.z / 32f + 270f) - (int)(transform.position.z / 32f + 270f) > 0.5 ? 1 : -1;
            int colliderCounter = 0;
           
            UpdateVehicleCollidersInGridSpace(gridZ * 540 + gridX, ref colliderCounter);

            if (colliderCounter >= NUM_VEHICLE_COLLIDERS) { Logging.Error("Vehicle Colliders Full!"); return; }

            UpdateVehicleCollidersInGridSpace(gridZ * 540 + gridX + offsetX, ref colliderCounter);

            if (colliderCounter >= NUM_VEHICLE_COLLIDERS) { Logging.Error("Vehicle Colliders Full!"); return; }

            UpdateVehicleCollidersInGridSpace((gridZ + offsetZ) * 540 + gridX, ref colliderCounter);

            if (colliderCounter >= NUM_VEHICLE_COLLIDERS) { Logging.Error("Vehicle Colliders Full!"); return; }

            UpdateVehicleCollidersInGridSpace((gridZ + offsetZ) * 540 + gridX + offsetX, ref colliderCounter);

            if (colliderCounter >= NUM_VEHICLE_COLLIDERS) { Logging.Error("Vehicle Colliders Full!"); return; }

            for (int i = colliderCounter; i < NUM_VEHICLE_COLLIDERS; i++)
            {
                _VehicleColliders[i].gameObject.SetActive(false);
            }
        }

        private void UpdateVehicleCollidersInGridSpace(int index, ref int colliderCounter)
        {
            ushort vehicleId = Singleton<VehicleManager>.instance.m_vehicleGrid[index];
            if (vehicleId != 0)
            {
                SetVehicleCollider(vehicleId, colliderCounter);
                colliderCounter++;
                var vehicle = Singleton<VehicleManager>.instance.m_vehicles.m_buffer[vehicleId];

                while (vehicle.m_nextGridVehicle != 0 && colliderCounter < NUM_VEHICLE_COLLIDERS)
                {
                    SetVehicleCollider(vehicle.m_nextGridVehicle, colliderCounter);
                    vehicle = Singleton<VehicleManager>.instance.m_vehicles.m_buffer[vehicle.m_nextGridVehicle];
                    colliderCounter++;
                }
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
                _ParkedVehicleColliders[i].gameObject.SetActive(false);
            }
        }

        private void UpdateParkedVehicleCollidersInGridSpace(int index, ref int colliderCounter)
        {
            ushort vehicleId = Singleton<VehicleManager>.instance.m_parkedGrid[index];
            if (vehicleId != 0)
            {
                SetVehicleCollider(vehicleId, colliderCounter, true);
                colliderCounter++;
                var parkedVehicle = Singleton<VehicleManager>.instance.m_parkedVehicles.m_buffer[vehicleId];

                while (parkedVehicle.m_nextGridParked != 0 && colliderCounter < NUM_PARKED_VEHICLE_COLLIDERS)
                {
                    SetVehicleCollider(parkedVehicle.m_nextGridParked, colliderCounter, true);
                    parkedVehicle = Singleton<VehicleManager>.instance.m_parkedVehicles.m_buffer[parkedVehicle.m_nextGridParked];
                    colliderCounter++;
                }
            }
        }

        public IEnumerator DisableColliders()
        {
            for (int i = 0; i < NUM_BUILDING_COLLIDERS; i++)
            {
                _BuildingColliders[i].gameObject.SetActive(false);
            }

            for (int i = 0; i < NUM_VEHICLE_COLLIDERS; i++)
            {
                _VehicleColliders[i].gameObject.SetActive(false);
            }

            for (int i = 0; i < NUM_PARKED_VEHICLE_COLLIDERS; i++)
            {
                _ParkedVehicleColliders[i].gameObject.SetActive(false);
            }
            yield return null;
        }
        public void DestroyColliders()
        {
            foreach (var collider in _BuildingColliders)
            {
                if (collider != null && collider.gameObject != null)
                {
                    Object.Destroy(collider.gameObject);
                }
            }

            foreach (var collider in _VehicleColliders)
            {
                if (collider != null && collider.gameObject != null)
                {
                    Object.Destroy(collider.gameObject);
                }
            }

            foreach (var collider in _ParkedVehicleColliders)
            {
                if (collider != null && collider.gameObject != null)
                {
                    Object.Destroy(collider.gameObject);
                }
            }
        }
    }
}
