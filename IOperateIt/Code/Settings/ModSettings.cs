using AlgernonCommons;
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

        // Remember edit values here if the settings have edited!
        internal static void ResetToDefaults()
        {
            AlgernonCommons.Translation.Translations.CurrentLanguage = "default";
            Logging.DetailLogging = false;
            AlgernonCommons.Notifications.WhatsNew.LastNotifiedVersionString = "0.0";

            UndergroundRendering = true;

            MaxVelocity = 125f;
            EnginePower = 225f;
            BrakingForce = 30f;
            BrakingABS = true;
            DownForce = 5.0f;
            DriveBias = 0.5f;
            BrakeBias = 0.7f;
            GripCoeffS = 1.0f;
            GripCoeffK = 0.8f;
            SpringDamp = 6.0f;
            SpringOffset = -0.1f;
            MassFactor = 85.0f;
            MassCenterHeight = 0.1f;
            MassCenterBias = 0.6f;
            Offset = new Vector3(0f, 2f, -5f);
            BuildingCollision = true;
            VehicleCollision = true;
            Utils.UUISupport.UUIKey.Keybinding = new Keybinding(KeyCode.D, false, true, false);
            KeyLightToggle = new KeyOnlyBinding(KeyCode.H);
            KeySirenToggle = new KeyOnlyBinding(KeyCode.G);
            KeyUnstuck = new KeyOnlyBinding(KeyCode.R);
            MainButtonPos = new Vector3(0f, 0f);
        }
        [XmlElement("UndergroundRendering")]
        public bool XMLUndergroundRendering { get => UndergroundRendering; set => UndergroundRendering = value; }
        [XmlIgnore]
        internal static bool UndergroundRendering = true;

        [XmlElement("MaxVelocity")]
        public float XMLMaxVelocity { get => MaxVelocity; set => MaxVelocity = value; }
        [XmlIgnore]
        internal static float MaxVelocity = 125f;

        [XmlElement("EnginePower")]
        public float XMLEnginePower { get => EnginePower; set => EnginePower = value; }
        [XmlIgnore]
        internal static float EnginePower = 225f;

        [XmlElement("BrakingForce")]
        public float XMLBrakingForce { get => BrakingForce; set => BrakingForce = value; }
        [XmlIgnore]
        internal static float BrakingForce = 30f;

        [XmlElement("BrakingABS")]
        public bool XMLBrakingABS { get => BrakingABS; set => BrakingABS = value; }
        [XmlIgnore]
        internal static bool BrakingABS = true;

        [XmlElement("DownForce")]
        public float XMLDownForce { get => DownForce; set => DownForce = value; }
        [XmlIgnore]
        internal static float DownForce = 5.0f;

        [XmlElement("DriveBias")]
        public float XMLDriveBias { get => DriveBias; set => DriveBias = value; }
        [XmlIgnore]
        internal static float DriveBias = 0.5f;

        [XmlElement("BrakeBias")]
        public float XMLBrakeBias { get => BrakeBias; set => BrakeBias = value; }
        [XmlIgnore]
        internal static float BrakeBias = 0.7f;

        [XmlElement("GripCoeffS")]
        public float XMLGripCoeffS { get => GripCoeffS; set => GripCoeffS = value; }
        [XmlIgnore]
        internal static float GripCoeffS = 1.0f;

        [XmlElement("GripCoeffK")]
        public float XMLGripCoeffK { get => GripCoeffK; set => GripCoeffK = value; }
        [XmlIgnore]
        internal static float GripCoeffK = 0.8f;

        [XmlElement("SpringDamp")]
        public float XMLSpringDamp { get => SpringDamp; set => SpringDamp = value; }
        [XmlIgnore]
        internal static float SpringDamp = 6.0f;

        [XmlElement("SpringOffset")]
        public float XMLSpringOffset { get => SpringOffset; set => SpringOffset = value; }
        [XmlIgnore]
        internal static float SpringOffset = -0.1f;

        [XmlElement("MassFactor")]
        public float XMLMassFactor { get => MassFactor; set => MassFactor = value; }
        [XmlIgnore]
        internal static float MassFactor = 85.0f;

        [XmlElement("MassCenterHeight")]
        public float XMLMassCenterHeight { get => MassCenterHeight; set => MassCenterHeight = value; }
        [XmlIgnore]
        internal static float MassCenterHeight = 0.1f;

        [XmlElement("MassCenterBias")]
        public float XMLMassCenterBias { get => MassCenterBias; set => MassCenterBias = value; }
        [XmlIgnore]
        internal static float MassCenterBias = 0.6f;

        [XmlElement("Offset")]
        public Vector3 XMLOffset { get => Offset; set => Offset = value; }
        [XmlIgnore]
        internal static Vector3 Offset = new Vector3(0f, 2f, -5f);

        [XmlElement("BuildingCollision")]
        public bool XMLBuildingCollision { get => BuildingCollision; set => BuildingCollision = value; }
        [XmlIgnore]
        internal static bool BuildingCollision = true;

        [XmlElement("VehicleCollision")]
        public bool XMLVehicleCollision { get => VehicleCollision; set => VehicleCollision = value; }
        [XmlIgnore]
        internal static bool VehicleCollision = true;

        [XmlElement("KeyUUIToggle")]
        public Keybinding XMLKeyUUIToggle { get => Utils.UUISupport.UUIKey.Keybinding; set => Utils.UUISupport.UUIKey.Keybinding = value; }

        [XmlElement("KeyLightToggle")]
        public KeyOnlyBinding XMLKeyLightToggle { get => KeyLightToggle; set => KeyLightToggle = value; }
        [XmlIgnore]
        internal static KeyOnlyBinding KeyLightToggle = new KeyOnlyBinding(KeyCode.H);

        [XmlElement("KeySirenToggle")]
        public KeyOnlyBinding XMLKeySirenToggle { get => KeySirenToggle; set => KeySirenToggle = value; }
        [XmlIgnore]
        internal static KeyOnlyBinding KeySirenToggle = new KeyOnlyBinding(KeyCode.G);

        [XmlElement("KeyUnstuck")]
        public KeyOnlyBinding XMLKeyUnstuck { get => KeyUnstuck; set => KeyUnstuck = value; }
        [XmlIgnore]
        internal static KeyOnlyBinding KeyUnstuck = new KeyOnlyBinding(KeyCode.R);

        [XmlElement("MainButtonPos")]
        public Vector2 XMLMainButtonPos { get => MainButtonPos; set => MainButtonPos = value; }
        [XmlIgnore]
        internal static Vector2 MainButtonPos = new Vector2(0f, 0f);
    }
}
