using HarmonyLib;
using IOperateIt.UI;
using IOperateIt.Utils;
using System.Collections.Generic;
using System.Reflection;

namespace IOperateIt.Patches
{
    [HarmonyPatch]
    [HarmonyAfter("Will258012.FPSCamera.Continued")]
    internal class ESCPatches
    {
        private static readonly MethodBase[] TargetMethods = {
            AccessTools.Method(typeof(GameKeyShortcuts), "Escape"),
            AccessTools.Method(typeof(MapEditorKeyShortcuts), "Escape"),
            AccessTools.Method(typeof(GameKeyShortcuts), "SteamEscape"),
            AccessTools.Method(typeof(MapEditorKeyShortcuts), "SteamEscape"),
        };
        [HarmonyTargetMethods]
        private static IEnumerable<MethodBase> TargetMethodsGetter() => TargetMethods;

        [HarmonyPrefix]
        private static bool HandleEscape()
        {
            if (MainPanel.instance?.OnEsc() ?? false) return false;
            if (ToolsModifierControl.GetCurrentTool<RoadSelectTool>() != null)
            {
                ToolsModifierControl.SetTool<DefaultTool>();
                return false;
            }

            return true;
        }
    }
}