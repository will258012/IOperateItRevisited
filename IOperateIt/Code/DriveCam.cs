using AlgernonCommons;
using ColossalFramework;
using ColossalFramework.UI;
using IOperateIt.Settings;
using IOperateIt.Utils;
using UnityEngine;

namespace IOperateIt
{
    public class DriveCam : MonoBehaviour
    {
        private const float ROTATE_KEY_SCALE = 100.0f;
        private const float ROTATE_MOUSE_SCALE = 1.0f;
        private const float ZOOM_KEY_SCALE = 10.0f;
        private const float ZOOM_MOUSE_SCALE = 1.0f;
        private const float UNDERGROUND_RENDER_BIAS = 1.0f;
        private const float LOOK_MAX_DIST = 100.0f;
        private const float LOOK_RESET_TIME = 5.0f;
        private const float NEAR_CLIP = 1.5f;

        public static DriveCam instance { get; private set; }

        private Rigidbody m_targetRigidBody;
        private Vector3 m_lastValidDir;
        private Quaternion m_rotation;
        private Quaternion m_rotationOffset;
        private float m_lastMovedTime;
        private float m_followDistance;

        private Camera m_mainCamera;
        private Camera m_uiCamera;
        private int m_cachedRenderMask;
        private Rect m_cachedCameraRect;
        private float m_cachedNearClip;
        private float m_cachedFOV;
        private CameraController.SavedCameraView m_savedView;

        private void Awake()
        {
            instance = this;
            enabled = false;
            Logging.Message("Setting up the Camera");
        }

        public void EnableCam(Rigidbody rigidBody, float distance)
        {
            enabled = true;
            ConfigureCamera();

            SetCursorVisibility(false);

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
            RestoreCamera();
            SetCursorVisibility(true);
            
            m_targetRigidBody = null;
            m_lastValidDir = Vector3.zero;
            m_rotation = Quaternion.identity;
            m_rotationOffset = Quaternion.identity;
            Logging.KeyMessage("Drive cam disabled");
        }
        private void SetUIVisibility(bool visibility)
        {
            Singleton<NotificationManager>.instance.NotificationsVisible = visibility;
            Singleton<GameAreaManager>.instance.BordersVisible = visibility;
            Singleton<DistrictManager>.instance.NamesVisible = visibility;
            Singleton<PropManager>.instance.MarkersVisible = visibility;
            Singleton<GuideManager>.instance.TutorialDisabled = !visibility;
            Singleton<DisasterManager>.instance.MarkersVisible = visibility;
            Singleton<NetManager>.instance.RoadNamesVisible = visibility;
            m_uiCamera.enabled = visibility;
        }

        private void SetCursorVisibility(bool visibility)
        {
            Cursor.visible = visibility;
            if (!visibility)
            {
                Cursor.lockState = CursorLockMode.Locked;
            }
            else
            {
                Cursor.lockState = CursorLockMode.None;
            }
        }

        private void ConfigureCamera()
        {
            ToolsModifierControl.cameraController.enabled = false;
            m_savedView = new CameraController.SavedCameraView(ToolsModifierControl.cameraController);

            m_mainCamera = Singleton<RenderManager>.instance.CurrentCameraInfo.m_camera;

            m_uiCamera = UIView.GetView("").uiCamera;

            m_cachedRenderMask = m_mainCamera.cullingMask;

            m_cachedFOV = m_mainCamera.fieldOfView;
            m_mainCamera.fieldOfView = ModSettings.CamFieldOfView;

            m_cachedNearClip = m_mainCamera.nearClipPlane;
            m_mainCamera.nearClipPlane = NEAR_CLIP;

            m_cachedCameraRect = Camera.main.rect;
            Camera.main.rect = CameraController.kFullScreenRect;

            SetUIVisibility(false);
        }

        private void RestoreCamera()
        {
            Camera.main.rect = m_cachedCameraRect;
            m_mainCamera.nearClipPlane = m_cachedNearClip;
            m_mainCamera.fieldOfView = m_cachedFOV;
            m_mainCamera.cullingMask = m_cachedRenderMask;
            SetUIVisibility(true);

            m_savedView.m_position = m_mainCamera.transform.position;
            m_savedView.m_height = m_savedView.m_position.y + 5.0f;
            m_savedView.m_size = ToolsModifierControl.cameraController.m_minDistance;
            Vector2 convertedAngle = new Vector3(m_mainCamera.transform.rotation.eulerAngles.y, m_mainCamera.transform.rotation.eulerAngles.x);
            convertedAngle.y = Mathf.Repeat(convertedAngle.y + 180f, 360f) - 180f;
            if (convertedAngle.y < -90f)
            {
                convertedAngle.y = -convertedAngle.y - 180f;
                convertedAngle.x += 180f;
            }
            else if (convertedAngle.y > 90f)
            {
                convertedAngle.y = -convertedAngle.y + 180f;
                convertedAngle.x += 180f;
            }
            convertedAngle.y = Mathf.Clamp(convertedAngle.y, 0f, 90f);
            convertedAngle.x = Mathf.Repeat(convertedAngle.x + 180f, 360f) - 180f;
            m_savedView.m_angle = convertedAngle;

            ToolsModifierControl.cameraController.Reset(Vector3.zero, (SimulationManager.UpdateMode)m_savedView.m_mode, m_savedView);
            ToolsModifierControl.cameraController.enabled = true;
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
            Vector3 vehiclePosition = m_targetRigidBody.transform.TransformPoint(Settings.ModSettings.Offset);

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

                m_rotation = Quaternion.Slerp(m_rotation, targetRotation, Time.deltaTime / Mathf.Max(Time.deltaTime, ModSettings.CamSmoothing));
            }
            else
            {
                m_rotation = m_rotationOffset;
            }

            float finalFollowDist = m_followDistance;
            MapUtils.RaycastInput input = MapUtils.GetRaycastInput(vehiclePosition, m_rotation * Vector3.back, 1000.0f, false);
            input.m_netService.m_service = ItemClass.Service.Road;
            input.m_netService.m_itemLayers = ItemClass.Layer.Default |
                                              ItemClass.Layer.MetroTunnels;
            input.m_netService2.m_service = ItemClass.Service.Beautification;
            if (MapUtils.RayCast(input, out var output))
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
            if (Input.GetKeyDown((KeyCode)Settings.ModSettings.KeyCamCursorToggle.Key))
            {
                bool visUpdate = !Cursor.visible;
                SetUIVisibility(visUpdate); 
                SetCursorVisibility(visUpdate);
            }

            if (Input.GetMouseButtonDown(2) || // middle click
                Input.GetKeyDown((KeyCode)Settings.ModSettings.KeyCamReset.Key))
            {
                m_rotationOffset = Quaternion.identity;
                m_lastMovedTime = 0f;
            }

            // mouse zoom
            m_followDistance = m_followDistance - ZOOM_MOUSE_SCALE * Input.mouseScrollDelta.y;

            // key zoom
            if (Input.GetKey((KeyCode)Settings.ModSettings.KeyCamZoomIn.Key)) m_followDistance -= ZOOM_KEY_SCALE * Time.deltaTime;
            if (Input.GetKey((KeyCode)Settings.ModSettings.KeyCamZoomOut.Key)) m_followDistance += ZOOM_KEY_SCALE * Time.deltaTime;

            m_followDistance = Mathf.Clamp(m_followDistance, 0.0f, LOOK_MAX_DIST);

            float yawDegree = 0f, pitchDegree = 0f;
            {
                // mouse rotation
                yawDegree = Input.GetAxis("Mouse X") * ROTATE_MOUSE_SCALE * Settings.ModSettings.CamMouseRotateSensitivity;
                pitchDegree = -Input.GetAxis("Mouse Y") * ROTATE_MOUSE_SCALE * Settings.ModSettings.CamMouseRotateSensitivity;

                // key rotation
                if (Input.GetKey((KeyCode)Settings.ModSettings.KeyCamRotateRight.Key)) yawDegree += Settings.ModSettings.CamKeyRotateSensitivity * ROTATE_KEY_SCALE * Time.deltaTime;
                if (Input.GetKey((KeyCode)Settings.ModSettings.KeyCamRotateLeft.Key)) yawDegree -= Settings.ModSettings.CamKeyRotateSensitivity * ROTATE_KEY_SCALE * Time.deltaTime;
                if (Input.GetKey((KeyCode)Settings.ModSettings.KeyCamRotateUp.Key)) pitchDegree -= Settings.ModSettings.CamKeyRotateSensitivity * ROTATE_KEY_SCALE * Time.deltaTime;
                if (Input.GetKey((KeyCode)Settings.ModSettings.KeyCamRotateDown.Key)) pitchDegree += Settings.ModSettings.CamKeyRotateSensitivity * ROTATE_KEY_SCALE * Time.deltaTime;
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
            eulerAngles.x = Mathf.Clamp(eulerAngles.x, -Settings.ModSettings.CamMaxPitchDeg, Settings.ModSettings.CamMaxPitchDeg);
            eulerAngles.z = 0f;
            m_rotationOffset = Quaternion.Euler(eulerAngles);
        }
    }
}
