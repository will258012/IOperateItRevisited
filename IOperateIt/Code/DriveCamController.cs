extern alias FPC;
using AlgernonCommons;
using AlgernonCommons.Translation;
using ColossalFramework;
using FPC.FPSCamera.Game;
using FPC.FPSCamera.Utils;
using IOperateIt.Settings;
using UnityEngine;
using FPCModSettings = FPC.FPSCamera.Settings.ModSettings;
namespace IOperateIt
{

    public class DriveCamController : MonoBehaviour
    {
        public class DriveCam : FPC.FPSCamera.Cam.IFPSCam
        {
            public string Name => Translations.Translate("DRIVINGMODE");
            public void DisableCam() => DriveController.Instance.StopDriving();
            public FPC.FPSCamera.Utils.MathUtils.Positioning GetPositioning() => new(Instance.targetRigidBody.transform.position, Instance.targetRigidBody.transform.rotation);
            public float GetSpeed() => Instance.targetRigidBody.velocity.magnitude;
            public bool IsValid() => DriveController.Instance.enabled;
        }

        private const float ROTATE_MOUSE_SCALE = .2f;
        private const float ZOOM_KEY_SCALE = 10.0f;
        private const float ZOOM_MOUSE_SCALE = 1.0f;

        private const float UNDERGROUND_RENDER_BIAS = 1.0f;
        private const float LOOK_MAX_DIST = 100.0f;
        private const float LOOK_RESET_TIME = 5.0f;

        private const float MIN_FOV = 10f;
        private const float MAX_FOV = 75f;
        public static DriveCamController Instance { get; private set; }

        private Rigidbody targetRigidBody;
        private Quaternion rotation;
        private Quaternion rotationOffset;
        private float lastMovedTime;

        private Camera mainCamera;
        private int cachedRenderMask;

        private float targetFoV = FPCModSettings.Instance.XMLCamFieldOfView;
        private bool isFOVTransitioning;

        private void Awake()
        {
            Instance = this;
            enabled = false;
            mainCamera = FPC.FPSCamera.Cam.Controller.GameCamController.Instance.MainCamera;
        }

        public void EnableCam(Rigidbody rigidBody, float distance)
        {
            enabled = true;
            FPC.FPSCamera.Cam.Controller.FPSCamController.Instance.FPSCam = new DriveCam();

            if (FPC.FPSCamera.Cam.Controller.FPSCamController.Instance.FPSCam is not DriveCam)
                throw new System.InvalidOperationException("Failed to enable DriveCam");

            cachedRenderMask = mainCamera.cullingMask;
            targetRigidBody = rigidBody;
            targetFoV = FPCModSettings.Instance.XMLCamFieldOfView;
            rotation = mainCamera.transform.rotation;
            rotationOffset = Quaternion.identity;
            Logging.KeyMessage("Drive cam enabled");
        }
        public void DisableCam()
        {
            enabled = false;
            mainCamera.cullingMask = cachedRenderMask;
            targetRigidBody = null;
            rotation = Quaternion.identity;
            rotationOffset = Quaternion.identity;
            Logging.KeyMessage("Drive cam disabled");
        }
        public void ResetCamera()
        {
            rotationOffset = Quaternion.identity;
            lastMovedTime = 0f;
        }
        private void Update()
        {
            UpdateCameraPos();
            HandleInputOnUpdate();
        }
        private void FixedUpdate()
        {
            UpdateCameraRendering();
        }
        private void UpdateCameraPos()
        {

            if (Time.time > lastMovedTime + LOOK_RESET_TIME)
            {
                var vehicleVelocity = targetRigidBody.velocity;
                var vehicleDir = Vector3.Normalize(vehicleVelocity);

                rotation = mainCamera.transform.rotation;

                if (vehicleVelocity.sqrMagnitude < 1.0f || Mathf.Abs(vehicleDir.y) > 0.99f)
                {
                    vehicleDir = Quaternion.Euler(0f, targetRigidBody.rotation.eulerAngles.y, 0f) * Vector3.forward;
                }

                var targetRotation = Quaternion.identity;
                targetRotation.SetLookRotation(vehicleDir);

                rotation = targetRotation;
            }
            else
            {
                rotation = rotationOffset;
            }

            bool isFPS = ModSettings.Offset.z > -1f;

            // Limit pitch
            var eulerAngles = rotation.eulerAngles;
            if (eulerAngles.x > 180f) eulerAngles.x -= 360f;
            eulerAngles.x = eulerAngles.x.Clamp(isFPS ? -FPCModSettings.Instance.XMLMaxPitchDeg : 0f, FPCModSettings.Instance.XMLMaxPitchDeg);
            eulerAngles.z = 0f;
            rotation = Quaternion.Euler(eulerAngles);

            var vehiclePosition = targetRigidBody.position +
                ((isFPS ? targetRigidBody.rotation /*rotate with the offset position*/ :
                rotation /*rotate with the vehicle position*/) * ModSettings.Offset);

            // Limit the camera's position to the allowed area.
            vehiclePosition = CameraController.ClampCameraPosition(vehiclePosition);

            // Apply the calculated position and rotation to the camera.
            if (FPCModSettings.Instance.XMLSmoothTransition)
            {
                mainCamera.transform.position =
                    mainCamera.transform.position.DistanceTo(vehiclePosition) <= FPCModSettings.Instance.XMLMaxTransDistance
                ? Vector3.Lerp(mainCamera.transform.position, vehiclePosition, Time.deltaTime * FPCModSettings.Instance.XMLTransSpeed)
                : vehiclePosition;
                mainCamera.transform.rotation = Quaternion.Slerp(mainCamera.transform.rotation, rotation, Time.deltaTime * FPCModSettings.Instance.XMLTransSpeed);
            }
            else
            {
                mainCamera.transform.position = vehiclePosition;
                mainCamera.transform.rotation = rotation;
            }
        }

        private void UpdateCameraRendering()
        {
            if (!ModSettings.UndergroundRendering) return;

            var terrainHeight = Singleton<TerrainManager>.instance.SampleDetailHeightSmooth(mainCamera.transform.position);

            if (terrainHeight + UNDERGROUND_RENDER_BIAS > mainCamera.transform.position.y)
            {
                Singleton<RenderManager>.instance.CurrentCameraInfo.m_camera.cullingMask |= (1 << Singleton<VehicleManager>.instance.m_undergroundLayer);
            }
            else
            {
                Singleton<RenderManager>.instance.CurrentCameraInfo.m_camera.cullingMask &= ~(1 << Singleton<VehicleManager>.instance.m_undergroundLayer);
            }
        }

        private void HandleInputOnUpdate()
        {
            var fpcModSettings = FPCModSettings.Instance;
            var cursorVisible =
             fpcModSettings.XMLKeyCursorToggle.IsPressed() ^ fpcModSettings.XMLShowCursorFollow;

            InputManager.ToggleCursor(cursorVisible);

            if (InputManager.MouseButton.Middle.MouseTriggered() ||
                   fpcModSettings.XMLKeyCamReset.KeyTriggered())
            {
                rotationOffset = Quaternion.identity;
                lastMovedTime = 0f;

                if (fpcModSettings.XMLSmoothTransition)
                    targetFoV = fpcModSettings.XMLCamFieldOfView;
                else
                    mainCamera.fieldOfView = fpcModSettings.XMLCamFieldOfView;
            }

            // scroll zooming
            var scroll = InputManager.MouseScroll;
            if (fpcModSettings.XMLSmoothTransition)
            {
                var currentFoV = mainCamera.fieldOfView;
                if (scroll > 0f && currentFoV > MIN_FOV)
                {
                    targetFoV = currentFoV / fpcModSettings.XMLFoViewScrollfactor;
                    isFOVTransitioning = true;
                }
                else if (scroll < 0f && currentFoV < MAX_FOV)
                {
                    targetFoV = currentFoV * fpcModSettings.XMLFoViewScrollfactor;
                    isFOVTransitioning = true;
                }
                else if (!isFOVTransitioning && currentFoV != targetFoV)
                    isFOVTransitioning = true;

                UpdateFOVTransition();
            }
            else
            {
                var FoV = mainCamera.fieldOfView;

                if (scroll > 0f && FoV > MIN_FOV)
                    mainCamera.fieldOfView = FoV / fpcModSettings.XMLFoViewScrollfactor;
                else if (scroll < 0f && FoV < MAX_FOV)
                    mainCamera.fieldOfView = FoV * fpcModSettings.XMLFoViewScrollfactor;
            }
            {
                // key movement
                var movementFactor = ((fpcModSettings.XMLKeySpeedUp.IsPressed() ? fpcModSettings.XMLSpeedUpFactor : 1f)
                                     * fpcModSettings.XMLMovementSpeed * Time.deltaTime).FromKmph();

                Vector3 camForward = mainCamera.transform.forward;
                Vector3 camRight = mainCamera.transform.right;
                Vector3 camUp = mainCamera.transform.up;

                var movement = Vector3.zero;
                if (!(KeyCode.LeftControl.KeyPressed() || KeyCode.RightControl.KeyPressed()))
                {

                    if (fpcModSettings.XMLKeyRotateUp.IsPressed()) movement += camForward * movementFactor;
                    if (fpcModSettings.XMLKeyRotateDown.IsPressed()) movement -= camForward * movementFactor;
                    if (fpcModSettings.XMLKeyRotateRight.IsPressed()) movement += camRight * movementFactor;
                    if (fpcModSettings.XMLKeyRotateLeft.IsPressed()) movement -= camRight * movementFactor;
                }
                if (fpcModSettings.XMLKeyMoveUp.IsPressed()) movement += camUp * movementFactor;
                if (fpcModSettings.XMLKeyMoveDown.IsPressed()) movement -= camUp * movementFactor;

                ModSettings.Offset += targetRigidBody.transform.InverseTransformDirection(movement);
            }

            float yawDegree = 0f, pitchDegree = 0f;
            var rotateFactor = fpcModSettings.XMLRotateKeyFactor * Time.deltaTime;
            {

                // key rotation
                if (KeyCode.LeftControl.KeyPressed() || KeyCode.RightControl.KeyPressed())
                {
                    if (fpcModSettings.XMLKeyRotateRight.IsPressed()) yawDegree += rotateFactor;
                    if (fpcModSettings.XMLKeyRotateLeft.IsPressed()) yawDegree -= rotateFactor;
                    if (fpcModSettings.XMLKeyRotateUp.IsPressed()) pitchDegree -= rotateFactor;
                    if (fpcModSettings.XMLKeyRotateDown.IsPressed()) pitchDegree += rotateFactor;
                }
                if (yawDegree == 0f && pitchDegree == 0f && !cursorVisible)
                {
                    // mouse rotation   
                    yawDegree = InputManager.MouseMoveHori * fpcModSettings.XMLRotateSensitivity *
                                (fpcModSettings.XMLInvertRotateHorizontal ? -1f : 1f) * ROTATE_MOUSE_SCALE;
                    pitchDegree = InputManager.MouseMoveVert * fpcModSettings.XMLRotateSensitivity *
                                  (fpcModSettings.XMLInvertRotateVertical ? 1f : -1f) * ROTATE_MOUSE_SCALE;
                }
            }

            if (yawDegree != 0f || pitchDegree != 0f)
            {
                if (Time.time > lastMovedTime + LOOK_RESET_TIME)
                {
                    rotationOffset = rotation;
                }

                lastMovedTime = Time.time;

                var yawRotation = Quaternion.Euler(0f, yawDegree, 0f);
                var pitchRotation = Quaternion.Euler(pitchDegree, 0f, 0f);
                rotationOffset = yawRotation * rotationOffset * pitchRotation;
            }

            // Limit pitch
            var eulerAngles = rotationOffset.eulerAngles;
            if (eulerAngles.x > 180f) eulerAngles.x -= 360f;
            eulerAngles.x = eulerAngles.x.Clamp(ModSettings.Offset.z > -1f ? -FPCModSettings.Instance.XMLMaxPitchDeg : 0f, FPCModSettings.Instance.XMLMaxPitchDeg);
            eulerAngles.z = 0f;
            rotationOffset = Quaternion.Euler(eulerAngles);
        }

        /// <summary>
        /// Updates the camera's FOV during a scroll transition to smoothly adjust to the target FOV.
        /// </summary>
        private void UpdateFOVTransition()
        {
            if (!isFOVTransitioning)
                return;

            if (mainCamera.fieldOfView.AlmostEquals(targetFoV, .1f))
            {
                mainCamera.fieldOfView = targetFoV;
                isFOVTransitioning = false;
            }
            mainCamera.fieldOfView = Mathf.Lerp(mainCamera.fieldOfView, targetFoV, Time.deltaTime * FPCModSettings.Instance.XMLTransSpeed);
        }
    }
}
