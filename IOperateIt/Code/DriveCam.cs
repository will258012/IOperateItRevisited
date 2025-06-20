extern alias FPSCamera;

using AlgernonCommons;
using FPSCamera.FPSCamera.Cam;

namespace IOperateIt
{
    public class DriveCam : IFPSCam
    {
        public DriveCam()
        {
            IsActivated = true;
            UnityEngine.Cursor.lockState = UnityEngine.CursorLockMode.Locked;
            Logging.KeyMessage("Drive cam started");
        }
        public bool IsActivated { get; private set; }
        public void DisableCam()
        {
            IsActivated = false;
            DriveController.instance.StopDriving();
            UnityEngine.Cursor.lockState = UnityEngine.CursorLockMode.None;
        }
        public FPSCamera.FPSCamera.Utils.MathUtils.Positioning GetPositioning()
            => new FPSCamera.FPSCamera.Utils.MathUtils.Positioning(DriveController.instance.gameObject.transform.position, DriveController.instance.gameObject.transform.rotation);
        public float GetSpeed() => DriveController.instance.m_speed;
        public bool IsValid() => true;
    }
}
