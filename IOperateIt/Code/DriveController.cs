extern alias FPSCamera;
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
        public static DriveController Instance { get; private set; }

        private Rigidbody vehicleRigidBody;
        private BoxCollider vehicleCollider;
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
            vehicleRigidBody.drag = 2f;
            vehicleRigidBody.angularDrag = 2.5f;
            vehicleRigidBody.mass = 1500f;
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
        }
        private void FixedUpdate()
        {
            HandleInputOnFixedUpdate();
            CalculateSlope();
            collidersManager.UpdateColliders(transform);

            terrainHeight = MapUtils.GetMinHeightAt(transform.position);
            if (MapUtils.GetClosestSegmentLevel(transform.position, out var newHeight))
                terrainHeight = newHeight;
            terrainHeight -= 2f;

            if (transform.position.y - 1f < terrainHeight)
            {
                vehicleRigidBody.velocity = new Vector3(vehicleRigidBody.velocity.x, 0, vehicleRigidBody.velocity.z);
                transform.position = new Vector3(transform.position.x, terrainHeight, transform.position.z);
            }

            if (Speed > ModSettings.MaxVelocity.FromKmph())
            {
                vehicleRigidBody.velocity = vehicleRigidBody.velocity.normalized * ModSettings.MaxVelocity.FromKmph();
            }

            prevVelocity = vehicleRigidBody.velocity;
        }
        private void LateUpdate()
        {
        }
        private void OnDestroy()
        {
            collidersManager.DestroyColliders();
        }

        private void OnCollisionEnter()
        {
        }
        public void StartDriving(Vector3 position, Quaternion rotation) => StartDriving(position, rotation, vehicleInfo);
        public void StartDriving(Vector3 position, Quaternion rotation, VehicleInfo vehicleInfo)
        {
            enabled = true;
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
            GetComponent<MeshFilter>().mesh = GetComponent<MeshFilter>().sharedMesh = vehicleMesh;
            GetComponent<MeshRenderer>().material = GetComponent<MeshRenderer>().sharedMaterial = vehicleInfo.m_material;
            gameObject.SetActive(true);
            vehicleCollider.size = vehicleMesh.bounds.size + new Vector3(0, 1.3f, 0);
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
            prevPosition = transform.position;
        }

        private void HandleInputOnFixedUpdate()
        {
            if (FPCModSettings.Instance.XMLKeyMoveForward.IsPressed())
                vehicleRigidBody.AddRelativeForce(Vector3.forward * ModSettings.AccelerationForce, ForceMode.Acceleration);

            if (FPCModSettings.Instance.XMLKeyMoveBackward.IsPressed())
                vehicleRigidBody.AddRelativeForce(Vector3.back * ModSettings.AccelerationForce, ForceMode.Acceleration);

            if (FPCModSettings.Instance.XMLKeyMoveLeft.IsPressed())
            {
                var normalizedVelocity = vehicleRigidBody.velocity.normalized;
                var brakeVelocity = normalizedVelocity * ModSettings.BreakingForce * 0.58f;
                vehicleRigidBody.AddForce(-brakeVelocity);
                vehicleRigidBody.AddRelativeTorque(Vector3.down * 5f * Time.fixedDeltaTime, ForceMode.VelocityChange);
            }
            if (FPCModSettings.Instance.XMLKeyMoveRight.IsPressed())
            {
                var normalizedVelocity = vehicleRigidBody.velocity.normalized;
                var brakeVelocity = normalizedVelocity * ModSettings.BreakingForce * 0.58f;
                vehicleRigidBody.AddForce(-brakeVelocity);
                vehicleRigidBody.AddRelativeTorque(Vector3.up * 5f * Time.fixedDeltaTime, ForceMode.VelocityChange);
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

            var instanceRotation = transform.rotation * rotationOffset;
            var instancePos = transform.position +
                ((ModSettings.Offset.z > -1f ? transform.rotation /*rotate with the offset position*/ :
                instanceRotation  /*rotate with the vehicle position*/) * ModSettings.Offset);

            // Limit the camera's position to the allowed area.
            instancePos = CameraController.ClampCameraPosition(instancePos);

            // Apply the calculated position and rotation to the camera.
            if (FPCModSettings.Instance.XMLSmoothTransition)
            {
                cameraTransform.position =
                cameraTransform.position.DistanceTo(instancePos) > FPCModSettings.Instance.XMLMinTransDistance &&
                cameraTransform.position.DistanceTo(instancePos) <= FPCModSettings.Instance.XMLMaxTransDistance
                ? Vector3.Lerp(cameraTransform.position, instancePos, Time.deltaTime * FPCModSettings.Instance.XMLTransSpeed)
                : instancePos;
                cameraTransform.rotation = Quaternion.Slerp(cameraTransform.rotation, instanceRotation, Time.deltaTime * FPCModSettings.Instance.XMLTransSpeed);
            }
            else
            {
                cameraTransform.position = instancePos;
                cameraTransform.rotation = instanceRotation;
            }
        }
    }
}