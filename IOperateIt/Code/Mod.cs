extern alias FPC;
using AlgernonCommons;
using AlgernonCommons.Notifications;
using AlgernonCommons.Patching;
using AlgernonCommons.Translation;
using FPC.FPSCamera.UI;
using ICities;
using IOperateIt.Settings;

namespace IOperateIt
{
    public sealed class Mod : PatcherMod<SettingsPanel, PatcherBase>, IUserMod
    {
        public override string BaseName => "IOperateIt Revisited";
        public override string HarmonyID => "Will258012.IOperateIt";
        public string Description => Translations.Translate("MOD_DESCRIPTION");
        public override void LoadSettings() => ModSettings.Load();
        public override void SaveSettings() => ModSettings.Save();
        public override WhatsNewMessage[] WhatsNewMessages => new WhatsNewMessage[]
        {
            new WhatsNewMessage
            {
                Version = AssemblyUtils.CurrentVersion,
                MessagesAreKeys = true,
                Messages = new string[]
                {
                    "WHATSNEW_L1",
                    "WHATSNEW_L2",
                    "WHATSNEW_L3"
                }
            }
        };
        public override void OnEnabled()
        {
            base.OnEnabled();
            Logging.EventExceptionOccured += (message) => ErrorNotification.ShowNotification(Name, 3371685709, message);
            FPSCameraAPI.Helper.CheckFPSCamera();
        }
    }
}
