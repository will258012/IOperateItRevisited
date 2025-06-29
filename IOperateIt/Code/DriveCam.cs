extern alias FPSCamera;

using AlgernonCommons;
using FPSCamera.FPSCamera.Cam;

namespace IOperateIt
{
    public class DriveCam : IFPSCam
    {
        public DriveCam() => Logging.KeyMessage("Drive cam started");
        public void DisableCam() => DriveController.Instance.DestroyVehicle();
        public FPSCamera.FPSCamera.Utils.MathUtils.Positioning GetPositioning()
            => new FPSCamera.FPSCamera.Utils.MathUtils.Positioning(DriveController.Instance.gameObject.transform.position, DriveController.Instance.gameObject.transform.rotation);
        public float GetSpeed() => DriveController.Instance.Speed;
        public bool IsValid() => true;
    }
}
