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
            public FPC.FPSCamera.Utils.MathUtils.Positioning GetPositioning() => new FPC.FPSCamera.Utils.MathUtils.Positioning(instance.m_targetRigidBody.transform.position, instance.m_targetRigidBody.transform.rotation);
            public float GetSpeed() => instance.m_targetRigidBody.velocity.magnitude;
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

        private Rigidbody m_targetRigidBody;
        private Vector3 m_lastValidDir;
        private Quaternion m_rotation;
        private Quaternion m_rotationOffset;
        private float m_lastMovedTime;
        private float m_followDistance;

        private Camera m_mainCamera;
        private int m_cachedRenderMask;

        private void Awake()
        {
            instance = this;
            enabled = false;
            m_mainCamera = FPC.FPSCamera.Cam.Controller.GameCamController.Instance.MainCamera;
        }

        public void EnableCam(Rigidbody rigidBody, float distance)
        {
            enabled = true;
            FPC.FPSCamera.Cam.Controller.FPSCamController.Instance.FPSCam = new DriveCam();

            m_cachedRenderMask = m_mainCamera.cullingMask;
            m_targetRigidBody = rigidBody;
            m_followDistance = Mathf.Clamp(distance, 0.0f, LOOK_MAX_DIST);
            m_lastValidDir = m_mainCamera.transform.TransformDirection(Vector3.forward);
            m_rotation = m_mainCamera.transform.rotation;
            m_rotationOffset = Quaternion.identity;
            Logging.KeyMessage("Drive cam enabled");
        }
        public void DisableCam()
        {
            enabled = false;
            m_mainCamera.cullingMask = m_cachedRenderMask;
            m_targetRigidBody = null;
            m_lastValidDir = Vector3.zero;
            m_rotation = Quaternion.identity;
            m_rotationOffset = Quaternion.identity;
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
            Vector3 vehiclePosition = m_targetRigidBody.transform.TransformPoint(ModSettings.Offset);

            if (Time.time > m_lastMovedTime + LOOK_RESET_TIME)
            {
                Vector3 vehicleVelocity = m_targetRigidBody.velocity;
                Vector3 vehicleDir = Vector3.Normalize(vehicleVelocity);

                m_rotation = m_mainCamera.transform.rotation;

                if (Vector3.Magnitude(vehicleVelocity) < 1.0 || vehicleDir.y > 0.99f || vehicleDir.y < -0.99f)
                {
                    vehicleDir = m_lastValidDir;
                }
                m_lastValidDir = vehicleDir;

                Quaternion targetRotation = Quaternion.identity;
                targetRotation.SetLookRotation(vehicleDir);

                var eulerTmp = m_rotationOffset.eulerAngles;
                eulerTmp.y = 0f;
                eulerTmp.z = 0f;

                m_rotationOffset = Quaternion.Euler(eulerTmp);

                targetRotation = targetRotation * m_rotationOffset;

                m_rotation = Quaternion.Slerp(m_rotation, targetRotation, Time.deltaTime * Mathf.Max(Time.deltaTime, FPCModSettings.Instance.XMLTransSpeed));
            }
            else
            {
                m_rotation = m_rotationOffset;
            }

            float finalFollowDist = m_followDistance;
            Utils.MapUtils.RaycastInput input = Utils.MapUtils.GetRaycastInput(vehiclePosition, m_rotation * Vector3.back, 1000.0f, false);
            input.m_netService.m_service = ItemClass.Service.Road;
            input.m_netService.m_itemLayers = ItemClass.Layer.Default |
                                              ItemClass.Layer.MetroTunnels;
            input.m_netService2.m_service = ItemClass.Service.Beautification;
            if (Utils.MapUtils.RayCast(input, out var output))
            {
                finalFollowDist = Mathf.Min(Vector3.Magnitude(output.m_hitPos - vehiclePosition), finalFollowDist);
            }

            // Apply the calculated position and rotation to the camera. Limit the camera's position to the allowed area.
            m_mainCamera.transform.rotation = m_rotation;
            m_mainCamera.transform.position = CameraController.ClampCameraPosition(vehiclePosition + m_rotation * new Vector3(0.0f, 0.0f, -finalFollowDist));
        }

        private void UpdateCameraRendering()
        {
            var terrainHeight = Singleton<TerrainManager>.instance.SampleDetailHeightSmooth(m_mainCamera.transform.position);

            if (terrainHeight + UNDERGROUND_RENDER_BIAS > m_mainCamera.transform.position.y)
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
                m_rotationOffset = Quaternion.identity;
                m_lastMovedTime = 0f;
            }

            // mouse zoom
            m_followDistance = m_followDistance - ZOOM_MOUSE_SCALE * Input.mouseScrollDelta.y;
            m_followDistance = Mathf.Clamp(m_followDistance, 0.0f, LOOK_MAX_DIST);

            {
                // key movement
                var movementFactor = ((FPCModSettings.Instance.XMLKeySpeedUp.IsPressed() ? FPCModSettings.Instance.XMLSpeedUpFactor : 1f)
                                     * FPCModSettings.Instance.XMLMovementSpeed * Time.deltaTime).FromKmph();

                Vector3 camForward = m_mainCamera.transform.forward;
                Vector3 camRight = m_mainCamera.transform.right;
                Vector3 camUp = m_mainCamera.transform.up;

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

                ModSettings.Offset += m_targetRigidBody.transform.InverseTransformDirection(movement);
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
                if (Time.time > m_lastMovedTime + LOOK_RESET_TIME)
                {
                    m_rotationOffset = m_rotation;
                }

                m_lastMovedTime = Time.time;

                var yawRotation = Quaternion.Euler(0f, yawDegree, 0f);
                var pitchRotation = Quaternion.Euler(pitchDegree, 0f, 0f);
                m_rotationOffset = yawRotation * m_rotationOffset * pitchRotation;
            }

            // Limit pitch
            var eulerAngles = m_rotationOffset.eulerAngles;
            if (eulerAngles.x > 180f) eulerAngles.x -= 360f;
            eulerAngles.x = Mathf.Clamp(eulerAngles.x, -FPCModSettings.Instance.XMLMaxPitchDeg, FPCModSettings.Instance.XMLMaxPitchDeg);
            eulerAngles.z = 0f;
            m_rotationOffset = Quaternion.Euler(eulerAngles);
        }
    }
}
