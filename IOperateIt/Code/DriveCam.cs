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
            Logging.KeyMessage("Drive cam started");
        }
        public bool IsActivated { get; private set; }
        public void DisableCam()
        {
            IsActivated = false;
            DriveController.Instance.DestroyVehicle();
        }
        public FPSCamera.FPSCamera.Utils.MathUtils.Positioning GetPositioning()
            => new FPSCamera.FPSCamera.Utils.MathUtils.Positioning(DriveController.Instance.gameObject.transform.position, DriveController.Instance.gameObject.transform.rotation);
        public float GetSpeed() => DriveController.Instance.Speed;
        public bool IsValid() => true;
    }
}
