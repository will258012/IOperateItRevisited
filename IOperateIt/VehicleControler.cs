using ColossalFramework;
using ColossalFramework.Math;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Text;
using IOperateIt.Manager;
using IOperateIt.Utils;
using UnityEngine;

namespace IOperateIt
{
    class ColliderContainer
    {
        public GameObject colliderOwner;
        public MeshCollider collider;
    }

    enum CameraType
    {
        normal,
        closeUp,
        fps,
    }

    public class VehicleControler : MonoBehaviour
    {
        private bool active;
        Rigidbody vehicleRigidBody;
        Mesh vehicleMesh;
        BoxCollider vehicleCollider;
        BuildingManager buildingManager;
        NetManager netManager;
        TerrainManager terrainManager;
        OptionsManager optionsManager;

        float terrainHeight;
        private Vector3 m_velocity;

        private CameraController cameraController;
        private Camera camera;
        private CameraType cameraType = CameraType.normal;
        private float fpsCameraOffsetXAxis = 2.75f;
        private float fpsCameraOffsetYAxis = 1.5f;
        private Vector3 lookAtVector = Vector3.forward;

        private readonly int NUM_BUILDING_COLLIDERS = 360/10;
        private readonly float SCAN_DISTANCE = 500f;
        private ColliderContainer[] mBuildingColliders;

        void Awake()
        {
            netManager = Singleton<NetManager>.instance;
            terrainManager = Singleton<TerrainManager>.instance;

            cameraController = GameObject.FindObjectOfType<CameraController>();
            camera = cameraController.GetComponent<Camera>();

            buildingManager = Singleton<BuildingManager>.instance;
            optionsManager = OptionsManager.Instance();
            
            mBuildingColliders = new ColliderContainer[NUM_BUILDING_COLLIDERS];
            for(int i =0; i<NUM_BUILDING_COLLIDERS; i++)
            {
                ColliderContainer container = new ColliderContainer();
                container.colliderOwner = new GameObject();
                container.collider = container.colliderOwner.AddComponent<MeshCollider>();
                container.collider.convex = true;
                mBuildingColliders[i] = container;
            }
        }
        
        void Update()
        {
            if (this.active && Input.GetKey(KeyCode.F2))
            {
                setInactive();
            }

            if (Input.GetKeyDown(KeyCode.F3))
            {
                cameraType = (CameraType)(((int)cameraType + 1) % Enum.GetNames(typeof(CameraType)).Length);
            }


        }

        public void setActive(Vector3 position, VehicleInfo vehicleInfo, Vector3 rotation)
        {
            if (!this.active)
            {
                this.active = true;
            }
            spawnVehicle(position, rotation, vehicleInfo);
            updateRoadColliders(position);
            cameraController.enabled = false;
            camera.fieldOfView = 45f;
            camera.nearClipPlane = 0.1f;
        }

        private void updateRoadColliders(Vector3 position)
        {

        }

        private void spawnVehicle(Vector3 position,Vector3 rotation, VehicleInfo vehicleInfo)
        {
            NetManager manager = Singleton<NetManager>.instance;
            TerrainManager terrainManager = Singleton<TerrainManager>.instance;
            Vector3[] hitPos = new Vector3[2];
            ushort nodeIndex;
            ushort segmentIndex;
            bool rayCastSuccess;

            gameObject.transform.parent = (Transform)null;
            gameObject.transform.position = position;
            gameObject.transform.eulerAngles = new Vector3(rotation.x, rotation.y);
            vehicleMesh = vehicleInfo.m_mesh;
            gameObject.AddComponent<MeshFilter>().mesh = vehicleMesh;
            gameObject.AddComponent<MeshRenderer>().material = vehicleInfo.m_material;
            gameObject.SetActive(true);
            this.vehicleRigidBody = gameObject.AddComponent<Rigidbody>();
            this.vehicleRigidBody.isKinematic = false;
            this.vehicleRigidBody.useGravity = false;
            this.vehicleRigidBody.drag = 1.75f;
            this.vehicleRigidBody.angularDrag = 2.5f;
            this.vehicleRigidBody.freezeRotation = true;
            this.vehicleCollider = gameObject.AddComponent<BoxCollider>();

            Segment3 ray = new Segment3(position + new Vector3(0f, 1.5f, 0f), position + new Vector3(0f, -100f, 0f));
            rayCastSuccess = manager.RayCast(ray, 0f, ItemClass.Service.Road, ItemClass.Service.PublicTransport, ItemClass.SubService.None, ItemClass.SubService.None, ItemClass.Layer.Default, ItemClass.Layer.None, NetNode.Flags.None, NetSegment.Flags.None, out hitPos[0], out nodeIndex, out segmentIndex);
            rayCastSuccess = rayCastSuccess || manager.RayCast(ray, 0f, ItemClass.Service.Beautification, ItemClass.Service.Water, ItemClass.SubService.None, ItemClass.SubService.None, ItemClass.Layer.Default, ItemClass.Layer.None, NetNode.Flags.None, NetSegment.Flags.None, out hitPos[1], out nodeIndex, out segmentIndex);
            terrainHeight = terrainManager.SampleDetailHeight(transform.position);
            LoggerUtils.Log(string.Format("{0},{1},{2},{3}", hitPos[0], hitPos[1], terrainHeight, position));
            terrainHeight = Mathf.Max(terrainHeight, Mathf.Max(hitPos[0].y, hitPos[1].y));

            if (position.y < terrainHeight + 1 && m_velocity.y <= 0)
            {
                transform.position = new Vector3(position.x, terrainHeight, position.z);
                m_velocity.y = Math.Max(0, m_velocity.y);
            }
            //calculateSlope();

        }

        void OnCollisionEnter(Collision col)
        {
        }

        public void setInactive()
        {
            if (this.active)
            {
                this.active = false;
                cameraController.enabled = true;
                unSpawnVehicle();
            }
        }

        private void unSpawnVehicle()
        {
            UnityEngine.Object.Destroy(gameObject);
        }

        private void LateUpdate()
        {

        }

        private void updateBuildingColliders()
        {
            Vector3 segmentVector;
            Segment3 circularScanSegment;
            ushort buildingIndex;
            Vector3 hitPos = Vector3.zero;
            HashSet<ushort> hitBuildings = new HashSet<ushort>();
            Building building;
            Vector3 buildingPosition;
            Quaternion buildingRotation;

            for (int i = 0; i < NUM_BUILDING_COLLIDERS; i++)
            {
                float x  = (float)Math.Cos(Mathf.Deg2Rad * (10f*i ));
                float z = (float)Math.Sin(Mathf.Deg2Rad * (10f * i));
                segmentVector = new Vector3(x, 1f, z) * SCAN_DISTANCE;
                circularScanSegment = new Segment3(transform.position,
                                                   transform.position + segmentVector);

                buildingManager.RayCast(circularScanSegment, ItemClass.Service.None, ItemClass.SubService.None, ItemClass.Layer.Default, Building.Flags.None, out hitPos, out buildingIndex);
                if (hitPos != Vector3.zero)
                {
                    building = buildingManager.m_buildings.m_buffer[buildingIndex];
                    if (!hitBuildings.Contains(buildingIndex) && building.Info.m_class.m_service != ItemClass.Service.Road &&
                        building.Info.name != "478820060.CableStay32m_Data" && building.Info.name != "BridgePillar.CableStay32m_Data")
                    {
                        building.CalculateMeshPosition(out buildingPosition, out buildingRotation);
                        mBuildingColliders[i].collider.sharedMesh = building.Info.m_mesh;
                        mBuildingColliders[i].colliderOwner.transform.position = buildingPosition;
                        mBuildingColliders[i].colliderOwner.transform.rotation = buildingRotation;
                        hitBuildings.Add(buildingIndex);
                    }
                }
            }
        }

        private void calculateSlope()
        {
            Vector3[] hitPos = new Vector3[2];

            Bounds meshBounds = gameObject.GetComponent<MeshFilter>().mesh.bounds;
            Vector3 vehicleRotation = this.vehicleRigidBody.velocity;
            Segment3 ray = new Segment3(transform.position + new Vector3(0f, 5f, 0f), transform.position + new Vector3(0f, -100f, 0f));
            Segment3 ray2 = new Segment3(transform.position + vehicleRotation.normalized + new Vector3(0f, 5f, 0f),  transform.position + vehicleRotation.normalized + new Vector3(0f, -100f, 0f));

            ushort nodeIndex;
            ushort segmentIndex;

            bool rayCast1Success = netManager.RayCast(ray, 0f, ItemClass.Service.Road, ItemClass.Service.PublicTransport, ItemClass.SubService.None, ItemClass.SubService.None, ItemClass.Layer.Default, ItemClass.Layer.None, NetNode.Flags.None, NetSegment.Flags.None, out hitPos[0], out nodeIndex, out segmentIndex);
            bool rayCast2Success = netManager.RayCast(ray2, 0f, ItemClass.Service.Road, ItemClass.Service.PublicTransport, ItemClass.SubService.None, ItemClass.SubService.None, ItemClass.Layer.Default, ItemClass.Layer.None, NetNode.Flags.None, NetSegment.Flags.None, out hitPos[1], out nodeIndex, out segmentIndex);
            //LoggerUtils.Log(string.Format("{0}",vehicleRotation.magnitude));

            if (rayCast1Success && rayCast2Success && vehicleRotation.magnitude > 7)
            {
                Vector3 diff = hitPos[1] - hitPos[0];
                float angle = Mathf.Atan2(diff.y, diff.x) * Mathf.Rad2Deg;
                //LoggerUtils.Log(string.Format("hitPos[1] {0}, hitPos[0] {1}, angle:{2}", hitPos[1], hitPos[0], angle));
                if (Math.Abs(angle) >= 4f)
                {
                    transform.eulerAngles = new Vector3(-angle,transform.eulerAngles.y,0 );
                }
                else
                {
                    transform.eulerAngles = new Vector3(0, transform.eulerAngles.y, 0);
                }
            }
        }

        private void FixedUpdate()
        {

            if (this.vehicleRigidBody != null)
            {
               
                Vector3[] hitPos = new Vector3[4];
                ushort nodeIndex;
                ushort segmentIndex;
                bool rayCastSuccess;

                updateBuildingColliders();

                //calculateSlope();

                Segment3 ray = new Segment3(transform.position + new Vector3(0f, 1.5f, 0f), transform.position + new Vector3(0f, -100f, 0f));

                rayCastSuccess = netManager.RayCast(ray, 0f, ItemClass.Service.Road, ItemClass.Service.PublicTransport, ItemClass.SubService.None, ItemClass.SubService.None, ItemClass.Layer.Default, ItemClass.Layer.None, NetNode.Flags.None, NetSegment.Flags.None, out hitPos[0], out nodeIndex, out segmentIndex);
                rayCastSuccess = rayCastSuccess || netManager.RayCast(ray, 0f, ItemClass.Service.Beautification, ItemClass.Service.Water, ItemClass.SubService.None, ItemClass.SubService.None, ItemClass.Layer.Default, ItemClass.Layer.None, NetNode.Flags.None, NetSegment.Flags.None, out hitPos[1], out nodeIndex, out segmentIndex);
                terrainHeight = terrainManager.SampleDetailHeight(transform.position);
                terrainHeight = Mathf.Max(terrainHeight, Mathf.Max(hitPos[0].y, hitPos[1].y));

                if (transform.position.y < terrainHeight + 1 && m_velocity.y <= 0)
                {
                    this.vehicleRigidBody.velocity = new Vector3(this.vehicleRigidBody.velocity.x, 0, this.vehicleRigidBody.velocity.z);
                    transform.position = new Vector3(transform.position.x, terrainHeight, transform.position.z);
                }
                else
                {
                    this.vehicleRigidBody.AddRelativeForce(Physics.gravity*2f);
                }

                if (Input.GetKey(optionsManager.forwardKey))
                {
                    this.vehicleRigidBody.AddRelativeForce(Vector3.forward * optionsManager.mAccelerationForce * Time.fixedDeltaTime, ForceMode.VelocityChange);

                }
                else if (Input.GetKey(optionsManager.backKey))
                {
                    this.vehicleRigidBody.AddRelativeForce(Vector3.back * optionsManager.mAccelerationForce * Time.fixedDeltaTime, ForceMode.VelocityChange);
                }
                if (Input.GetKey(optionsManager.leftKey))
                {
                    Vector3 normalisedVelocity = this.vehicleRigidBody.velocity.normalized;
                    Vector3 brakeVelocity = normalisedVelocity * optionsManager.mBreakingForce*0.58f;
                    this.vehicleRigidBody.AddForce(-brakeVelocity);

                    this.vehicleRigidBody.AddRelativeTorque(Vector3.down * 5f * Time.fixedDeltaTime, ForceMode.VelocityChange);
                }
                if (Input.GetKey(optionsManager.rightKey))
                {
                    Vector3 normalisedVelocity = this.vehicleRigidBody.velocity.normalized;
                    Vector3 brakeVelocity = normalisedVelocity * optionsManager.mBreakingForce * 0.58f; 
                    this.vehicleRigidBody.AddForce(-brakeVelocity);

                    this.vehicleRigidBody.AddRelativeTorque(Vector3.up * 5f * Time.fixedDeltaTime, ForceMode.VelocityChange);
                }


                if (this.vehicleRigidBody.velocity.sqrMagnitude > optionsManager.mMaxVelocitySquared)
                {
                    Vector3 normalisedVelocity = this.vehicleRigidBody.velocity.normalized;
                    Vector3 brakeVelocity = normalisedVelocity * optionsManager.mAccelerationForce;  
                    this.vehicleRigidBody.AddForce(-brakeVelocity);
                }

                switch (cameraType)
                {
                    case CameraType.closeUp:
                        Vector3 needPos = transform.position + (transform.forward * (optionsManager.mcloseupXAxisOffset - vehicleMesh.bounds.size.x))+ (transform.up * (optionsManager.mcloseupYAxisOffset + vehicleMesh.bounds.size.y));
                        camera.transform.position = needPos;
                        camera.transform.LookAt(transform);
                        camera.transform.rotation = transform.rotation;
                        break;
                    case CameraType.fps:
                        Vector3 forward = transform.rotation * Vector3.forward;
                        Vector3 up = transform.rotation * Vector3.up;

                        var pos = transform.position + (transform.forward * fpsCameraOffsetXAxis) + (transform.up * fpsCameraOffsetYAxis);
                        camera.transform.position = pos;
                        Vector3 lookAt = pos + (transform.rotation * Vector3.forward) * 1.0f;

                        var currentOrientation = camera.transform.rotation;
                        camera.transform.LookAt(lookAt, Vector3.up);
                        camera.transform.rotation = Quaternion.Slerp(currentOrientation, camera.transform.rotation,
                            Time.deltaTime * 3.0f);
                        break;
                    case CameraType.normal:
                    default:
                        camera.transform.position = transform.position + new Vector3(optionsManager.mcameraXAxisOffset, optionsManager.mcameraYAxisOffset, 0);
                        camera.transform.LookAt(transform);
                        break;
                }
            }

        }
    }
}
