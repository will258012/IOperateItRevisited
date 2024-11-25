﻿extern alias FPSCamera;
using AlgernonCommons;
using AlgernonCommons.Notifications;
using AlgernonCommons.Patching;
using AlgernonCommons.Translation;
using ICities;

namespace IOperateIt
{
    public sealed class Mod : PatcherMod<OptionsPanel, PatcherBase>, IUserMod
    {
        public override string BaseName => "IOperateIt Revisited";
        public override string HarmonyID => "Will258012.IOperateIt";

        public string Description => Translations.Translate("MOD_DESCRIPTION");
        public override void SaveSettings() => Settings.ModSettings.Save();

        public override void LoadSettings() => Settings.ModSettings.Load();

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
    }
}