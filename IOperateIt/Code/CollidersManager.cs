using ColossalFramework.Math;
using IOperateIt.Settings;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace IOperateIt
{
    public class ColliderContainer
    {
        public GameObject ColliderOwner;
        public MeshCollider MeshCollider;
        public BoxCollider BoxCollider;
    }
    public class CollidersManager
    {
        private const int NUM_BUILDING_COLLIDERS = 36;
        private const int NUM_VEHICLE_COLLIDERS = 160;
        private const int NUM_PARKED_VEHICLE_COLLIDERS = 80;
        private const float SCAN_DISTANCE = 50f;

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

            for (int i = 0; i < NUM_BUILDING_COLLIDERS; i++)
            {
                var buildingCollider = new ColliderContainer
                {
                    ColliderOwner = new GameObject("BuildingCollider" + i)
                };
                buildingCollider.MeshCollider = buildingCollider.ColliderOwner.AddComponent<MeshCollider>();
                buildingCollider.MeshCollider.convex = false;
                buildingCollider.MeshCollider.enabled = true;
                _BuildingColliders[i] = buildingCollider;
                _BuildingColliders[i].ColliderOwner.SetActive(false);
            }

            for (int i = 0; i < NUM_VEHICLE_COLLIDERS; i++)
            {
                var vehicleCollider = new ColliderContainer
                {
                    ColliderOwner = new GameObject("VehicleCollider" + i)
                };
                vehicleCollider.BoxCollider = vehicleCollider.ColliderOwner.AddComponent<BoxCollider>();
                vehicleCollider.BoxCollider.enabled = true;
                _VehicleColliders[i] = vehicleCollider;
                _VehicleColliders[i].ColliderOwner.SetActive(false);
            }

            for (int i = 0; i < NUM_PARKED_VEHICLE_COLLIDERS; i++)
            {
                var parkedVehicleCollider = new ColliderContainer
                {
                    ColliderOwner = new GameObject("ParkedVehicleCollider" + i)
                };
                parkedVehicleCollider.BoxCollider = parkedVehicleCollider.ColliderOwner.AddComponent<BoxCollider>();
                parkedVehicleCollider.BoxCollider.enabled = true;
                _ParkedVehicleColliders[i] = parkedVehicleCollider;
                _ParkedVehicleColliders[i].ColliderOwner.SetActive(false);
            }
            yield return null;
        }
        private void SetVehicleCollider(ushort vehicleId, int colliderIndex, bool isParked = false)
        {
            Vector3 vehiclePosition;
            Quaternion vehicleRotation;
            if (isParked)
            {
                var parkedVehicle = VehicleManager.instance.m_parkedVehicles.m_buffer[vehicleId];
                vehicleRotation = parkedVehicle.m_rotation;
                vehiclePosition = parkedVehicle.m_position;
                _ParkedVehicleColliders[colliderIndex].ColliderOwner.SetActive(true);
                _ParkedVehicleColliders[colliderIndex].BoxCollider.size = parkedVehicle.Info.m_mesh.bounds.size + new Vector3(0, 2.6f, 0);
                _ParkedVehicleColliders[colliderIndex].ColliderOwner.transform.position = vehiclePosition;
                _ParkedVehicleColliders[colliderIndex].ColliderOwner.transform.rotation = vehicleRotation;
            }
            else
            {
                var vehicle = VehicleManager.instance.m_vehicles.m_buffer[vehicleId];
                vehicle.GetSmoothPosition(vehicleId, out vehiclePosition, out vehicleRotation);
                // add an arbitrary height to make collider work better
                _VehicleColliders[colliderIndex].ColliderOwner.SetActive(true);
                _VehicleColliders[colliderIndex].BoxCollider.size = vehicle.Info.m_lodMesh.bounds.size + new Vector3(0, 1.3f, 0);
                _VehicleColliders[colliderIndex].ColliderOwner.transform.position = vehiclePosition;
                _VehicleColliders[colliderIndex].ColliderOwner.transform.rotation = vehicleRotation;
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
            Segment3 circularScanSegment;
            ushort buildingIndex;
            var hitPos = Vector3.zero;
            HashSet<ushort> hitBuildings = new HashSet<ushort>();
            Building building;
            Vector3 buildingPosition;
            Quaternion buildingRotation;

            for (int i = 0; i < NUM_BUILDING_COLLIDERS; i++)
            {
                float x = (float)Mathf.Cos(Mathf.Deg2Rad * (360f / NUM_BUILDING_COLLIDERS * i));
                float z = (float)Mathf.Sin(Mathf.Deg2Rad * (360f / NUM_BUILDING_COLLIDERS * i));
                segmentVector = new Vector3(x, 1f, z) * SCAN_DISTANCE;
                circularScanSegment = new Segment3(transform.position,
                                                   transform.position + segmentVector);

                BuildingManager.instance.RayCast(circularScanSegment, ItemClass.Service.None, ItemClass.SubService.None, ItemClass.Layer.Default, Building.Flags.None, out hitPos, out buildingIndex);
                if (hitPos != Vector3.zero)
                {
                    building = BuildingManager.instance.m_buildings.m_buffer[buildingIndex];
                    if (!hitBuildings.Contains(buildingIndex) &&
                        building.Info.name != "478820060.CableStay32m_Data" && building.Info.name != "BridgePillar.CableStay32m_Data")
                    {
                        building.CalculateMeshPosition(out buildingPosition, out buildingRotation);

                        if (building.Info.m_class.m_service == ItemClass.Service.Road)
                        {
                            buildingPosition.y -= 1.0f;
                        }

                        _BuildingColliders[i].ColliderOwner.SetActive(true);
                        _BuildingColliders[i].MeshCollider.sharedMesh = building.Info.m_mesh;
                        _BuildingColliders[i].ColliderOwner.transform.position = buildingPosition;
                        _BuildingColliders[i].ColliderOwner.transform.rotation = buildingRotation;
                        hitBuildings.Add(buildingIndex);
                    }
                }
            }
        }


        private void UpdateVehicleColliders(Transform transform)
        {
            int gridX = Mathf.Clamp((int)(transform.position.x / 32.0 + 270.0), 0, 539);
            int gridZ = Mathf.Clamp((int)(transform.position.z / 32.0 + 270.0), 0, 539);
            int colliderCounter = 0;

            Vehicle vehicle;

            int index = gridZ * 540 + gridX;
            ushort vehicleId = VehicleManager.instance.m_vehicleGrid[index];
            if (vehicleId != 0)
            {
                SetVehicleCollider(vehicleId, colliderCounter);
                colliderCounter++;
                vehicle = VehicleManager.instance.m_vehicles.m_buffer[vehicleId];

                while (vehicle.m_nextGridVehicle != 0 && colliderCounter < NUM_VEHICLE_COLLIDERS)
                {
                    SetVehicleCollider(vehicle.m_nextGridVehicle, colliderCounter);
                    vehicle = VehicleManager.instance.m_vehicles.m_buffer[vehicle.m_nextGridVehicle];
                    colliderCounter++;
                }
            }
            for (int i = colliderCounter; i < NUM_VEHICLE_COLLIDERS; i++)
            {
                _VehicleColliders[i].ColliderOwner.SetActive(false);
                //_VehicleColliders[colliderCounter].BoxCollider.transform.position = new Vector3(float.PositiveInfinity, float.PositiveInfinity);
            }

        }

        private void UpdateParkedVehicleColliders(Transform transform)
        {
            int gridX = Mathf.Clamp((int)(transform.position.x / 32.0 + 270.0), 0, 539);
            int gridZ = Mathf.Clamp((int)(transform.position.z / 32.0 + 270.0), 0, 539);
            int colliderCounter = 0;

            VehicleParked parkedVehicle;

            int index = gridZ * 540 + gridX;
            ushort vehicleId = VehicleManager.instance.m_parkedGrid[index];
            if (vehicleId != 0)
            {
                SetVehicleCollider(vehicleId, colliderCounter, true);
                colliderCounter++;
                parkedVehicle = VehicleManager.instance.m_parkedVehicles.m_buffer[vehicleId];

                while (parkedVehicle.m_nextGridParked != 0 && colliderCounter < NUM_PARKED_VEHICLE_COLLIDERS)
                {
                    SetVehicleCollider(parkedVehicle.m_nextGridParked, colliderCounter, true);
                    parkedVehicle = VehicleManager.instance.m_parkedVehicles.m_buffer[parkedVehicle.m_nextGridParked];
                    colliderCounter++;
                }
            }
            for (int i = colliderCounter; i < NUM_PARKED_VEHICLE_COLLIDERS; i++)
            {
                _ParkedVehicleColliders[i].ColliderOwner.SetActive(false);
                //_VehicleColliders[colliderCounter].BoxCollider.transform.position = new Vector3(float.PositiveInfinity, float.PositiveInfinity);
            }
        }



        public IEnumerator DisableColliders()
        {
            for (int i = 0; i < NUM_BUILDING_COLLIDERS; i++)
            {
                _BuildingColliders[i].ColliderOwner.SetActive(false);
            }

            for (int i = 0; i < NUM_VEHICLE_COLLIDERS; i++)
            {
                _VehicleColliders[i].ColliderOwner.SetActive(false);
            }

            for (int i = 0; i < NUM_PARKED_VEHICLE_COLLIDERS; i++)
            {
                _ParkedVehicleColliders[i].ColliderOwner.SetActive(false);
            }
            yield return null;
        }
        public void DestroyColliders()
        {
            foreach (var collider in _BuildingColliders)
            {
                if (collider != null && collider.ColliderOwner != null)
                {
                    Object.Destroy(collider.ColliderOwner);
                }
            }

            foreach (var collider in _VehicleColliders)
            {
                if (collider != null && collider.ColliderOwner != null)
                {
                    Object.Destroy(collider.ColliderOwner);
                }
            }

            foreach (var collider in _ParkedVehicleColliders)
            {
                if (collider != null && collider.ColliderOwner != null)
                {
                    Object.Destroy(collider.ColliderOwner);
                }
            }
        }
    }
}
