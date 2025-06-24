using AlgernonCommons;
using ColossalFramework;
using ColossalFramework.UI;
using HarmonyLib;
using ICities;
using System.Reflection;
using UnityEngine;

namespace IOperateIt
{
    public class DriveCam : MonoBehaviour
    {
        private const float UNDERGROUND_RENDER_BIAS = 1.0f;
        private const float NEAR_CLIP = 1.0f;

        public static DriveCam instance { get; private set; }

        private Rigidbody m_targetRigidBody;
        private Vector3 m_targetDir;
        private Vector3 m_lastValidDir;
        private Quaternion m_rotation;
        private Quaternion m_rotationOffset;
        private int m_renderMask;
        private Rect m_cameraRect;
        private float m_nearClip;

        private Camera m_mainCamera;

        private void Awake()
        {
            instance = this;
            enabled = false;
            Logging.Message("Setting up the Camera");
        }

        public void EnableCam(Rigidbody rigidBody)
        {
            enabled = true;
            ConfigureCamera();

            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;

            m_targetRigidBody = rigidBody;
            m_targetDir = m_mainCamera.transform.InverseTransformDirection(Vector3.forward);
            m_lastValidDir = m_targetDir;
            m_rotation = m_mainCamera.transform.rotation;
            m_rotationOffset = Quaternion.identity;
            Logging.KeyMessage("Drive cam enabled");
        }
        public void DisableCam()
        {
            enabled = false;
            RestoreCamera();

            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            
            m_targetRigidBody = null;
            m_targetDir = Vector3.zero;
            m_lastValidDir = Vector3.zero;
            m_rotation = Quaternion.identity;
            m_rotationOffset = Quaternion.identity;
            Logging.KeyMessage("Drive cam disabled");
        }
        private static void SetUIVisibility(bool visibility)
        {
            Singleton<NotificationManager>.instance.NotificationsVisible = visibility;
            Singleton<GameAreaManager>.instance.BordersVisible = visibility;
            Singleton<DistrictManager>.instance.NamesVisible = visibility;
            Singleton<PropManager>.instance.MarkersVisible = visibility;
            Singleton<GuideManager>.instance.TutorialDisabled = !visibility;
            Singleton<DisasterManager>.instance.MarkersVisible = visibility;
            Singleton<NetManager>.instance.RoadNamesVisible = visibility;
            UIView.Show(visibility);
        }

        private void ConfigureCamera()
        {
            ToolsModifierControl.cameraController.enabled = false;
            SetUIVisibility(false);

            m_mainCamera = Singleton<RenderManager>.instance.CurrentCameraInfo.m_camera;

            m_renderMask = m_mainCamera.cullingMask;

            m_nearClip = m_mainCamera.nearClipPlane;
            m_mainCamera.nearClipPlane = NEAR_CLIP;

            m_cameraRect = Camera.main.rect;
            Camera.main.rect = CameraController.kFullScreenRect;
        }

        private void RestoreCamera()
        {
            Camera.main.rect = m_cameraRect;
            m_mainCamera.nearClipPlane = m_nearClip;
            m_mainCamera.cullingMask = m_renderMask;
            SetUIVisibility(true);
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
            Transform cameraTransform = m_mainCamera.transform;

            Vector3 vehiclePosition = m_targetRigidBody.transform.position;
            Vector3 vehicleVelocity = m_targetRigidBody.velocity;

            if (Vector3.Magnitude(vehicleVelocity) < 1.0)
            {
                m_targetDir = m_lastValidDir;
            }
            else
            {
                m_targetDir = Vector3.Normalize(vehicleVelocity);
                if (m_targetDir.y > 0.999f || m_targetDir.y < -0.999f)
                {
                    m_targetDir = Vector3.Normalize(m_targetRigidBody.transform.InverseTransformDirection(Vector3.forward));
                }
                if (m_targetDir.y > 0.999f || m_targetDir.y < -0.999f)
                {
                    m_targetDir = m_lastValidDir;
                }
                m_lastValidDir = m_targetDir;
            }
            Quaternion targetRotation = Quaternion.identity;
            targetRotation.SetLookRotation(m_targetDir);
            targetRotation *= m_rotationOffset;

            // Apply the calculated position and rotation to the camera. Limit the camera's position to the allowed area.
            targetRotation = Quaternion.Slerp(cameraTransform.rotation, targetRotation, Time.deltaTime);

            cameraTransform.position = CameraController.ClampCameraPosition(vehiclePosition + targetRotation * Settings.ModSettings.Offset);
            cameraTransform.rotation = targetRotation;


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
                Cursor.visible = !Cursor.visible;

            if (Input.GetMouseButtonDown(2) || // middle click
                Input.GetKeyDown((KeyCode)Settings.ModSettings.KeyCamReset.Key))
            {
                m_rotationOffset = Quaternion.identity;
            }
            float yawDegree = 0f, pitchDegree = 0f;
            { // key rotation
                if (Input.GetKey((KeyCode)Settings.ModSettings.KeyCamRotateRight.Key)) yawDegree += Settings.ModSettings.CamKeyRotateSensitivity * Time.deltaTime;
                if (Input.GetKey((KeyCode)Settings.ModSettings.KeyCamRotateLeft.Key)) yawDegree -= Settings.ModSettings.CamKeyRotateSensitivity * Time.deltaTime;
                if (Input.GetKey((KeyCode)Settings.ModSettings.KeyCamRotateUp.Key)) pitchDegree -= Settings.ModSettings.CamKeyRotateSensitivity * Time.deltaTime;
                if (Input.GetKey((KeyCode)Settings.ModSettings.KeyCamRotateDown.Key)) pitchDegree += Settings.ModSettings.CamKeyRotateSensitivity * Time.deltaTime;

                if (yawDegree == 0f && pitchDegree == 0f)
                {
                    // mouse rotation
                    yawDegree = Input.GetAxis("Mouse X") * Settings.ModSettings.CamMouseRotateSensitivity;
                    pitchDegree = Input.GetAxis("Mouse Y") * Mathf.Sign(Settings.ModSettings.Offset.z) * Settings.ModSettings.CamMouseRotateSensitivity;
                }
            }

            var yawRotation = Quaternion.Euler(0f, yawDegree, 0f);
            var pitchRotation = Quaternion.Euler(pitchDegree, 0f, 0f);
            m_rotationOffset = yawRotation * m_rotationOffset * pitchRotation;

            // Limit pitch
            var eulerAngles = m_rotationOffset.eulerAngles;
            if (eulerAngles.x > 180f) eulerAngles.x -= 360f;
            eulerAngles.x = Mathf.Clamp(eulerAngles.x, -Settings.ModSettings.CamMaxPitchDeg, Settings.ModSettings.CamMaxPitchDeg);
            eulerAngles.z = 0f;
            m_rotationOffset = Quaternion.Euler(eulerAngles);
        }
    }
}
