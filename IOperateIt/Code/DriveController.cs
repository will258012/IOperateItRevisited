extern alias FPSCamera;

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
        private const float GRIP = 2.0f;

        private float halfLength = 0.0f;
        private float halfWidth = 0.0f;
        
        public static DriveController Instance { get; private set; }

        private Rigidbody vehicleRigidBody;
        private BoxCollider vehicleCollider;
        private Color vehicleColor;
        private bool setColor;
        internal VehicleInfo vehicleInfo;

        private List<LightEffect> lightEffects = new List<LightEffect>();
        private List<EffectInfo> regularEffects = new List<EffectInfo>();
        private List<EffectInfo> specialEffects = new List<EffectInfo>();

        private CollidersManager collidersManager = new CollidersManager();
        private float terrainHeight;
        private Vector3 prevPosition;
        private Vector3 prevVelocity;
        private Quaternion rotationOffset;
        private bool isSirenEnabled = true;
        private bool isLightEnabled = false;

        private float steer = 0f;
        private float throttle = 0f;
        internal float Speed => vehicleRigidBody.velocity.magnitude;
        private void Awake()
        {
            Instance = this;
            gameObject.AddComponent<MeshFilter>();
            gameObject.AddComponent<MeshRenderer>();
            GetComponent<MeshRenderer>().enabled = true;

            vehicleRigidBody = gameObject.AddComponent<Rigidbody>();
            vehicleRigidBody.isKinematic = false;
            vehicleRigidBody.useGravity = true;
            vehicleRigidBody.freezeRotation = true;
            vehicleRigidBody.drag = 0.1f;
            vehicleRigidBody.angularDrag = 0.1f;
            vehicleRigidBody.mass = 1000f;
            vehicleRigidBody.interpolation = RigidbodyInterpolation.Interpolate;


            vehicleCollider = gameObject.AddComponent<BoxCollider>();

            StartCoroutine(collidersManager.InitializeColliders());
            gameObject.SetActive(false);
            enabled = false;
        }
        private void Update()
        {
            HandleInputOnUpdate();
            UpdateCameraPos();
            PlayEffects();

            /*
            MaterialPropertyBlock materialBlock = VehicleManager.instance.m_materialBlock;
            materialBlock.Clear();
            //materialBlock.SetMatrix(VehicleManager.instance.ID_TyreMatrix, value);
            //materialBlock.SetVector(VehicleManager.instance.ID_TyrePosition, tyrePosition);
            materialBlock.SetVector(VehicleManager.instance.ID_LightState, Vector4.one);
            if (setColor)
            {
                materialBlock.SetColor(VehicleManager.instance.ID_Color, vehicleColor);
            }
            GetComponent<MeshRenderer>().SetPropertyBlock(materialBlock);
             */
        }
        private void FixedUpdate()
        {
            HandleInputOnFixedUpdate();

            vehicleRigidBody.AddRelativeForce(Vector3.forward * ModSettings.AccelerationForce * throttle, ForceMode.Force);

            var lateralVel = transform.InverseTransformDirection(vehicleRigidBody.velocity);
            lateralVel.z = 0.0f;
            lateralVel.y = 0.0f;

            lateralVel = Mathf.Min(Vector3.Magnitude(lateralVel) * 0.99f, GRIP) * Vector3.Normalize(lateralVel);

            vehicleRigidBody.AddRelativeForce(-lateralVel, ForceMode.VelocityChange);

            var invert = Vector3.Dot(Vector3.forward, transform.InverseTransformDirection(vehicleRigidBody.velocity)) > 0.0f ? 1.0f : -1.0f;
            var speedsteer = Mathf.Min(Mathf.Max(Speed * 20f, 0f), 60f);
            speedsteer = Mathf.Sign(steer) * Mathf.Min(Mathf.Abs(60f * steer), speedsteer);

            var angularTarget = 0.99f * (Vector3.down * invert * speedsteer * Time.fixedDeltaTime) - transform.InverseTransformDirection(vehicleRigidBody.angularVelocity);

            vehicleRigidBody.AddRelativeTorque(angularTarget, ForceMode.VelocityChange);

            collidersManager.UpdateColliders(transform);

            terrainHeight = CalculateHeight(transform.position);

            if (transform.position.y < terrainHeight)
            {
                vehicleRigidBody.velocity = new Vector3(vehicleRigidBody.velocity.x, 0f, vehicleRigidBody.velocity.z);
                transform.position = new Vector3(transform.position.x, 0.5f * terrainHeight + 0.5f * transform.position.y, transform.position.z);
            }

            if (transform.position.y + WALL_HEIGHT < terrainHeight)
            {
                transform.position = prevPosition;
                vehicleRigidBody.velocity = Vector3.zero;
            }

            CalculateSlope();

            if (Speed > ModSettings.MaxVelocity.FromKmph())
            {
                vehicleRigidBody.velocity = vehicleRigidBody.velocity.normalized * ModSettings.MaxVelocity.FromKmph();
            }

            UpdateCameraRendering();

            prevVelocity = vehicleRigidBody.velocity;
            prevPosition = transform.position;
        }
        private void LateUpdate()
        {
        }
        private void OnDestroy()
        {
            collidersManager.DestroyColliders();
        }

        private void OnCollisionEnter(Collision collision)
        {
            foreach (ContactPoint contact in collision.contacts)
            {
                Debug.DrawRay(contact.point, contact.normal, Color.white);
            }
        }

        public void StartDriving(Vector3 position, Quaternion rotation) => StartDriving(position, rotation, vehicleInfo, Color.gray, false);
        public void StartDriving(Vector3 position, Quaternion rotation, VehicleInfo vehicleInfo, Color vehicleColor, bool setColor)
        {
            enabled = true;
            this.setColor = setColor;
            this.vehicleColor = vehicleColor;
            this.vehicleInfo = vehicleInfo;
            SpawnVehicle(position, rotation);
            FPSCamController.Instance.FPSCam = new DriveCam();
            FPSCamController.Instance.EnableCam(true);
        }
        private void SpawnVehicle(Vector3 position, Quaternion rotation)
        {
            gameObject.transform.position = position;
            gameObject.transform.rotation = rotation;
            var vehicleMesh = vehicleInfo.m_mesh;
            vehicleInfo.CalculateGeneratedInfo();
            GetComponent<MeshFilter>().mesh = GetComponent<MeshFilter>().sharedMesh = vehicleMesh;
            GetComponent<MeshRenderer>().material = GetComponent<MeshRenderer>().sharedMaterial = vehicleInfo.m_material;

            MaterialPropertyBlock materialBlock = VehicleManager.instance.m_materialBlock;
            materialBlock.Clear();
            if (setColor)
            {
                materialBlock.SetColor(VehicleManager.instance.ID_Color, vehicleColor);
            }
            GetComponent<MeshRenderer>().SetPropertyBlock(materialBlock);

            gameObject.SetActive(true);
            vehicleCollider.size = vehicleMesh.bounds.size + new Vector3(0, 1.3f, 0);
            halfWidth = vehicleMesh.bounds.size.x;
            halfLength = vehicleMesh.bounds.size.z;
            rotationOffset = Quaternion.identity;
            vehicleRigidBody.velocity = Vector3.zero;

            AddEffects();
        }

        internal void DestroyVehicle()
        {
            lightEffects.Clear();
            regularEffects.Clear();
            specialEffects.Clear();

            StartCoroutine(collidersManager.DisableColliders());
            enabled = false;
            gameObject.SetActive(false);
        }
        private void CalculateSlope()
        {
            Vector3 diffVector = transform.position - prevPosition;
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

            var height = FPSCamera.FPSCamera.Utils.MapUtils.GetMinHeightAt(position);
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
                    NetSegment segment = NetManager.instance.m_segments.m_buffer[output.m_netSegment];

                    if (GetClosestLanePositionDriveFiltered(segment, transform.position, out roadPos, out offset))
                    {
                        height = roadPos.y;

                        if (offset == 0f || offset == 1f)
                        {
                            var node = NetManager.instance.m_nodes.m_buffer[offset == 0f ? segment.m_startNode : segment.m_endNode];
                            if (node.CountSegments() == 2)
                            {
                                segment = NetManager.instance.m_segments.m_buffer[node.GetAnotherSegment(output.m_netSegment)];
                                if (GetClosestLanePositionDriveFiltered(segment, transform.position, out roadPos, out _))
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

        private bool GetClosestLanePositionDriveFiltered(NetSegment segmentIn, Vector3 posIn, out Vector3 posOut, out float offsetOut)
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
                    NetManager.instance.m_lanes.m_buffer[lane].GetClosestPosition(transform.position, out var posTmp, out var offsetTmp);
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
                lane = NetManager.instance.m_lanes.m_buffer[lane].m_nextLane;
                index++;
            }
            return found;
        }

        private void HandleInputOnFixedUpdate()
        {
            bool throttling = false;
            if (FPCModSettings.Instance.XMLKeyMoveForward.IsPressed())
            {
                throttle = Mathf.Clamp(throttle + Time.fixedDeltaTime / THROTTLE_RESP, 0.0f, 1.0f);
                throttling = true;
            }
            if (FPCModSettings.Instance.XMLKeyMoveBackward.IsPressed())
            {
                throttle = Mathf.Clamp(throttle - Time.fixedDeltaTime / THROTTLE_RESP, -1.0f, 0.0f);
                throttling = true;
            }
            if (!throttling)
            {
                if (throttle > 0.0f)
                {
                    throttle = Mathf.Clamp(throttle - Time.fixedDeltaTime / THROTTLE_RESP, 0.0f, 1.0f);
                }
                else if (throttle < 0.0f)
                {
                    throttle = Mathf.Clamp(throttle + Time.fixedDeltaTime / THROTTLE_RESP, -1.0f, 0.0f);
                }
            }

            bool steering = false;
            if (FPCModSettings.Instance.XMLKeyMoveLeft.IsPressed())
            {
                steer = Mathf.Clamp(steer + Time.fixedDeltaTime / STEER_RESP, -1.0f, 1.0f);
                steering = true;
            }
            if (FPCModSettings.Instance.XMLKeyMoveRight.IsPressed())
            {
                steer = Mathf.Clamp(steer - Time.fixedDeltaTime / STEER_RESP, -1.0f, 1.0f);
                steering = true;
            }
            if (!steering)
            {
                if (steer > 0.0f)
                {
                    steer = Mathf.Clamp(steer - Time.fixedDeltaTime / STEER_RESP, 0.0f, 1.0f);
                }
                if (steer < 0.0f)
                {
                    steer = Mathf.Clamp(steer + Time.fixedDeltaTime / STEER_RESP, -1.0f, 0.0f);
                }
            }
        }

        private void HandleInputOnUpdate()
        {
            if (InputManager.KeyTriggered((KeyCode)ModSettings.KeyLightToggle.Key))
                isLightEnabled = !isLightEnabled;

            if (InputManager.KeyTriggered((KeyCode)ModSettings.KeySirenToggle.Key))
                isSirenEnabled = !isSirenEnabled;

            var cursorVisible =
             FPCModSettings.Instance.XMLKeyCursorToggle.IsPressed() ^ (FPCModSettings.Instance.XMLShowCursorFollow);
            InputManager.ToggleCursor(cursorVisible);

            if (InputManager.MouseTriggered(InputManager.MouseButton.Middle) ||
                InputManager.KeyTriggered(FPCModSettings.Instance.XMLKeyCamReset))
            {
                rotationOffset = Quaternion.identity;
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
            rotationOffset = yawRotation * rotationOffset * pitchRotation;

            // Limit pitch
            var eulerAngles = rotationOffset.eulerAngles;
            if (eulerAngles.x > 180f) eulerAngles.x -= 360f;
            eulerAngles.x = eulerAngles.x.Clamp(ModSettings.Offset.z > -1f ? -FPCModSettings.Instance.XMLMaxPitchDeg : 0f, FPCModSettings.Instance.XMLMaxPitchDeg);
            eulerAngles.z = 0f;
            rotationOffset = Quaternion.Euler(eulerAngles);
        }
        private void AddEffects()
        {
            if (vehicleInfo.m_effects != null)
            {
                foreach (var effect in vehicleInfo.m_effects)
                {
                    {
                        if (effect.m_effect != null)
                        {
                            if (effect.m_vehicleFlagsRequired.IsFlagSet(Vehicle.Flags.Emergency1 | Vehicle.Flags.Emergency2))
                                specialEffects.Add(effect.m_effect);
                            else
                            {
                                if (effect.m_effect is MultiEffect multiEffect)
                                {
                                    foreach (var sub in multiEffect.m_effects)
                                        if (sub.m_effect is LightEffect lightEffect)
                                            lightEffects.Add(lightEffect);
                                }
                                regularEffects.Add(effect.m_effect);
                            }
                        }
                    }
                }
            }
        }
        private void PlayEffects()
        {
            var position = transform.position;
            var velocity = vehicleRigidBody.velocity;
            var acceleration = ((vehicleRigidBody.velocity - prevVelocity) / Time.fixedDeltaTime).magnitude;
            var swayPosition = Vector3.zero;
            var rotation = transform.rotation;
            var scale = Vector3.one;
            var matrix = vehicleInfo.m_vehicleAI.CalculateBodyMatrix(Vehicle.Flags.Created | Vehicle.Flags.Spawned, ref position, ref rotation, ref scale, ref swayPosition);
            var area = new EffectInfo.SpawnArea(matrix, vehicleInfo.m_lodMeshData);
            var listenerInfo = AudioManager.instance.CurrentListenerInfo;
            var audioGroup = VehicleManager.instance.m_audioGroup;
            RenderGroup.MeshData effectMeshData = vehicleInfo.m_vehicleAI.GetEffectMeshData();
            var area2 = new EffectInfo.SpawnArea(matrix, effectMeshData, vehicleInfo.m_generatedInfo.m_tyres, vehicleInfo.m_lightPositions);

            foreach (var regularEffect in regularEffects)
            {
                regularEffect.PlayEffect(default, area, velocity, acceleration, 1f, listenerInfo, audioGroup);
            }
            if (isLightEnabled)
                foreach (var light in lightEffects)
                {
                    light.RenderEffect(default, area2, velocity, acceleration, 1f, -1f, SimulationManager.instance.m_simulationTimeDelta, RenderManager.instance.CurrentCameraInfo);
                }

            if (isSirenEnabled)
                foreach (var specialEffect in specialEffects)
                {
                    specialEffect.RenderEffect(default, area2, velocity, acceleration, 1f, -1f, SimulationManager.instance.m_simulationTimeDelta, RenderManager.instance.CurrentCameraInfo);
                    specialEffect.PlayEffect(default, area, velocity, acceleration, 1f, listenerInfo, audioGroup);
                }
        }
        private void UpdateCameraPos()
        {
            var cameraTransform = GameCamController.Instance.MainCamera.transform;

            Vector3 vehiclePosition = vehicleRigidBody.transform.position;
            Vector3 vehicleDirection = Vector3.Normalize(vehicleRigidBody.velocity);
            if (vehicleDirection.y > 0.999f || vehicleDirection.y < -0.999f)
            {
                vehicleDirection = Vector3.Normalize(vehicleRigidBody.transform.InverseTransformDirection(Vector3.forward));
            }
            if (vehicleDirection.y > 0.999f || vehicleDirection.y < -0.999f)
            {
                vehicleDirection = Vector3.forward;
            }
            Quaternion targetRotation = Quaternion.identity;
            targetRotation.SetLookRotation(vehicleDirection);
            targetRotation *= rotationOffset;

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
                RenderManager.instance.CurrentCameraInfo.m_camera.cullingMask |= (1 << Singleton<VehicleManager>.instance.m_undergroundLayer);
            }
            else
            {
                RenderManager.instance.CurrentCameraInfo.m_camera.cullingMask &= ~(1 << Singleton<VehicleManager>.instance.m_undergroundLayer);
            }
        }
    }
}