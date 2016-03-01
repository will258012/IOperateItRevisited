using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEngine;

namespace IOperateIt.Utils
{
    class LoggerUtils
    {
            private static readonly string TAG = string.Format("{0}: ", Assembly.GetExecutingAssembly().GetName().Name);

            public static void Log(string message)
            {
                Debug.Log(TAG + message);
            }

            public static void LogWarning(string message)
            {
                Debug.LogWarning(TAG + message);
            }

            public static void LogError(string message)
            {
                Debug.LogError(TAG + message);
            }

            public static void LogException(Exception e)
            {
                Debug.LogException(e);
            }

            public static void LogToConsole(string message)
            {
                DebugOutputPanel.AddMessage(ColossalFramework.Plugins.PluginManager.MessageType.Message, TAG + message);
            }

            public static void LogWarningToConsole(string message)
            {
                DebugOutputPanel.AddMessage(ColossalFramework.Plugins.PluginManager.MessageType.Warning, TAG + message);
            }

            public static void LogErrorToConsole(string message)
            {
                DebugOutputPanel.AddMessage(ColossalFramework.Plugins.PluginManager.MessageType.Error, TAG + message);
            }
    }
}
