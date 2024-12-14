using AlgernonCommons.Patching;
using ICities;
using IOperateIt.UI;
using System.Collections.Generic;
using UnityEngine;

namespace IOperateIt
{
    public class IOperateItLoading : PatcherLoadingBase<OptionsPanel, PatcherBase>
    {
        protected override List<AppMode> PermittedModes => new List<AppMode> { AppMode.Game, AppMode.MapEditor };
        protected override bool CreatedChecksPassed() => FPSCameraAPI.Helper.IsFPSCameraInstalledAndEnabled;
        public override void OnLevelUnloading()
        {
            if (gameObject != null)
            {
                Object.Destroy(gameObject);
                gameObject = null;
            }
            base.OnLevelUnloading();
        }
        protected override void LoadedActions(LoadMode mode)
        {
            base.LoadedActions(mode);
            gameObject = new GameObject("IOperateIt");
            gameObject.AddComponent<MainPanel>();
            gameObject.AddComponent<DriveButtons>();
            gameObject.AddComponent<DriveController>();
        }
        private GameObject gameObject = null;
    }
}
