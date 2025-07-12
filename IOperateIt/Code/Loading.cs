using AlgernonCommons.Patching;
using ICities;
using IOperateIt.UI;
using IOperateIt.Utils;
using System.Collections.Generic;
using UnityEngine;

namespace IOperateIt
{
    public class Loading : PatcherLoadingBase<OptionsPanel, PatcherBase>
    {
        protected override List<AppMode> PermittedModes => new List<AppMode> { AppMode.Game, AppMode.MapEditor };
        protected override bool CreatedChecksPassed() { return true; }
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
            gameObject.AddComponent<DriveCam>();
        }

        public override void OnCreated(ILoading loading)
        {
            base.OnCreated(loading);
            ModSupport.Initialize();
        }

        private GameObject gameObject = null;
    }
}
