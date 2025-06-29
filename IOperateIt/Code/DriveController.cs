extern alias FPSCamera;
using AlgernonCommons;
using AlgernonCommons.UI;
using ColossalFramework;
using FPSCamera.FPSCamera.Cam.Controller;
using FPSCamera.FPSCamera.Game;
using FPSCamera.FPSCamera.Utils;
using IOperateIt.Settings;
using System;
using System.Collections.Generic;
using UnityEngine;
using static FPSCamera.FPSCamera.Utils.MathUtils;
using FPCModSettings = FPSCamera.FPSCamera.Settings.ModSettings;
namespace IOperateIt
{
    public class DriveController : MonoBehaviour
    {
        public static DriveController Instance { get; private set; }

        private Rigidbody vehicleRigidBody;
        private BoxCollider vehicleCollider;
        internal VehicleInfo vehicleInfo;

        private readonly List<LightEffect> lightEffects = new List<LightEffect>();
        private readonly List<EffectInfo> regularEffects = new List<EffectInfo>();
        private readonly List<EffectInfo> specialEffects = new List<EffectInfo>();

        private readonly CollidersManager collidersManager = new CollidersManager();
        private float terrainHeight;
        private Positioning offset;
        private Vector3 prevPosition;
        private Vector3 prevVelocity;
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
            vehicleRigidBody.useGravity = false;
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
            try
            {
                HandleInputOnUpdate();
                UpdateCameraPos();
                PlayEffects();
            }
            catch (Exception e)
            {
                FPSCamController.Instance.FPSCam = null;
                Logging.LogException(e);
                DestroyVehicle();
            }
        }
        private void FixedUpdate()
        {
            try
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
                else
                {
                    vehicleRigidBody.AddRelativeForce(Physics.gravity * 6f, ForceMode.Acceleration);
                }

                if (Speed > ModSettings.MaxVelocity.FromKmph())
                {
                    vehicleRigidBody.velocity = vehicleRigidBody.velocity.normalized * ModSettings.MaxVelocity.FromKmph();
                }

                prevVelocity = vehicleRigidBody.velocity;
            }
            catch (Exception e)
            {
                FPSCamController.Instance.FPSCam = null;
                Logging.LogException(e);
                DestroyVehicle();
            }
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
            offset.pos = ModSettings.Offset;
            offset.rotation = Quaternion.identity;

            this.vehicleInfo = vehicleInfo;
            SpawnVehicle(position, rotation);
            FPSCamController.Instance.FPSCam = new DriveCam();
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
            vehicleRigidBody.velocity = Vector3.zero;

            AddEffects();
        }

        internal void DestroyVehicle()
        {
            lightEffects.Clear();
            regularEffects.Clear();
            specialEffects.Clear();

            ModSettings.Offset = offset.pos;
            ModSettings.Save();
            OptionsPanelManager<OptionsPanel>.LocaleChanged();
            UI.MainPanel.Instance?.LocaleChanged();

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

            if (InputManager.MouseButton.Middle.MouseTriggered() ||
                FPCModSettings.Instance.XMLKeyCamReset.KeyTriggered())
            {
                offset.rotation = Quaternion.identity;
            }
            { // key movement
                var movementFactor = ((FPCModSettings.Instance.XMLKeySpeedUp.IsPressed() ? FPCModSettings.Instance.XMLSpeedUpFactor : 1f)
                                     * FPCModSettings.Instance.XMLMovementSpeed * Time.deltaTime).FromKmph();

                var movement = Vector3.zero;
                if (!(KeyCode.LeftControl.KeyPressed() || KeyCode.RightControl.KeyPressed()))
                {
                    if (FPCModSettings.Instance.XMLKeyRotateUp.IsPressed()) movement += Vector3.forward * movementFactor;
                    if (FPCModSettings.Instance.XMLKeyRotateDown.IsPressed()) movement += Vector3.back * movementFactor;
                    if (FPCModSettings.Instance.XMLKeyRotateRight.IsPressed()) movement += Vector3.right * movementFactor;
                    if (FPCModSettings.Instance.XMLKeyRotateLeft.IsPressed()) movement += Vector3.left * movementFactor;
                }
                if (FPCModSettings.Instance.XMLKeyMoveUp.IsPressed()) movement += Vector3.up * movementFactor;
                if (FPCModSettings.Instance.XMLKeyMoveDown.IsPressed()) movement += Vector3.down * movementFactor;

                offset.pos += offset.pos.z < -1f ? movement : offset.rotation * movement;
            }

            float yawDegree = 0f, pitchDegree = 0f;
            { // key rotation
                var rotateFactor = FPCModSettings.Instance.XMLRotateKeyFactor * Time.deltaTime;
                if (KeyCode.LeftControl.KeyPressed() || KeyCode.RightControl.KeyPressed())
                {
                    if (FPCModSettings.Instance.XMLKeyRotateRight.IsPressed()) yawDegree += 1f * rotateFactor;
                    if (FPCModSettings.Instance.XMLKeyRotateLeft.IsPressed()) yawDegree -= 1f * rotateFactor;
                    if (FPCModSettings.Instance.XMLKeyRotateUp.IsPressed()) pitchDegree -= 1f * rotateFactor;
                    if (FPCModSettings.Instance.XMLKeyRotateDown.IsPressed()) pitchDegree += 1f * rotateFactor;
                }
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
            offset.rotation = yawRotation * offset.rotation * pitchRotation;

            // Limit pitch
            var eulerAngles = offset.rotation.eulerAngles;
            if (eulerAngles.x > 180f) eulerAngles.x -= 360f;
            eulerAngles.x = eulerAngles.x.Clamp(offset.pos.z > -1f ? -FPCModSettings.Instance.XMLMaxPitchDeg : 0f, FPCModSettings.Instance.XMLMaxPitchDeg);
            eulerAngles.z = 0f;
            offset.rotation = Quaternion.Euler(eulerAngles);
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
            var pos = transform.position;
            var velocity = vehicleRigidBody.velocity;
            var acceleration = ((vehicleRigidBody.velocity - prevVelocity) / Time.fixedDeltaTime).magnitude;
            var swayPos = Vector3.zero;
            var rotation = transform.rotation;
            var scale = Vector3.one;
            var matrix = vehicleInfo.m_vehicleAI.CalculateBodyMatrix(Vehicle.Flags.Created | Vehicle.Flags.Spawned, ref pos, ref rotation, ref scale, ref swayPos);
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

        /* private void DrawTyre()
         {
             Vector3 currentVelocity = vehicleRigidBody.velocity;
             Vector3 horizontalForward = new Vector3(transform.forward.x, 0, transform.forward.z);
             Vector3 horizontalVelocity = new Vector3(currentVelocity.x, 0, currentVelocity.z);
             var pos = transform.position;
             float steerAngle = SignedAngle(horizontalForward, horizontalVelocity, Vector3.up);
             float travelDist = (pos - prevPosition).magnitude;

             Vector4 tyrePos;
             tyrePos.x = steerAngle;
             tyrePos.y = travelDist;
             tyrePos.z = 0f;
             tyrePos.w = 0f;

             var swayPos = Vector3.zero;
             var rotation = transform.rotation;
             var scale = Vector3.one;
             var matrixBody = vehicleInfo.m_vehicleAI.CalculateBodyMatrix(Vehicle.Flags.Created | Vehicle.Flags.Spawned, ref pos, ref rotation, ref scale, ref swayPos);
             var matrixTyre = vehicleInfo.m_vehicleAI.CalculateTyreMatrix(Vehicle.Flags.Created | Vehicle.Flags.Spawned, ref pos, ref rotation, ref scale, ref matrixBody);

             MaterialPropertyBlock materialBlock = VehicleManager.instance.m_materialBlock;
             materialBlock.Clear();
             materialBlock.SetMatrix(VehicleManager.instance.ID_TyreMatrix, matrixTyre);
             materialBlock.SetVector(VehicleManager.instance.ID_TyrePosition, Vector3.zero);
             materialBlock.SetVector(VehicleManager.instance.ID_LightState, Vector3.zero);
             VehicleManager.instance.m_drawCallData.m_defaultCalls = VehicleManager.instance.m_drawCallData.m_defaultCalls + 1;

             vehicleInfo.m_material.SetVectorArray(VehicleManager.instance.ID_TyreLocation, vehicleInfo.m_generatedInfo.m_tyres);
             Graphics.DrawMesh(vehicleInfo.m_mesh, matrixBody, vehicleInfo.m_material, 0, null, 0, materialBlock);
         }*/
        private void UpdateCameraPos()
        {
            var cameraTransform = GameCamController.Instance.MainCamera.transform;

            var instanceRotation = transform.rotation * offset.rotation;
            var instancePos = transform.position +
                ((offset.pos.z > -1f ? transform.rotation /*rotate with the offset position*/ :
                instanceRotation  /*rotate with the vehicle position*/) * offset.pos);

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