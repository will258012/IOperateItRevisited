extern alias FPSCamera;
using HarmonyLib;
using IOperateIt.UI;

namespace IOperateIt.Patches
{
    [HarmonyPatch(typeof(GameKeyShortcuts), "Escape")]
    [HarmonyAfter("Will258012.FPSCamera.Continued")]
    internal class EscHandler
    {
        [HarmonyPrefix]
        public static bool ESCPatch() =>
            // cancel calling <Escape> if FPSCamera consumes it
            !MainPanel.Instance.OnEsc();
    }
}