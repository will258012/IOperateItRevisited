using HarmonyLib;
using IOperateIt.UI;

namespace IOperateIt.Patches
{
    [HarmonyPatch(typeof(GameKeyShortcuts), "Escape")]
    [HarmonyAfter("Will258012.FPSCamera.Continued")]
    internal class EscHandler
    {
        [HarmonyPrefix]
        public static bool ESCPatch() => !MainPanel.Instance.OnEsc();
    }
}