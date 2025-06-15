extern alias FPSCamera;

using AlgernonCommons;
using ColossalFramework;
using FPSCamera.FPSCamera.Cam.Controller;
using FPSCamera.FPSCamera.Game;
using FPSCamera.FPSCamera.Utils;
using IOperateIt.Settings;
using KianCommons;
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
        private const float GRIP = 23.0f;

        private float m_halfLength = 0.0f;
        private float m_halfWidth = 0.0f;
        private float m_distanceTravelled = 0.0f;
        
        public static DriveController instance { get; private set; }

        private Rigidbody       m_vehicleRigidBody;
        private BoxCollider     m_vehicleCollider;
        private Color           m_vehicleColor;
        private bool            m_setColor;
        private VehicleInfo     m_vehicleInfo;

        private List<LightEffect>   m_lightEffects      = new List<LightEffect>();
        private List<EffectInfo>    m_regularEffects    = new List<EffectInfo>();
        private List<EffectInfo>    m_specialEffects    = new List<EffectInfo>();

        private CollidersManager m_collidersManager = new CollidersManager();
        private float   m_terrainHeight;
        private Vector3 m_prevPosition;
        private Vector3 m_prevVelocity;
        private Vector3 m_lastValidCameraVector = Vector3.forward;
        private Vector4 m_lightState;
        private Quaternion m_rotationOffset;
        private bool m_isSirenEnabled = true;
        private bool m_isLightEnabled = false;
        private int m_renderMask = 0;

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

            m_lightState.x = m_isLightEnabled ? 1.0f : 0.0f;
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

            for (int iter = 0; iter < Singleton<VehicleManager>.instance.m_vehicleCount; iter++)
            {
                ref Vehicle vehicle = ref Singleton<VehicleManager>.instance.m_vehicles.m_buffer[iter];
                vehicle.Info.m_undergroundMaterial = vehicle.Info.m_material;
                vehicle.Info.m_undergroundLodMaterial = vehicle.Info.m_lodMaterial;
                //vehicle.Info.m_lodRenderDistance = 100;
            }

            m_vehicleRigidBody.AddRelativeForce(Vector3.forward * ModSettings.AccelerationForce * m_throttle, ForceMode.Force);

            var lateralVel = transform.InverseTransformDirection(m_vehicleRigidBody.velocity);
            lateralVel.z = 0.0f;
            lateralVel.y = 0.0f;

            lateralVel = Mathf.Min(Vector3.Magnitude(lateralVel) * 0.99f, GRIP * Time.fixedDeltaTime) * Vector3.Normalize(lateralVel);

            m_vehicleRigidBody.AddRelativeForce(-lateralVel, ForceMode.VelocityChange);

            var invert = Vector3.Dot(Vector3.forward, transform.InverseTransformDirection(m_vehicleRigidBody.velocity)) > 0.0f ? 1.0f : -1.0f;
            var speedsteer = Mathf.Min(Mathf.Max(m_speed * 20f, 0f), 60f);
            speedsteer = Mathf.Sign(m_steer) * Mathf.Min(Mathf.Abs(60f * m_steer), speedsteer);

            var angularTarget = 0.99f * (Vector3.up * invert * speedsteer * Time.fixedDeltaTime) - transform.InverseTransformDirection(m_vehicleRigidBody.angularVelocity);

            m_vehicleRigidBody.AddRelativeTorque(angularTarget, ForceMode.VelocityChange);

            m_collidersManager.UpdateColliders(transform);

            m_terrainHeight = CalculateHeight(transform.position);

            if (transform.position.y < m_terrainHeight)
            {
                m_vehicleRigidBody.velocity = new Vector3(m_vehicleRigidBody.velocity.x, 0f, m_vehicleRigidBody.velocity.z);
                transform.position = new Vector3(transform.position.x, 0.5f * m_terrainHeight + 0.5f * transform.position.y, transform.position.z);
            }

            if (transform.position.y + WALL_HEIGHT < m_terrainHeight)
            {
                transform.position = m_prevPosition;
                m_vehicleRigidBody.velocity = Vector3.zero;
            }

            CalculateSlope();

            LimitVelocity();

            UpdateCameraRendering();

            m_distanceTravelled += invert * Vector3.Magnitude(transform.position - m_prevPosition);

            m_prevVelocity = m_vehicleRigidBody.velocity;
            m_prevPosition = transform.position;
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
            m_setColor = setColor;
            m_vehicleColor = vehicleColor;
            m_vehicleColor.a = 0; // Make sure blinking is not set.
            m_vehicleInfo = vehicleInfo;
            m_lightState = Vector4.zero;
            SpawnVehicle(position, rotation);
            FPSCamController.Instance.FPSCam = new DriveCam();
            FPSCamController.Instance.EnableCam(true);
            m_renderMask = Singleton<RenderManager>.instance.CurrentCameraInfo.m_camera.cullingMask;
        }
        private void SpawnVehicle(Vector3 position, Quaternion rotation)
        {
            gameObject.transform.position = position;
            gameObject.transform.rotation = rotation;
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
            m_vehicleCollider.size = vehicleMesh.bounds.size + new Vector3(0, 1.3f, 0);
            m_halfWidth = vehicleMesh.bounds.size.x;
            m_halfLength = vehicleMesh.bounds.size.z;
            m_rotationOffset = Quaternion.identity;
            m_vehicleRigidBody.velocity = Vector3.zero;

            AddEffects();
        }

        internal void DestroyVehicle()
        {
            m_lightEffects.Clear();
            m_regularEffects.Clear();
            m_specialEffects.Clear();

            StartCoroutine(m_collidersManager.DisableColliders());
            enabled = false;
            gameObject.SetActive(false);
            Singleton<RenderManager>.instance.CurrentCameraInfo.m_camera.cullingMask = m_renderMask;
            m_vehicleInfo = null;
            m_vehicleColor = default;
            m_setColor = false;
        }
        private void CalculateSlope()
        {
            Vector3 diffVector = transform.position - m_prevPosition;
            Vector3 horizontalDirection = new Vector3(diffVector.x, 0f, diffVector.z);
            float heightDifference = diffVector.y;

            if (horizontalDirection.sqrMagnitude > 0.01f)
            {
                float slopeAngle = Mathf.Atan2(heightDifference, horizontalDirection.magnitude) * Mathf.Rad2Deg;//+: upslope -: downslope
                slopeAngle = Mathf.Clamp(slopeAngle, -90f, 90f);

                bool isReversing = Vector3.Dot(diffVector.normalized, transform.forward) < 0;

                if (isReversing)
                    slopeAngle = -slopeAngle;

                var targetRotation = Quaternion.Euler(
                    -slopeAngle,
                    transform.rotation.eulerAngles.y,
                    0f
                );

                transform.rotation = Quaternion.Slerp(
                    transform.rotation,
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

            var height = MapUtils.GetMinHeightAt(position);
            height -= 2f; // Map Utils adds 2f

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

                    if (GetClosestLanePositionDriveFiltered(ref segment, transform.position, out roadPos, out offset))
                    {
                        height = roadPos.y;

                        if (offset == 0f || offset == 1f)
                        {
                            var node = Singleton<NetManager>.instance.m_nodes.m_buffer[offset == 0f ? segment.m_startNode : segment.m_endNode];
                            if (node.CountSegments() == 2)
                            {
                                segment = ref Singleton<NetManager>.instance.m_segments.m_buffer[node.GetAnotherSegment(output.m_netSegment)];
                                if (GetClosestLanePositionDriveFiltered(ref segment, transform.position, out roadPos, out _))
                                {
                                    height = Mathf.Min(roadPos.y, height);
                                }
                            }
                        }
                    }
                }
            }

            return height;
        }

        private bool GetClosestLanePositionDriveFiltered(ref NetSegment segmentIn, Vector3 posIn, out Vector3 posOut, out float offsetOut)
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
                    Singleton<NetManager>.instance.m_lanes.m_buffer[lane].GetClosestPosition(transform.position, out var posTmp, out var offsetTmp);
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
            var position = transform.position;
            var velocity = m_vehicleRigidBody.velocity;
            var acceleration = ((m_vehicleRigidBody.velocity - m_prevVelocity) / Time.fixedDeltaTime).magnitude;
            var swayPosition = Vector3.zero;
            var rotation = transform.rotation;
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
            if (Mathf.Min(terrainHeight, roadHeight) > cameraTransform.position.y)
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