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
            public void DisableCam() => DriveController.instance.StopDriving();
            public FPC.FPSCamera.Utils.MathUtils.Positioning GetPositioning() => new FPC.FPSCamera.Utils.MathUtils.Positioning(instance.targetRigidBody.transform.position, instance.targetRigidBody.transform.rotation);
            public float GetSpeed() => instance.targetRigidBody.velocity.magnitude;
            public bool IsValid() => true;
        }

        private const float ROTATE_KEY_SCALE = 100.0f;
        private const float ROTATE_MOUSE_SCALE = 1.0f;
        private const float ZOOM_KEY_SCALE = 10.0f;
        private const float ZOOM_MOUSE_SCALE = 1.0f;
        private const float UNDERGROUND_RENDER_BIAS = 1.0f;
        private const float LOOK_MAX_DIST = 100.0f;
        private const float LOOK_RESET_TIME = 5.0f;

        public static DriveCamController instance { get; private set; }

        private Rigidbody targetRigidBody;
        private Vector3 lastValidDir;
        private Quaternion rotation;
        private Quaternion rotationOffset;
        private float lastMovedTime;
        private float followDistance;

        private Camera mainCamera;
        private int cachedRenderMask;

        private void Awake()
        {
            instance = this;
            enabled = false;
            mainCamera = FPC.FPSCamera.Cam.Controller.GameCamController.Instance.MainCamera;
        }

        public void EnableCam(Rigidbody rigidBody, float distance)
        {
            enabled = true;
            FPC.FPSCamera.Cam.Controller.FPSCamController.Instance.FPSCam = new DriveCam();

            cachedRenderMask = mainCamera.cullingMask;
            targetRigidBody = rigidBody;
            followDistance = Mathf.Clamp(distance, 0.0f, LOOK_MAX_DIST);
            lastValidDir = mainCamera.transform.TransformDirection(Vector3.forward);
            rotation = mainCamera.transform.rotation;
            rotationOffset = Quaternion.identity;
            Logging.KeyMessage("Drive cam enabled");
        }
        public void DisableCam()
        {
            enabled = false;
            mainCamera.cullingMask = cachedRenderMask;
            targetRigidBody = null;
            lastValidDir = Vector3.zero;
            rotation = Quaternion.identity;
            rotationOffset = Quaternion.identity;
            Logging.KeyMessage("Drive cam disabled");
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
        public void UpdateCameraPos()
        {
            Vector3 vehiclePosition = targetRigidBody.transform.TransformPoint(ModSettings.Offset);

            if (Time.time > lastMovedTime + LOOK_RESET_TIME)
            {
                Vector3 vehicleVelocity = targetRigidBody.velocity;
                Vector3 vehicleDir = Vector3.Normalize(vehicleVelocity);

                rotation = mainCamera.transform.rotation;

                if (Vector3.Magnitude(vehicleVelocity) < 1.0 || vehicleDir.y > 0.99f || vehicleDir.y < -0.99f)
                {
                    vehicleDir = lastValidDir;
                }
                lastValidDir = vehicleDir;

                Quaternion targetRotation = Quaternion.identity;
                targetRotation.SetLookRotation(vehicleDir);

                var eulerTmp = rotationOffset.eulerAngles;
                eulerTmp.y = 0f;
                eulerTmp.z = 0f;

                rotationOffset = Quaternion.Euler(eulerTmp);

                targetRotation = targetRotation * rotationOffset;

                rotation = Quaternion.Slerp(rotation, targetRotation, Time.deltaTime * Mathf.Max(Time.deltaTime, FPCModSettings.Instance.XMLTransSpeed));
            }
            else
            {
                rotation = rotationOffset;
            }

            float finalFollowDist = followDistance;
            Utils.MapUtils.RaycastInput input = Utils.MapUtils.GetRaycastInput(vehiclePosition, rotation * Vector3.back, 1000.0f, false);
            input.m_netService.m_service = ItemClass.Service.Road;
            input.m_netService.m_itemLayers = ItemClass.Layer.Default |
                                              ItemClass.Layer.MetroTunnels;
            input.m_netService2.m_service = ItemClass.Service.Beautification;
            if (Utils.MapUtils.RayCast(input, out var output))
            {
                finalFollowDist = Mathf.Min(Vector3.Magnitude(output.m_hitPos - vehiclePosition), finalFollowDist);
            }

            // Apply the calculated position and rotation to the camera. Limit the camera's position to the allowed area.
            mainCamera.transform.rotation = rotation;
            mainCamera.transform.position = CameraController.ClampCameraPosition(vehiclePosition + rotation * new Vector3(0.0f, 0.0f, -finalFollowDist));
        }

        private void UpdateCameraRendering()
        {
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
            var cursorVisible =
             FPCModSettings.Instance.XMLKeyCursorToggle.IsPressed() ^ (FPCModSettings.Instance.XMLShowCursorFollow);

            InputManager.ToggleCursor(cursorVisible);

            if (Input.GetMouseButtonDown(2) || // middle click
                   FPCModSettings.Instance.XMLKeyCamReset.KeyTriggered())
            {
                rotationOffset = Quaternion.identity;
                lastMovedTime = 0f;
            }

            // mouse zoom
            followDistance = followDistance - ZOOM_MOUSE_SCALE * Input.mouseScrollDelta.y;
            followDistance = Mathf.Clamp(followDistance, 0.0f, LOOK_MAX_DIST);

            {
                // key movement
                var movementFactor = ((FPCModSettings.Instance.XMLKeySpeedUp.IsPressed() ? FPCModSettings.Instance.XMLSpeedUpFactor : 1f)
                                     * FPCModSettings.Instance.XMLMovementSpeed * Time.deltaTime).FromKmph();

                Vector3 camForward = mainCamera.transform.forward;
                Vector3 camRight = mainCamera.transform.right;
                Vector3 camUp = mainCamera.transform.up;

                var movement = Vector3.zero;
                if (!(KeyCode.LeftControl.KeyPressed() || KeyCode.RightControl.KeyPressed()))
                {

                    if (FPCModSettings.Instance.XMLKeyRotateUp.IsPressed()) movement += camForward * movementFactor;
                    if (FPCModSettings.Instance.XMLKeyRotateDown.IsPressed()) movement -= camForward * movementFactor;
                    if (FPCModSettings.Instance.XMLKeyRotateRight.IsPressed()) movement += camRight * movementFactor;
                    if (FPCModSettings.Instance.XMLKeyRotateLeft.IsPressed()) movement -= camRight * movementFactor;
                }
                if (FPCModSettings.Instance.XMLKeyMoveUp.IsPressed()) movement += camUp * movementFactor;
                if (FPCModSettings.Instance.XMLKeyMoveDown.IsPressed()) movement -= camUp * movementFactor;

                ModSettings.Offset += targetRigidBody.transform.InverseTransformDirection(movement);
            }

            float yawDegree = 0f, pitchDegree = 0f;
            {
                // mouse rotation
                yawDegree = Input.GetAxis("Mouse X") * ROTATE_MOUSE_SCALE * FPCModSettings.Instance.XMLRotateSensitivity;
                pitchDegree = -Input.GetAxis("Mouse Y") * ROTATE_MOUSE_SCALE * FPCModSettings.Instance.XMLRotateSensitivity;
                // key rotation
                if (KeyCode.LeftControl.KeyPressed() || KeyCode.RightControl.KeyPressed())
                {
                    if (FPCModSettings.Instance.XMLKeyRotateRight.IsPressed()) yawDegree += FPCModSettings.Instance.XMLRotateSensitivity * ROTATE_KEY_SCALE * Time.deltaTime;
                    if (FPCModSettings.Instance.XMLKeyRotateLeft.IsPressed()) yawDegree -= FPCModSettings.Instance.XMLRotateSensitivity * ROTATE_KEY_SCALE * Time.deltaTime;
                    if (FPCModSettings.Instance.XMLKeyRotateUp.IsPressed()) pitchDegree -= FPCModSettings.Instance.XMLRotateSensitivity * ROTATE_KEY_SCALE * Time.deltaTime;
                    if (FPCModSettings.Instance.XMLKeyRotateDown.IsPressed()) pitchDegree += FPCModSettings.Instance.XMLRotateSensitivity * ROTATE_KEY_SCALE * Time.deltaTime;
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
            eulerAngles.x = Mathf.Clamp(eulerAngles.x, -FPCModSettings.Instance.XMLMaxPitchDeg, FPCModSettings.Instance.XMLMaxPitchDeg);
            eulerAngles.z = 0f;
            rotationOffset = Quaternion.Euler(eulerAngles);
        }
    }
}
