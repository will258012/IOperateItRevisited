using ColossalFramework;
using ColossalFramework.Math;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Text;
using UDriveIt.Manager;
using UnityEngine;

namespace UDriveIt
{
    class ColliderHolder
    {
        public BoxCollider collider;
        public GameObject colliderOwner;
    }
    
    public class AxleCollider
    {
        public GameObject leftWheelObj;
        public GameObject rightWheelObj;
        public WheelCollider leftWheel;
        public WheelCollider rightWheel;

        public AxleCollider( GameObject leftWheelObj, GameObject rightWheelObj, WheelCollider leftWheel, WheelCollider rightWheel)
        {
            this.leftWheelObj = leftWheelObj;
            this.rightWheelObj = rightWheelObj;
            this.leftWheel = leftWheel;
            this.rightWheel = rightWheel;
        }
    }

    public class VehicleControler : MonoBehaviour
    {
        private bool active;
        GameObject vehicle;
        GameObject wheelHolder;

        Rigidbody vehicleRigidBody;
        MeshCollider vehicleCollider;
        BuildingManager buildingManager;
        Dictionary<ushort, ColliderHolder> mBuildingColliders = new Dictionary<ushort, ColliderHolder>(30);
        Dictionary<ushort, ColliderHolder> mRoadCollider = new Dictionary<ushort, ColliderHolder>(30);
        List<AxleCollider> wheels = new List<AxleCollider>();

        void Update()
        {

        }

        public void setActive(Vector3 position, VehicleInfo vehicleInfo)
        {
            if (!this.active)
            {
                this.active = true;
            }
            spawnVehicle(position, vehicleInfo);
            updateRoadColliders(position);
        }

        private void updateRoadColliders(Vector3 position)
        {
            NetManager manager = Singleton<NetManager>.instance;
            ushort[] closeSegments = new ushort[30];
            int closeSegmentCount;
            manager.GetClosestSegments(position, closeSegments, out closeSegmentCount);

            foreach (ushort segment in closeSegments)
            {
                if( !mRoadCollider.ContainsKey(segment))
                {
                    NetSegment netSegment = manager.m_segments.m_buffer[segment];
                    ColliderHolder holder = new ColliderHolder();
                    holder.colliderOwner = new GameObject("Segment" + segment);
                    holder.collider = holder.colliderOwner.AddComponent<BoxCollider>();
                    holder.collider.center = netSegment.m_middlePosition;
                    holder.collider.size = new Vector3(netSegment.m_bounds.size.x, 0.01f, netSegment.m_bounds.size.z);
                    holder.collider.transform.rotation = netSegment.Info.transform.rotation;
                    mRoadCollider[segment] = holder;
                }
            }
           /* Segment3 ray = new Segment3(gameObject.transform.position + new Vector3(0f, 1.5f, 0f), gameObject.transform.position + new Vector3(0f, -100f, 0f));
            if(manager.RayCast(ray, 0f, ItemClass.Service.Road, 
                               ItemClass.Service.PublicTransport,
                               ItemClass.SubService.None,
                               ItemClass.SubService.None,
                               ItemClass.Layer.Default,
                               ItemClass.Layer.None,
                               NetNode.Flags.None,
                               NetSegment.Flags.None,
                               out hitPos,
                               out nodeIndex,
                               out segmentIndex)){}*/

        }

        private void spawnVehicle(Vector3 position, VehicleInfo vehicleInfo)
        {
            if (vehicle != null)
            {
                unSpawnVehicle();
            }

            this.vehicle = new GameObject("UDIVehicle");
            this.vehicle.transform.parent = (Transform)null;
            this.vehicle.transform.position = position + new Vector3(0, 5f, 0);
            this.vehicle.AddComponent<MeshFilter>().mesh = vehicleInfo.m_mesh;
            this.vehicle.AddComponent<MeshRenderer>().material = vehicleInfo.m_material;
            this.vehicle.SetActive(true);
            this.vehicleRigidBody = this.vehicle.AddComponent<Rigidbody>();
            this.vehicleRigidBody.useGravity = true;
            this.vehicleRigidBody.mass = 2000f;
            this.vehicleRigidBody.angularDrag = 1.5f;
            this.vehicleRigidBody.drag = 0.0f;
            this.vehicleRigidBody.inertiaTensor = new Vector3(1f, 1f, 1f);
            this.vehicleRigidBody.velocity = this.vehicle.transform.forward * 0f;
            this.vehicleCollider = this.vehicle.AddComponent<MeshCollider>();
            this.vehicleCollider.convex = true;
            this.vehicleCollider.enabled = true;
            this.vehicle.transform.LookAt(position, Vector3.up);
            this.vehicle.transform.Rotate(180f, 0f, 0f);
            //this.vehicleRigidBody.centerOfMass = new Vector3(this.vehicleCollider.bounds.center.x + this.vehicleCollider.bounds.extents.x / 2, -2f, this.vehicleCollider.bounds.center.z);

            Vector3 vehicleExtents = this.vehicleCollider.bounds.extents;
            this.wheelHolder = new GameObject("WheelHolder");
            this.wheelHolder.transform.parent = this.vehicle.transform;
            GameObject flwObj = new GameObject("WheelFL");
            WheelCollider flWheel = flwObj.AddComponent<WheelCollider>();
            flwObj.transform.parent = this.wheelHolder.transform;
            flwObj.transform.position = new Vector3(-vehicleExtents.x, 0, vehicleExtents.z);
            flWheel.enabled = true;
            GameObject frwObj = new GameObject("WheelFR");
            WheelCollider frWheel = frwObj.AddComponent<WheelCollider>();
            frwObj.transform.parent = this.wheelHolder.transform;
            frwObj.transform.position = new Vector3(vehicleExtents.x, 0, vehicleExtents.z);
            frWheel.enabled = true;
            wheels.Add(new AxleCollider(flwObj, frwObj, flWheel, frWheel));

            GameObject blwObj = new GameObject("WheelBL");
            WheelCollider blWheel = blwObj.AddComponent<WheelCollider>();
            blwObj.transform.parent = this.wheelHolder.transform;
            blwObj.transform.position = new Vector3(-vehicleExtents.x, 0, -vehicleExtents.z);
            blWheel.enabled = true;
            GameObject brwObj = new GameObject("WheelBR");
            WheelCollider brWheel = brwObj.AddComponent<WheelCollider>();
            brwObj.transform.parent = this.wheelHolder.transform;
            brwObj.transform.position = new Vector3(vehicleExtents.x, 0, -vehicleExtents.z);
            brWheel.enabled = true;
            wheels.Add(new AxleCollider(blwObj, brwObj, blWheel, brWheel));
            /*
            this.wheels[0].rightWheelObj = new GameObject("WheelFR");
            this.wheels[0].rightWheelObj.transform.parent = this.wheelHolder.transform;
            this.wheels[0].rightWheelObj.transform.position = new Vector3(vehicleExtents.x, 0, vehicleExtents.z);
            this.wheels[0].rightWheel = this.wheels[0].rightWheelObj.AddComponent<WheelCollider>();
            this.wheels[0].rightWheel.enabled = true;

            this.wheels[1].leftWheelObj = new GameObject("WheelBL");
            this.wheels[1].leftWheelObj.transform.parent = this.wheelHolder.transform;
            this.wheels[1].leftWheelObj.transform.position = new Vector3(-vehicleExtents.x, 0, -vehicleExtents.z);
            this.wheels[1].leftWheel = this.wheels[1].leftWheelObj.AddComponent<WheelCollider>();
            this.wheels[1].leftWheel.enabled = true;

            this.wheels[1].rightWheelObj = new GameObject("WheelBR");
            this.wheels[1].rightWheelObj.transform.parent = this.wheelHolder.transform;
            this.wheels[1].rightWheelObj.transform.position = new Vector3(vehicleExtents.x, 0, -vehicleExtents.z);
            this.wheels[1].rightWheel = this.wheels[1].rightWheelObj.AddComponent<WheelCollider>();
            this.wheels[1].rightWheel.enabled = true;*/
        }

        public void setInactive()
        {
            if (this.active)
            {
                this.active = true;
                unSpawnVehicle();
            }
        }

        private void unSpawnVehicle()
        {
            UnityEngine.Object.Destroy(vehicle);
            this.vehicleRigidBody = null;
            this.vehicleCollider = null;
            wheels.Clear();
            this.vehicle = null;

        }

        private void LateUpdate()
        {

        }

        private void FixedUpdate()
        {
            if (this.vehicleRigidBody != null)
            {
                if(Input.GetKey(KeyCode.Keypad8))
                {

                    this.vehicleRigidBody.AddRelativeForce(Vector3.forward * 500f, ForceMode.Impulse);

                }
                if (Input.GetKey(KeyCode.Keypad2))
                {

                    this.vehicleRigidBody.AddRelativeForce(Vector3.back * 500f, ForceMode.Impulse);

                }
                if (Input.GetKey(KeyCode.Keypad4))
                {

                    this.vehicleRigidBody.AddRelativeTorque(Vector3.left * 500f, ForceMode.Impulse);

                }
                if (Input.GetKey(KeyCode.Keypad6))
                {

                    this.vehicleRigidBody.AddRelativeForce(Vector3.right * 500f, ForceMode.Impulse);

                }
                //this.vehicleRigidBody.AddRelativeForce(Vector3.forward * 1000f);
            }

        }
    }
}
