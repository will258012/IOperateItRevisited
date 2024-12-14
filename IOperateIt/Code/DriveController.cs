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
        private Dictionary<GameObject, Light> Lights = new Dictionary<GameObject, Light>();
        //private List<EffectInfo> audioEffects = new List<EffectInfo>();
        //private List<EffectInfo> togglableAudioEffects = new List<EffectInfo>();
        private CollidersManager _collidersManager;
        private float terrainHeight;
        private Vector3 prevPosition;
        private Vector3 prevVelocity;
        private Quaternion rotationOffset;

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

            vehicleCollider = gameObject.AddComponent<BoxCollider>();

            _collidersManager = new CollidersManager();
            StartCoroutine(_collidersManager.InitializeColliders());
            gameObject.SetActive(false);
            enabled = false;
        }
        private void Update()
        {
            HandleInputOnUpdate();
            UpdateCameraPos();
        }
        private void FixedUpdate()
        {
            HandleInputOnFixedUpdate();
            CalculateSlope();
            _collidersManager.UpdateColliders(transform);

            terrainHeight = MapUtils.GetMinHeightAt(transform.position);
            if (MapUtils.GetClosestSegmentLevel(transform.position, out var newHeight))
                terrainHeight = newHeight;
            terrainHeight -= 2f;

            if (transform.position.y < terrainHeight + 1f)
            {
                vehicleRigidBody.velocity = new Vector3(vehicleRigidBody.velocity.x, 0, vehicleRigidBody.velocity.z);
                transform.position = new Vector3(transform.position.x, terrainHeight, transform.position.z);
            }
            else
            {
                vehicleRigidBody.AddRelativeForce(Physics.gravity * 6f);
            }

            var currentSpeed = FPCModSettings.Instance.XMLUseMetricUnit
                ? vehicleRigidBody.velocity.magnitude.ToKilometer()
                : vehicleRigidBody.velocity.magnitude.ToMile();

            if (currentSpeed > ModSettings.MaxVelocity)
            {
                var normalisedVelocity = vehicleRigidBody.velocity.normalized;
                var overSpeedFactor = currentSpeed - ModSettings.MaxVelocity;
                var brakeVelocity = normalisedVelocity * (ModSettings.AccelerationForce + overSpeedFactor);
                vehicleRigidBody.AddForce(-brakeVelocity);
            }

            prevVelocity = vehicleRigidBody.velocity;

            /*
            foreach (var effect in audioEffects)
            {
                effect.PlayEffect(default,
                           new EffectInfo.SpawnArea(vehicleRigidBody.transform.localToWorldMatrix, vehicleInfo.m_lodMeshData),
                           vehicleRigidBody.velocity,
                           (vehicleRigidBody.velocity.magnitude - prevVelocity.magnitude) / Time.fixedDeltaTime,
                           vehicleRigidBody.velocity.magnitude,
                           AudioManager.instance.CurrentListenerInfo,
                           VehicleManager.instance.m_audioGroup);

            }
            foreach (var effect in togglableAudioEffects)
            {
                effect.PlayEffect(default,
                   new EffectInfo.SpawnArea(vehicleRigidBody.transform.localToWorldMatrix, vehicleInfo.m_lodMeshData),
                   vehicleRigidBody.velocity,
                   (vehicleRigidBody.velocity.magnitude - prevVelocity.magnitude) / Time.fixedDeltaTime,
                   vehicleRigidBody.velocity.magnitude,
                   AudioManager.instance.CurrentListenerInfo,
                   VehicleManager.instance.m_audioGroup);
            }
            */
        }
        private void LateUpdate()
        {
        }
        private void OnDestroy()
        {
            _collidersManager.DestroyColliders();
            foreach (var light in Lights)
            {
                Destroy(light.Key);
            }
            Lights.Clear();
            /*
            foreach (var audioEffect in audioEffects)
            {
                if (audioEffect != null)
                {
                    Destroy(audioEffect);
                }
            }
            audioEffects.Clear();
            foreach (var togglableAudioEffect in togglableAudioEffects)
            {
                if (togglableAudioEffect != null)
                {
                    Destroy(togglableAudioEffect);
                }
            }
            togglableAudioEffects.Clear();
            */
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

            for (int i = 0; i < vehicleInfo.m_lightPositions.Length; i++)
            {
                var lightPosition = vehicleInfo.m_lightPositions[i];
                var lightObj = new GameObject("Light" + i);
                var light = lightObj.AddComponent<Light>();
                lightObj.transform.parent = gameObject.transform;
                lightObj.transform.localPosition = lightPosition;
                lightObj.transform.localRotation = Quaternion.identity;
                light.type = LightType.Spot;
                light.enabled = false;
                light.spotAngle = 20f;
                light.range = 50f;
                light.intensity = 5f;
                light.color = Color.white;
                Lights.Add(lightObj, light);
            }
            vehicleCollider.size = vehicleMesh.bounds.size + new Vector3(0, 1.3f, 0);

            rotationOffset = Quaternion.identity;
            vehicleRigidBody.velocity = Vector3.zero;
            /*
            foreach (var effect in vehicleInfo.m_effects)
            {
                var effectInfo = effect.m_effect;

                if (effectInfo != null)
                {
                    if ((effect.m_vehicleFlagsRequired & (Vehicle.Flags.Emergency1 | Vehicle.Flags.Emergency2)) != Vehicle.Flags.Deleted)
                    {
                        togglableAudioEffects.Add(effectInfo);
                    }
                    else
                    {
                        audioEffects.Add(effectInfo);
                    }
                }
            }
            */
        }
        internal void DestroyVehicle()
        {
            foreach (var light in Lights)
            {
                Destroy(light.Key);
            }
            Lights.Clear();
            /*
            foreach (var audioEffect in audioEffects)
            {
                if (audioEffect != null)
                {
                    Destroy(audioEffect);
                }
            }
            audioEffects.Clear();
            foreach (var togglableAudioEffect in togglableAudioEffects)
            {
                if (togglableAudioEffect != null)
                {
                    Destroy(togglableAudioEffect);
                }
            }
            togglableAudioEffects.Clear();
            */
            StartCoroutine(_collidersManager.DisableColliders());
            enabled = false;
            gameObject.SetActive(false);
        }
        private void CalculateSlope()
        {
            var oldEuler = transform.rotation.eulerAngles;
            var diffVector = transform.position - prevPosition;
            Quaternion newRotation;
            if (diffVector.sqrMagnitude > 0.05f)
            {
                newRotation = Quaternion.LookRotation(diffVector);
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.Euler(Mathf.Abs(newRotation.eulerAngles.x), oldEuler.y, 0), Time.deltaTime * 6.0f);
            }
            prevPosition = transform.position;
        }

        private void HandleInputOnFixedUpdate()
        {

            if (InputManager.KeyPressed(FPCModSettings.Instance.XMLKeyMoveForward))
            {
                vehicleRigidBody.AddRelativeForce(Vector3.forward * ModSettings.AccelerationForce * Time.fixedDeltaTime, ForceMode.VelocityChange);
            }
            if (InputManager.KeyPressed(FPCModSettings.Instance.XMLKeyMoveBackward))
            {
                vehicleRigidBody.AddRelativeForce(Vector3.back * ModSettings.AccelerationForce * Time.fixedDeltaTime, ForceMode.VelocityChange);
            }
            if (InputManager.KeyPressed(FPCModSettings.Instance.XMLKeyMoveLeft) && vehicleRigidBody.velocity.sqrMagnitude > 0.05f)
            {
                var normalizedVelocity = vehicleRigidBody.velocity.normalized;
                var brakeVelocity = normalizedVelocity * ModSettings.BreakingForce * 0.58f;
                vehicleRigidBody.AddForce(-brakeVelocity);
                vehicleRigidBody.AddRelativeTorque(Vector3.down * 5f * Time.fixedDeltaTime, ForceMode.VelocityChange);
            }
            if (InputManager.KeyPressed(FPCModSettings.Instance.XMLKeyMoveRight) && vehicleRigidBody.velocity.sqrMagnitude > 0.05f)
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
            {
                foreach (var light in Lights)
                {
                    light.Value.enabled = !light.Value.enabled;
                }
            }
            var cursorVisible =
                InputManager.KeyPressed(FPCModSettings.Instance.XMLKeyCursorToggle) ^ (FPCModSettings.Instance.XMLShowCursorFollow);
            InputManager.ToggleCursor(cursorVisible);

            if (InputManager.MouseTriggered(InputManager.MouseButton.Middle) ||
                InputManager.KeyTriggered(FPCModSettings.Instance.XMLKeyCamReset))
            {
                rotationOffset = Quaternion.identity;
            }
            float yawDegree = 0f, pitchDegree = 0f;
            { // key rotation
                var rotateFactor = FPCModSettings.Instance.XMLRotateKeyFactor * Time.deltaTime;

                if (InputManager.KeyPressed(FPCModSettings.Instance.XMLKeyRotateRight)) yawDegree += 1f * rotateFactor;
                if (InputManager.KeyPressed(FPCModSettings.Instance.XMLKeyRotateLeft)) yawDegree -= 1f * rotateFactor;
                if (InputManager.KeyPressed(FPCModSettings.Instance.XMLKeyRotateUp)) pitchDegree -= 1f * rotateFactor;
                if (InputManager.KeyPressed(FPCModSettings.Instance.XMLKeyRotateDown)) pitchDegree += 1f * rotateFactor;

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