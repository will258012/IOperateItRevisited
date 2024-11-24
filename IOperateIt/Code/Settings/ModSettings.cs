using AlgernonCommons.Keybinding;
using AlgernonCommons.XML;
using ColossalFramework.IO;
using System.IO;
using System.Xml.Serialization;
using UnityEngine;

namespace IOperateIt.Settings
{
    [XmlRoot("IOperateIt")]
    public sealed class ModSettings : SettingsXMLBase
    {
        [XmlIgnore]
        private static readonly string SettingsFileName = Path.Combine(DataLocation.localApplicationData, "IOperateIt.xml");

        internal static void Load() => XMLFileUtils.Load<ModSettings>(SettingsFileName);

        internal static void Save() => XMLFileUtils.Save<ModSettings>(SettingsFileName);

        [XmlElement("MaxVelocity")]
        public float XMLMaxVelocity { get => MaxVelocity; set => MaxVelocity = value; }
        [XmlIgnore]
        internal static float MaxVelocity = 125f;

        [XmlElement("AccelerationForce")]
        public float XMLAccelerationForce { get => AccelerationForce; set => AccelerationForce = value; }
        [XmlIgnore]
        internal static float AccelerationForce = 100f;

        [XmlElement("BreakingForce")]
        public float XMLBreakingForce { get => BreakingForce; set => BreakingForce = value; }
        [XmlIgnore]
        internal static float BreakingForce = 35f;

        [XmlElement("Offset")]
        public Vector3 XMLOffset { get => Offset; set => Offset = value; }
        [XmlIgnore]
        internal static Vector3 Offset = new Vector3(0f, 2f, 2f);

        [XmlElement("KeyUUIToggle")]
        public Keybinding XMLKeyUUIToggle { get => KeyUUIToggle; set => KeyUUIToggle = value; }
        [XmlIgnore]
        internal static Keybinding KeyUUIToggle = new Keybinding(KeyCode.D, false, true, false);

        [XmlElement("KeyLightToggle")]
        public KeyOnlyBinding XMLKeyLightToggle { get => KeyLightToggle; set => KeyLightToggle = value; }
        [XmlIgnore]
        internal static KeyOnlyBinding KeyLightToggle = new KeyOnlyBinding(KeyCode.H);

        [XmlElement("MainButtonPos")]
        public Vector2 XMLMainButtonPos { get => MainButtonPos; set => MainButtonPos = value; }
        [XmlIgnore]
        internal static Vector2 MainButtonPos = new Vector2(0f, 0f);
    }
}
