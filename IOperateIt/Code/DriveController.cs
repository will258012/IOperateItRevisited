extern alias FPSCamera;

using AlgernonCommons;
using ColossalFramework;
using FPSCamera.FPSCamera.Cam.Controller;
using FPSCamera.FPSCamera.Game;
using FPSCamera.FPSCamera.Utils;
using IOperateIt.Settings;
using System.Collections.Generic;
using UnityEngine;
using FPCModSettings = FPSCamera.FPSCamera.Settings.ModSettings;
namespace IOperateIt
{
    public class DriveController : MonoBehaviour
    {
        private const float THROTTLE_RESP = 0.5f;
        private const float STEER_RESP = 0.25f;
        private const float STEER_MAX = 30.0f * Mathf.PI / 180.0f;
        private const float ROAD_RAYCAST_UPPER = 2.0f;
        private const float ROAD_RAYCAST_LOWER = -7.5f;
        private const float WALL_HEIGHT = 0.5f;
        private const float UNDERGROUND_RENDER_BIAS = 1.0f;
        private const float GRIP = 23.0f;
        private const float LIGHT_HEADLIGHT_INTENSITY = 5.0f;
        private const float LIGHT_BRAKELIGHT_INTENSITY = 0.5f;

        private struct NetInfoBackup
        {
            public NetInfoBackup(NetInfo.Node[] nodes, NetInfo.Segment[] segments)
            {
                this.nodes = nodes;
                this.segments = segments;
            }
            
            public NetInfo.Node[]      nodes;
            public NetInfo.Segment[]   segments;
        }

        public static DriveController instance { get; private set; }

        private Rigidbody       m_vehicleRigidBody;
        private BoxCollider     m_vehicleCollider;
        private Color           m_vehicleColor;
        private bool            m_setColor;
        private VehicleInfo     m_vehicleInfo;

        private List<LightEffect>   m_lightEffects      = new List<LightEffect>();
        private List<EffectInfo>    m_regularEffects    = new List<EffectInfo>();
        private List<EffectInfo>    m_specialEffects    = new List<EffectInfo>();

        private Dictionary<string, string> m_customTunnelMappings = new Dictionary<string, string>();
        private Dictionary<NetInfo, NetInfoBackup> m_backupPrefabData = new Dictionary<NetInfo, NetInfoBackup>();
        private Material m_backupMaterial = null;

        private CollidersManager m_collidersManager = new CollidersManager();
        private Vector3 m_prevPosition;
        private Vector3 m_prevVelocity;
        private Vector3 m_lastValidCameraVector = Vector3.forward;
        private Vector4 m_lightState;
        private Quaternion m_rotationOffset;
        private bool m_isSirenEnabled = true;
        private bool m_isLightEnabled = false;
        private bool m_isBraking = false;
        private int m_renderMask = 0;

        private float m_terrainHeight;
        private float m_halfLength = 0.0f;
        private float m_halfWidth = 0.0f;
        private float m_distanceTravelled = 0.0f;
        private float m_steer = 0f;
        private float m_throttle = 0f;
        internal float m_speed => m_vehicleRigidBody.velocity.magnitude;
        private void Awake()
        {
            instance = this;
            gameObject.AddComponent<MeshFilter>();
            gameObject.AddComponent<MeshRenderer>();
            gameObject.GetComponent<MeshRenderer>().enabled = true;

            m_vehicleRigidBody = gameObject.AddComponent<Rigidbody>();
            m_vehicleRigidBody.isKinematic = false;
            m_vehicleRigidBody.useGravity = true;
            m_vehicleRigidBody.freezeRotation = false;
            m_vehicleRigidBody.drag = 0.1f;
            m_vehicleRigidBody.angularDrag = 0.1f;
            m_vehicleRigidBody.mass = 1000f;
            m_vehicleRigidBody.interpolation = RigidbodyInterpolation.Interpolate;
            
            PhysicMaterial material = new PhysicMaterial();
            material.bounciness = 0.05f;
            material.staticFriction = 0.1f;

            m_vehicleCollider = gameObject.AddComponent<BoxCollider>();
            m_vehicleCollider.material = material;

            StartCoroutine(m_collidersManager.InitializeColliders());
            gameObject.SetActive(false);
            enabled = false;

            // Some tunnel names are atypical and need to be manually mapped.
            m_customTunnelMappings["HighwayRamp Tunnel"]                        = "HighwayRampElevated";
            m_customTunnelMappings["Metro Track"]                               = "Metro Track Elevated 01";
            m_customTunnelMappings["Metro Station Track"]                       = "Metro Station Track Elevated 01";
            m_customTunnelMappings["Large Oneway Road Tunnel"]                  = "Large Oneway Elevated";
            m_customTunnelMappings["Metro Station Below Ground Bypass"]         = "Metro Station Track Elevated Bypass";
            m_customTunnelMappings["Metro Station Below Ground Dual Island"]    = "Metro Station Track Elevated Dual Island";
            m_customTunnelMappings["Metro Station Below Ground Island"]         = "Metro Station Track Elevated Island Platform";

        }
        private void Update()
        {
            HandleInputOnUpdate();
            UpdateCameraPos();
            PlayEffects();

            MaterialPropertyBlock materialBlock = Singleton<VehicleManager>.instance.m_materialBlock;
            materialBlock.Clear();
            //materialBlock.SetMatrix(Singleton<VehicleManager>.instance.ID_TyreMatrix, value);
            Vector4 tyrePosition = default;
            tyrePosition.x = m_steer * Mathf.PI / 6.0f;
            tyrePosition.y = m_distanceTravelled;
            tyrePosition.z = 0f;
            tyrePosition.w = 0f;
            materialBlock.SetVector(Singleton<VehicleManager>.instance.ID_TyrePosition, tyrePosition);

            m_lightState.x = m_isLightEnabled ? LIGHT_HEADLIGHT_INTENSITY : 0.0f;
            m_lightState.y = m_isBraking ? LIGHT_BRAKELIGHT_INTENSITY : 0.0f;
            materialBlock.SetVector(Singleton<VehicleManager>.instance.ID_LightState, m_lightState);
            if (m_setColor)
            {
                materialBlock.SetColor(Singleton<VehicleManager>.instance.ID_Color, m_vehicleColor);
            }
            gameObject.GetComponent<MeshRenderer>().SetPropertyBlock(materialBlock);
        }
        private void FixedUpdate()
        {
            HandleInputOnFixedUpdate();

            var relativeVel = m_vehicleRigidBody.transform.InverseTransformDirection(m_vehicleRigidBody.velocity);

            if (relativeVel.z == 0f || m_throttle * relativeVel.z < 0f)
            {
                m_isBraking = true;
            }
            else
            {
                m_isBraking = false;
            }

            m_vehicleRigidBody.AddRelativeForce(Vector3.forward * m_throttle * (m_isBraking ? ModSettings.BreakingForce : ModSettings.AccelerationForce), ForceMode.Force);

            relativeVel.z = 0.0f;
            relativeVel.y = 0.0f;

            relativeVel = Mathf.Min(Vector3.Magnitude(relativeVel) * 0.99f, GRIP * Time.fixedDeltaTime) * Vector3.Normalize(relativeVel);

            m_vehicleRigidBody.AddRelativeForce(-relativeVel, ForceMode.VelocityChange);

            var invert = Vector3.Dot(Vector3.forward, m_vehicleRigidBody.transform.InverseTransformDirection(m_vehicleRigidBody.velocity)) > 0.0f ? 1.0f : -1.0f;
            var speedsteer = Mathf.Min(Mathf.Max(m_speed * 20f, 0f), 60f);
            speedsteer = Mathf.Sign(m_steer) * Mathf.Min(Mathf.Abs(60f * m_steer), speedsteer);

            var angularTarget = 0.99f * (Vector3.up * invert * speedsteer * Time.fixedDeltaTime) - m_vehicleRigidBody.transform.InverseTransformDirection(m_vehicleRigidBody.angularVelocity);

            m_vehicleRigidBody.AddRelativeTorque(angularTarget, ForceMode.VelocityChange);

            m_collidersManager.UpdateColliders(m_vehicleRigidBody.transform);

            m_terrainHeight = CalculateHeight(m_vehicleRigidBody.transform.position);

            if (m_vehicleRigidBody.transform.position.y < m_terrainHeight)
            {
                m_vehicleRigidBody.velocity = new Vector3(m_vehicleRigidBody.velocity.x, 0f, m_vehicleRigidBody.velocity.z);
                m_vehicleRigidBody.transform.position = new Vector3(m_vehicleRigidBody.transform.position.x, 
                                                                    0.5f * m_terrainHeight + 0.5f * m_vehicleRigidBody.transform.position.y, 
                                                                    m_vehicleRigidBody.transform.position.z);
            }

            if (m_vehicleRigidBody.transform.position.y + WALL_HEIGHT < m_terrainHeight)
            {
                m_vehicleRigidBody.transform.position = m_prevPosition;
                m_vehicleRigidBody.velocity = Vector3.zero;
            }

            CalculateSlope();

            LimitVelocity();

            UpdateCameraRendering();

            m_distanceTravelled += invert * Vector3.Magnitude(m_vehicleRigidBody.transform.position - m_prevPosition);

            m_prevVelocity = m_vehicleRigidBody.velocity;
            m_prevPosition = m_vehicleRigidBody.transform.position;
        }
        private void LateUpdate()
        {
        }
        private void OnDestroy()
        {
            m_collidersManager.DestroyColliders();
        }

        private void LimitVelocity()
        {
            if (m_speed > ModSettings.MaxVelocity.FromKmph())
            {
                m_vehicleRigidBody.AddForce(m_vehicleRigidBody.velocity.normalized * ModSettings.MaxVelocity.FromKmph() - m_vehicleRigidBody.velocity, ForceMode.VelocityChange);
            }
        }

        private void OnCollisionEnter(Collision collision)
        {
            LimitVelocity();

            ColliderContainer container = collision.collider.gameObject.GetComponent<ColliderContainer>();
            if (container.Type == ColliderContainer.ContainerType.TYPE_VEHICLE)
            {
                ref Vehicle otherVehicle = ref Singleton<VehicleManager>.instance.m_vehicles.m_buffer[container.ID];

                ref Vector3 otherVelocity = ref (otherVehicle.m_lastFrame <= 1 ? 
                    ref (otherVehicle.m_lastFrame == 0 ? ref otherVehicle.m_frame0.m_velocity : ref otherVehicle.m_frame1.m_velocity) : 
                    ref (otherVehicle.m_lastFrame == 2 ? ref otherVehicle.m_frame2.m_velocity : ref otherVehicle.m_frame3.m_velocity));

                float collisionOrientation = Vector3.Dot(Vector3.Normalize(m_vehicleRigidBody.position - collision.collider.transform.position), Vector3.Normalize(otherVelocity));

                otherVelocity = otherVelocity * 0.8f * (0.5f + (-collisionOrientation + 1.0f) * 0.25f);
            }
        }

        private void OnCollisionStay(Collision collision)
        {
            LimitVelocity();
        }

        private void OnCollisionExit(Collision collision)
        {
            LimitVelocity();
        }

        public void updateColor(Color color, bool enable)
        {
            m_vehicleColor = color; 
            m_setColor = enable;
        }

        public void updateVehicleInfo(VehicleInfo info)
        {
            m_vehicleInfo = info; 
        }

        public bool isVehicleInfoSet()
        {
            return m_vehicleInfo != null;
        }

        public void StartDriving(Vector3 position, Quaternion rotation) => StartDriving(position, rotation, m_vehicleInfo, m_vehicleColor, m_setColor);
        public void StartDriving(Vector3 position, Quaternion rotation, VehicleInfo vehicleInfo, Color vehicleColor, bool setColor)
        {
            enabled = true;
            SpawnVehicle(position, rotation, vehicleInfo, vehicleColor, setColor);
            OverridePrefabs();
            FPSCamController.Instance.FPSCam = new DriveCam();
            FPSCamController.Instance.EnableCam(true);
            m_renderMask = Singleton<RenderManager>.instance.CurrentCameraInfo.m_camera.cullingMask;
        }
        public void StopDriving()
        {
            StartCoroutine(m_collidersManager.DisableColliders());

            Singleton<RenderManager>.instance.CurrentCameraInfo.m_camera.cullingMask = m_renderMask;
            RestorePrefabs();
            DestroyVehicle();
            enabled = false;
        }
        private void SpawnVehicle(Vector3 position, Quaternion rotation, VehicleInfo vehicleInfo, Color vehicleColor, bool setColor)
        {
            m_setColor = setColor;
            m_vehicleColor = vehicleColor;
            m_vehicleColor.a = 0; // Make sure blinking is not set.
            m_vehicleInfo = vehicleInfo;
            m_lightState = Vector4.zero;

            m_vehicleRigidBody.transform.position = position;
            m_vehicleRigidBody.transform.rotation = rotation; 
            m_vehicleRigidBody.velocity = Vector3.zero;
            
            var vehicleMesh = m_vehicleInfo.m_mesh;
            m_vehicleInfo.CalculateGeneratedInfo();
            gameObject.GetComponent<MeshFilter>().mesh = gameObject.GetComponent<MeshFilter>().sharedMesh = vehicleMesh;
            gameObject.GetComponent<MeshRenderer>().material = gameObject.GetComponent<MeshRenderer>().sharedMaterial = m_vehicleInfo.m_material;
            gameObject.GetComponent<MeshRenderer>().sortingLayerID = m_vehicleInfo.m_prefabDataLayer;

            if (m_setColor)
            {
                MaterialPropertyBlock materialBlock = Singleton<VehicleManager>.instance.m_materialBlock;
                materialBlock.Clear();
                materialBlock.SetColor(Singleton<VehicleManager>.instance.ID_Color, m_vehicleColor);
                gameObject.GetComponent<MeshRenderer>().SetPropertyBlock(materialBlock);
            }

            gameObject.SetActive(true);
            m_vehicleCollider.size = vehicleMesh.bounds.size;
            m_halfWidth = vehicleMesh.bounds.size.x;
            m_halfLength = vehicleMesh.bounds.size.z;
            m_rotationOffset = Quaternion.identity;

            AddEffects();
        }
        private void DestroyVehicle()
        {
            RemoveEffects();
            gameObject.SetActive(false);

            m_setColor = false;
            m_vehicleColor = default;
            m_vehicleInfo = null;
            m_lightState = Vector4.zero;
            m_halfWidth = 0f;
            m_halfLength = 0f;
            m_rotationOffset = Quaternion.identity;
        }

        private void OverridePrefabs()
        {
            int undergroudLayer = LayerMask.NameToLayer("MetroTunnels"); // underground render layer
            int roadLayer = LayerMask.NameToLayer("Road"); // road render layer

            for (uint prefabIndex = 0; prefabIndex < PrefabCollection<VehicleInfo>.PrefabCount(); prefabIndex++)
            {
                VehicleInfo prefabVehicleInfo = PrefabCollection<VehicleInfo>.GetPrefab(prefabIndex);
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

            for (uint prefabIndex = 0; prefabIndex < prefabCount; prefabIndex++)
            {
                NetInfo prefabNetInfo = PrefabCollection<NetInfo>.GetPrefab(prefabIndex);
                NetInfo prefabReplaceInfo = null;

                if (prefabNetInfo.m_class.m_layer == ItemClass.Layer.MetroTunnels)
                {
                    string replaceName;
                    if (!m_customTunnelMappings.TryGetValue(prefabNetInfo.name, out replaceName))
                    {
                        replaceName = prefabNetInfo.name.Replace(" Tunnel", " Elevated");
                    }

                    for (uint otherPrefabIndex = 0; otherPrefabIndex < prefabCount; otherPrefabIndex++)
                    {
                        NetInfo tmpInfo = PrefabCollection<NetInfo>.GetPrefab(otherPrefabIndex);
                        if (tmpInfo.m_class.m_layer == ItemClass.Layer.Default && tmpInfo.name == replaceName)
                        {
                            prefabReplaceInfo = tmpInfo;
                            break;
                        }
                    }

                    if (prefabReplaceInfo != null)
                    {
                        m_backupPrefabData[prefabNetInfo] = new NetInfoBackup(prefabNetInfo.m_nodes, prefabNetInfo.m_segments);

                        NetInfo.Segment[] segments = new NetInfo.Segment[prefabReplaceInfo.m_segments.Length];

                        for (int index = 0; index < prefabReplaceInfo.m_segments.Length; index++)
                        {
                            NetInfo.Segment newSegment = CopySegment(prefabReplaceInfo.m_segments[index]);
                            newSegment.m_layer = undergroudLayer;
                            segments[index] = newSegment;
                        }

                        NetInfo.Node[] nodes = new NetInfo.Node[prefabReplaceInfo.m_nodes.Length];

                        for (int index = 0; index < prefabReplaceInfo.m_nodes.Length; index++)
                        {
                            NetInfo.Node newNode = CopyNode(prefabReplaceInfo.m_nodes[index]);
                            newNode.m_layer = undergroudLayer;
                            newNode.m_flagsForbidden = newNode.m_flagsForbidden & ~NetNode.Flags.Underground;
                            nodes[index] = newNode;
                        }

                        prefabNetInfo.m_segments = segments;
                        prefabNetInfo.m_nodes = nodes;
                    }
                    else
                    {
                        Logging.Error("Failed to replace " + prefabNetInfo.name + " with " + replaceName);
                    }
                }
                else if (prefabNetInfo.name.Contains("Slope")) // only slope components have underground transition elements
                {
                    m_backupPrefabData[prefabNetInfo] = new NetInfoBackup(prefabNetInfo.m_nodes, prefabNetInfo.m_segments);

                    NetInfo.Segment[] segments = new NetInfo.Segment[prefabNetInfo.m_segments.Length];

                    for (int index = 0; index < prefabNetInfo.m_segments.Length; index++)
                    {
                        NetInfo.Segment currSegment = prefabNetInfo.m_segments[index];
                        if (currSegment.m_layer == undergroudLayer)
                        {
                            // disable slope underground component from rendering.
                            NetInfo.Segment newSegment = CopySegment(currSegment);
                            newSegment.m_forwardForbidden = NetSegment.Flags.All;
                            newSegment.m_forwardRequired = NetSegment.Flags.None;
                            newSegment.m_backwardForbidden = NetSegment.Flags.All;
                            newSegment.m_backwardRequired = NetSegment.Flags.None;
                            segments[index] = newSegment;
                        }
                        else
                        {
                            segments[index] = currSegment;
                        }
                    }

                    NetInfo.Node[] nodes = new NetInfo.Node[prefabNetInfo.m_nodes.Length];

                    for (int index = 0; index < prefabNetInfo.m_nodes.Length; index++)
                    {
                        NetInfo.Node newNode = CopyNode(prefabNetInfo.m_nodes[index]);
                        if (newNode.m_layer == undergroudLayer)
                        {
                            newNode.m_flagsForbidden = NetNode.Flags.All;
                            newNode.m_flagsRequired = NetNode.Flags.None;
                            nodes[index] = newNode;
                        }
                        else
                        {
                            newNode.m_flagsForbidden = newNode.m_flagsForbidden & ~NetNode.Flags.Underground;
                            nodes[index] = newNode;
                        }
                    }

                    prefabNetInfo.m_segments = segments;
                    prefabNetInfo.m_nodes = nodes;
                }
            }

            RenderManager rm = Singleton<RenderManager>.instance;
            m_backupMaterial = rm.m_groupLayerMaterials[undergroudLayer];
            rm.m_groupLayerMaterials[undergroudLayer] = rm.m_groupLayerMaterials[roadLayer];
            rm.UpdateGroups(undergroudLayer);
        }

        private void RestorePrefabs()
        {
            int undergroudLayer = LayerMask.NameToLayer("MetroTunnels"); // underground render layer

            for (uint iter = 0; iter < PrefabCollection<VehicleInfo>.PrefabCount(); iter++)
            {
                VehicleInfo prefabVehicleInfo = PrefabCollection<VehicleInfo>.GetPrefab(iter);
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

            int prefabCount = PrefabCollection<NetInfo>.PrefabCount();
            for (uint prefabIndex = 0; prefabIndex < prefabCount; prefabIndex++)
            {
                NetInfo prefabNetInfo = PrefabCollection<NetInfo>.GetPrefab(prefabIndex);

                if (prefabNetInfo.m_class.m_layer == ItemClass.Layer.MetroTunnels || prefabNetInfo.name.Contains("Slope"))
                {
                    if (m_backupPrefabData.TryGetValue(prefabNetInfo, out NetInfoBackup backupData))
                    {
                        prefabNetInfo.m_segments = backupData.segments;
                        prefabNetInfo.m_nodes = backupData.nodes;

                        m_backupPrefabData.Remove(prefabNetInfo);
                    }
                }
            }

            m_backupPrefabData.Clear();

            RenderManager rm = Singleton<RenderManager>.instance;
            rm.m_groupLayerMaterials[undergroudLayer] = m_backupMaterial;
            m_backupMaterial = null;
            rm.UpdateGroups(undergroudLayer);
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

        //private static NetInfo.LodValue CopyLodValue(NetInfo.LodValue value)
        //{
        //    NetInfo.LodValue retval = new NetInfo.LodValue();
        //    retval.m_key = value.m_key;
        //    retval.m_material = value.m_material;
        //    retval.m_lodMin = value.m_lodMin;
        //    retval.m_lodMax = value.m_lodMax;
        //    retval.m_surfaceTexA = value.m_surfaceTexA;
        //    retval.m_surfaceTexB = value.m_surfaceTexB;
        //    retval.m_surfaceMapping = value.m_surfaceMapping;
        //    retval.m_heightMap = value.m_heightMap;
        //    retval.m_heightMapping = value.m_heightMapping;

        //    return retval;
        //}
        private void CalculateSlope()
        {
            Vector3 diffVector = m_vehicleRigidBody.transform.position - m_prevPosition;
            Vector3 horizontalDirection = new Vector3(diffVector.x, 0f, diffVector.z);
            float heightDifference = diffVector.y;

            if (horizontalDirection.sqrMagnitude > 0.001f)
            {
                float slopeAngle = Mathf.Atan2(heightDifference, horizontalDirection.magnitude) * Mathf.Rad2Deg;//+: upslope -: downslope
                slopeAngle = Mathf.Clamp(slopeAngle, -90f, 90f);

                bool isReversing = Vector3.Dot(diffVector.normalized, m_vehicleRigidBody.transform.forward) < 0;

                if (isReversing)
                    slopeAngle = -slopeAngle;

                var targetRotation = Quaternion.Euler(
                    -slopeAngle,
                    m_vehicleRigidBody.transform.rotation.eulerAngles.y,
                    0f
                );

                m_vehicleRigidBody.transform.rotation = Quaternion.Slerp(
                    m_vehicleRigidBody.transform.rotation,
                    targetRotation,
                    Time.deltaTime * 6f
                );
            }
        }

        private float CalculateHeight(Vector3 position)
        {
            bool roadFound = false;
            ToolBase.RaycastInput input;
            ToolBase.RaycastOutput output;
            Vector3 roadPos;

            var height = Mathf.Max(MapUtils.GetTerrainLevel(position), Singleton<TerrainManager>.instance.WaterLevel(new Vector2(position.x, position.z)));

            input = MapUtils.RayCastTool.GetRaycastInput(position, ROAD_RAYCAST_LOWER, ROAD_RAYCAST_UPPER); // Configure raycast input parameters
            input.m_netService.m_service = ItemClass.Service.Road;
            input.m_netService.m_itemLayers = ItemClass.Layer.Default |// ItemClass.Layer.PublicTransport is sonly for TransportLine, not for Road.
                                              ItemClass.Layer.MetroTunnels;
            input.m_netService2.m_service = ItemClass.Service.Beautification; // For paths

            input.m_ignoreSegmentFlags = NetSegment.Flags.Deleted |
                                         NetSegment.Flags.Collapsed |
                                         NetSegment.Flags.Flooded;
            input.m_ignoreTerrain = true;

            // Perform the raycast and check for a result:
            if (MapUtils.RayCastTool.RayCast(input, out output, 0f))
            {
                height = Mathf.Max(height, output.m_hitPos.y);
                roadFound = true;
            }

            // If no result, change the service to ItemClass.Service.PublicTransport (for tracks).
            if (!roadFound)
            {
                input.m_netService.m_service = ItemClass.Service.PublicTransport;

                // Perform the raycast again:
                if (MapUtils.RayCastTool.RayCast(input, out output, 0f))
                {
                    height = Mathf.Max(height, output.m_hitPos.y);
                    roadFound = true;
                }
            }

            // If a road was found, try to find a lane and find the precise height.
            if (roadFound)
            {
                if (output.m_netSegment != 0)
                {
                    float offset = 0f;
                    ref NetSegment segment = ref Singleton<NetManager>.instance.m_segments.m_buffer[output.m_netSegment];

                    if (GetClosestLanePositionFiltered(ref segment, m_vehicleRigidBody.transform.position, out roadPos, out offset))
                    {
                        height = roadPos.y;

                        if (offset == 0f || offset == 1f)
                        {
                            ref NetNode node = ref Singleton<NetManager>.instance.m_nodes.m_buffer[offset == 0f ? segment.m_startNode : segment.m_endNode];
                            if (node.CountSegments() > 1)
                            {
                                GetClosestLanePositionOnNodeFiltered(ref node, output.m_netSegment, ref roadPos);
                                height = roadPos.y;
                            }
                        }
                    }
                }
            }

            return height;
        }

        private void GetClosestLanePositionOnNodeFiltered(ref NetNode node, ushort currSegmentId, ref Vector3 currClosest)
        {
            int iter = 0;
            float currClosestDist = Vector3.Magnitude(currClosest - m_vehicleRigidBody.transform.position);
            while (iter < 8) // Cities only supports 8 segments per node.
            {
                ushort altSegmentId = node.GetSegment(iter);
                if (altSegmentId != 0 && altSegmentId != currSegmentId)
                {
                    ref NetSegment tmpSegment = ref Singleton<NetManager>.instance.m_segments.m_buffer[altSegmentId];
                    Vector3 roadPos;
                    float offset;
                    if (GetClosestLanePositionFiltered(ref tmpSegment, m_vehicleRigidBody.transform.position, out roadPos, out offset))
                    {
                        if (offset != 0 && offset != 1)
                        {
                            currClosest = roadPos;
                            return;
                        }

                        float tmpDist = Vector3.Magnitude(roadPos - m_vehicleRigidBody.transform.position);
                        if (tmpDist < currClosestDist)
                        {
                            currClosestDist = tmpDist;
                            currClosest = roadPos;
                        }
                    }
                }
                iter++;
            }
        }

        private bool GetClosestLanePositionFiltered(ref NetSegment segmentIn, Vector3 posIn, out Vector3 posOut, out float offsetOut)
        {
            uint lane = segmentIn.m_lanes;
            float dist = 10000.0f;
            bool found = false;
            int index = 0;
            NetInfo.LaneType type;

            posOut = Vector3.zero;
            offsetOut = -1f;
            
            while (lane != 0)
            {
                type = segmentIn.Info.m_lanes[index].m_laneType;

                if (type != NetInfo.LaneType.None)
                {
                    Singleton<NetManager>.instance.m_lanes.m_buffer[lane].GetClosestPosition(m_vehicleRigidBody.transform.position, out var posTmp, out var offsetTmp);
                    if ((offsetTmp != 0f && offsetTmp != 1f) || ((type & NetInfo.LaneType.Pedestrian) == 0))
                    {
                        float distTmp = Vector3.Magnitude(posTmp - posIn);
                        if (distTmp < dist)
                        {
                            dist = distTmp;
                            posOut = posTmp;
                            offsetOut = offsetTmp;
                            found = true;
                        }
                    }
                }
                lane = Singleton<NetManager>.instance.m_lanes.m_buffer[lane].m_nextLane;
                index++;
            }
            return found;
        }

        private void HandleInputOnFixedUpdate()
        {
            bool throttling = false;
            if (FPCModSettings.Instance.XMLKeyMoveForward.IsPressed())
            {
                m_throttle = Mathf.Clamp(m_throttle + Time.fixedDeltaTime / THROTTLE_RESP, 0.0f, 1.0f);
                throttling = true;
            }
            if (FPCModSettings.Instance.XMLKeyMoveBackward.IsPressed())
            {
                m_throttle = Mathf.Clamp(m_throttle - Time.fixedDeltaTime / THROTTLE_RESP, -1.0f, 0.0f);
                throttling = true;
            }
            if (!throttling)
            {
                if (m_throttle > 0.0f)
                {
                    m_throttle = Mathf.Clamp(m_throttle - Time.fixedDeltaTime / THROTTLE_RESP, 0.0f, 1.0f);
                }
                else if (m_throttle < 0.0f)
                {
                    m_throttle = Mathf.Clamp(m_throttle + Time.fixedDeltaTime / THROTTLE_RESP, -1.0f, 0.0f);
                }
            }

            bool steering = false;
            if (FPCModSettings.Instance.XMLKeyMoveRight.IsPressed())
            {
                m_steer = Mathf.Clamp(m_steer + Time.fixedDeltaTime / STEER_RESP, -1.0f, 1.0f);
                steering = true;
            }
            if (FPCModSettings.Instance.XMLKeyMoveLeft.IsPressed())
            {
                m_steer = Mathf.Clamp(m_steer - Time.fixedDeltaTime / STEER_RESP, -1.0f, 1.0f);
                steering = true;
            }
            if (!steering)
            {
                if (m_steer > 0.0f)
                {
                    m_steer = Mathf.Clamp(m_steer - Time.fixedDeltaTime / STEER_RESP, 0.0f, 1.0f);
                }
                if (m_steer < 0.0f)
                {
                    m_steer = Mathf.Clamp(m_steer + Time.fixedDeltaTime / STEER_RESP, -1.0f, 0.0f);
                }
            }
        }

        private void HandleInputOnUpdate()
        {
            if (InputManager.KeyTriggered((KeyCode)ModSettings.KeyLightToggle.Key))
                m_isLightEnabled = !m_isLightEnabled;

            if (InputManager.KeyTriggered((KeyCode)ModSettings.KeySirenToggle.Key))
                m_isSirenEnabled = !m_isSirenEnabled;

            var cursorVisible =
             FPCModSettings.Instance.XMLKeyCursorToggle.IsPressed() ^ (FPCModSettings.Instance.XMLShowCursorFollow);
            InputManager.ToggleCursor(cursorVisible);

            if (InputManager.MouseTriggered(InputManager.MouseButton.Middle) ||
                InputManager.KeyTriggered(FPCModSettings.Instance.XMLKeyCamReset))
            {
                m_rotationOffset = Quaternion.identity;
            }
            float yawDegree = 0f, pitchDegree = 0f;
            { // key rotation
                var rotateFactor = FPCModSettings.Instance.XMLRotateKeyFactor * Time.deltaTime;

                if (FPCModSettings.Instance.XMLKeyRotateRight.IsPressed()) yawDegree += 1f * rotateFactor;
                if (FPCModSettings.Instance.XMLKeyRotateLeft.IsPressed()) yawDegree -= 1f * rotateFactor;
                if (FPCModSettings.Instance.XMLKeyRotateUp.IsPressed()) pitchDegree -= 1f * rotateFactor;
                if (FPCModSettings.Instance.XMLKeyRotateDown.IsPressed()) pitchDegree += 1f * rotateFactor;

                if (yawDegree == 0f && pitchDegree == 0f)
                {
                    // mouse rotation
                    const float mouseFactor = .2f;
                    yawDegree = InputManager.MouseMoveHori * FPCModSettings.Instance.XMLRotateSensitivity *
                                (FPCModSettings.Instance.XMLInvertRotateHorizontal ? -1f : 1f) * mouseFactor;
                    pitchDegree = InputManager.MouseMoveVert * FPCModSettings.Instance.XMLRotateSensitivity *
                                  (FPCModSettings.Instance.XMLInvertRotateVertical ? 1f : -1f) * mouseFactor;
                }
            }

            var yawRotation = Quaternion.Euler(0f, yawDegree, 0f);
            var pitchRotation = Quaternion.Euler(pitchDegree, 0f, 0f);
            m_rotationOffset = yawRotation * m_rotationOffset * pitchRotation;

            // Limit pitch
            var eulerAngles = m_rotationOffset.eulerAngles;
            if (eulerAngles.x > 180f) eulerAngles.x -= 360f;
            eulerAngles.x = eulerAngles.x.Clamp(ModSettings.Offset.z > -1f ? -FPCModSettings.Instance.XMLMaxPitchDeg : 0f, FPCModSettings.Instance.XMLMaxPitchDeg);
            eulerAngles.z = 0f;
            m_rotationOffset = Quaternion.Euler(eulerAngles);
        }
        private void AddEffects()
        {
            if (m_vehicleInfo.m_effects != null)
            {
                foreach (var effect in m_vehicleInfo.m_effects)
                {
                    {
                        if (effect.m_effect != null)
                        {
                            if (effect.m_vehicleFlagsRequired.IsFlagSet(Vehicle.Flags.Emergency1 | Vehicle.Flags.Emergency2))
                                m_specialEffects.Add(effect.m_effect);
                            else
                            {
                                if (effect.m_effect is MultiEffect multiEffect)
                                {
                                    foreach (var sub in multiEffect.m_effects)
                                        if (sub.m_effect is LightEffect lightEffect)
                                            m_lightEffects.Add(lightEffect);
                                }
                                m_regularEffects.Add(effect.m_effect);
                            }
                        }
                    }
                }
            }
        }
        private void PlayEffects()
        {
            var position = m_vehicleRigidBody.transform.position;
            var velocity = m_vehicleRigidBody.velocity;
            var acceleration = ((m_vehicleRigidBody.velocity - m_prevVelocity) / Time.fixedDeltaTime).magnitude;
            var swayPosition = Vector3.zero;
            var rotation = m_vehicleRigidBody.transform.rotation;
            var scale = Vector3.one;
            var matrix = m_vehicleInfo.m_vehicleAI.CalculateBodyMatrix(Vehicle.Flags.Created | Vehicle.Flags.Spawned, ref position, ref rotation, ref scale, ref swayPosition);
            var area = new EffectInfo.SpawnArea(matrix, m_vehicleInfo.m_lodMeshData);
            var listenerInfo = Singleton<AudioManager>.instance.CurrentListenerInfo;
            var audioGroup = Singleton<VehicleManager>.instance.m_audioGroup;
            RenderGroup.MeshData effectMeshData = m_vehicleInfo.m_vehicleAI.GetEffectMeshData();
            var area2 = new EffectInfo.SpawnArea(matrix, effectMeshData, m_vehicleInfo.m_generatedInfo.m_tyres, m_vehicleInfo.m_lightPositions);

            foreach (var regularEffect in m_regularEffects)
            {
                regularEffect.PlayEffect(default, area, velocity, acceleration, 1f, listenerInfo, audioGroup);
            }
            if (m_isLightEnabled)
                foreach (var light in m_lightEffects)
                {
                    light.RenderEffect(default, area2, velocity, acceleration, 1f, -1f, Singleton<SimulationManager>.instance.m_simulationTimeDelta, Singleton<RenderManager>.instance.CurrentCameraInfo);
                }

            if (m_isSirenEnabled)
                foreach (var specialEffect in m_specialEffects)
                {
                    specialEffect.RenderEffect(default, area2, velocity, acceleration, 1f, -1f, Singleton<SimulationManager>.instance.m_simulationTimeDelta, Singleton<RenderManager>.instance.CurrentCameraInfo);
                    specialEffect.PlayEffect(default, area, velocity, acceleration, 1f, listenerInfo, audioGroup);
                }
        }

        private void RemoveEffects()
        {
            m_lightEffects.Clear();
            m_regularEffects.Clear();
            m_specialEffects.Clear();
        }
        private void UpdateCameraPos()
        {
            var cameraTransform = GameCamController.Instance.MainCamera.transform;

            Vector3 vehiclePosition = m_vehicleRigidBody.transform.position;
            Vector3 vehicleDirection;
            if (m_speed < 1.0)
            {
                vehicleDirection = m_lastValidCameraVector;
            }
            else
            {
                vehicleDirection = Vector3.Normalize(m_vehicleRigidBody.velocity);
                if (vehicleDirection.y > 0.999f || vehicleDirection.y < -0.999f)
                {
                    vehicleDirection = Vector3.Normalize(m_vehicleRigidBody.transform.InverseTransformDirection(Vector3.forward));
                }
                if (vehicleDirection.y > 0.999f || vehicleDirection.y < -0.999f)
                {
                    vehicleDirection = m_lastValidCameraVector;
                }
                m_lastValidCameraVector = vehicleDirection;
            }
            Quaternion targetRotation = Quaternion.identity;
            targetRotation.SetLookRotation(vehicleDirection);
            targetRotation *= m_rotationOffset;

            // Apply the calculated position and rotation to the camera.
            if (FPCModSettings.Instance.XMLSmoothTransition)
            {
                targetRotation = Quaternion.Slerp(cameraTransform.rotation, targetRotation, Time.deltaTime * FPCModSettings.Instance.XMLTransSpeed);
            }

            cameraTransform.position = vehiclePosition + targetRotation * ModSettings.Offset;
            cameraTransform.rotation = targetRotation;

            // Limit the camera's position to the allowed area.
            cameraTransform.position = CameraController.ClampCameraPosition(cameraTransform.position);
        }

        private void UpdateCameraRendering()
        {
            var cameraTransform = GameCamController.Instance.MainCamera.transform;
            var terrainHeight = MapUtils.GetTerrainLevel(cameraTransform.position);
            var roadFound = MapUtils.GetClosestSegmentLevel(cameraTransform.position, out float roadHeight);
            if (!roadFound || roadHeight < terrainHeight + ROAD_RAYCAST_LOWER) {
                roadHeight = terrainHeight;
            }
            if (Mathf.Min(terrainHeight, roadHeight) + UNDERGROUND_RENDER_BIAS > cameraTransform.position.y)
            {
                Singleton<RenderManager>.instance.CurrentCameraInfo.m_camera.cullingMask |= (1 << Singleton<VehicleManager>.instance.m_undergroundLayer);
            }
            else
            {
                Singleton<RenderManager>.instance.CurrentCameraInfo.m_camera.cullingMask &= ~(1 << Singleton<VehicleManager>.instance.m_undergroundLayer);
            }
        }
    }
}