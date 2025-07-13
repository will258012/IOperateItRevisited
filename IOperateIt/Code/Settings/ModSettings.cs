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

        [XmlElement("EnginePower")]
        public float XMLEnginePower { get => EnginePower; set => EnginePower = value; }
        [XmlIgnore]
        internal static float EnginePower = 225f;

        [XmlElement("BrakingForce")]
        public float XMLBrakingForce { get => BrakingForce; set => BrakingForce = value; }
        [XmlIgnore]
        internal static float BrakingForce = 30f;

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

        [XmlElement("GripOvermatch")]
        public float XMLGripOvermatch { get => GripOvermatch; set => GripOvermatch = value; }
        [XmlIgnore]
        internal static float GripOvermatch = 0.3f;

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
        internal static Vector3 Offset = new Vector3(0f, 2f, 0f);

        [XmlElement("CamMouseRotateSensitivity")]
        public float XMLCamMouseRotateSensitivity { get => CamMouseRotateSensitivity; set => CamMouseRotateSensitivity = value; }
        [XmlIgnore]
        internal static float CamMouseRotateSensitivity = 1f;

        [XmlElement("CamKeyRotateSensitivity")]
        public float XMLCamKeyRotateSensitivity { get => CamKeyRotateSensitivity; set => CamKeyRotateSensitivity = value; }
        [XmlIgnore]
        internal static float CamKeyRotateSensitivity = 1f;

        [XmlElement("CamFieldOfView")]
        public float XMLCamFieldOfView { get => CamFieldOfView; set => CamFieldOfView = value; }
        [XmlIgnore]
        internal static float CamFieldOfView = 45f;

        [XmlElement("CamMaxPitchDeg")]
        public float XMLCamMaxPitchDeg { get => CamMaxPitchDeg; set => CamMaxPitchDeg = value; }
        [XmlIgnore]
        internal static float CamMaxPitchDeg = 70f;

        [XmlElement("CamSmoothing")]
        public float XMLCamSmoothing { get => CamSmoothing; set => CamSmoothing = value; }
        [XmlIgnore]
        internal static float CamSmoothing = 1f;

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

        [XmlElement("KeyMoveForward")]
        public KeyOnlyBinding XMLKeyMoveForward { get => KeyMoveForward; set => KeyMoveForward = value; }
        [XmlIgnore]
        internal static KeyOnlyBinding KeyMoveForward = new KeyOnlyBinding(KeyCode.W);

        [XmlElement("KeyMoveBackward")]
        public KeyOnlyBinding XMLKeyMoveBackward { get => KeyMoveBackward; set => KeyMoveBackward = value; }
        [XmlIgnore]
        internal static KeyOnlyBinding KeyMoveBackward = new KeyOnlyBinding(KeyCode.S);

        [XmlElement("KeyMoveLeft")]
        public KeyOnlyBinding XMLKeyMoveLeft { get => KeyMoveLeft; set => KeyMoveLeft = value; }
        [XmlIgnore]
        internal static KeyOnlyBinding KeyMoveLeft = new KeyOnlyBinding(KeyCode.A);

        [XmlElement("KeyMoveRight")]
        public KeyOnlyBinding XMLKeyMoveRight { get => KeyMoveRight; set => KeyMoveRight = value; }
        [XmlIgnore]
        internal static KeyOnlyBinding KeyMoveRight = new KeyOnlyBinding(KeyCode.D);

        [XmlElement("KeyCamCursorToggle")]
        public KeyOnlyBinding XMLKeyCamCursorToggle { get => KeyCamCursorToggle; set => KeyCamCursorToggle = value; }
        [XmlIgnore]
        internal static KeyOnlyBinding KeyCamCursorToggle = new KeyOnlyBinding(KeyCode.Tab);

        [XmlElement("KeyCamReset")]
        public KeyOnlyBinding XMLKeyCamReset { get => KeyCamReset; set => KeyCamReset = value; }
        [XmlIgnore]
        internal static KeyOnlyBinding KeyCamReset = new KeyOnlyBinding(KeyCode.Backslash);

        [XmlElement("KeyCamZoomIn")]
        public KeyOnlyBinding XMLKeyCamZoomIn { get => KeyCamZoomIn; set => KeyCamZoomIn = value; }
        [XmlIgnore]
        internal static KeyOnlyBinding KeyCamZoomIn = new KeyOnlyBinding(KeyCode.Equals);

        [XmlElement("KeyCamZoomOut")]
        public KeyOnlyBinding XMLKeyCamZoomOut { get => KeyCamZoomOut; set => KeyCamZoomOut = value; }
        [XmlIgnore]
        internal static KeyOnlyBinding KeyCamZoomOut = new KeyOnlyBinding(KeyCode.Minus);

        [XmlElement("KeyCamRotateLeft")]
        public KeyOnlyBinding XMLKeyCamRotateLeft { get => KeyCamRotateLeft; set => KeyCamRotateLeft = value; }
        [XmlIgnore]
        internal static KeyOnlyBinding KeyCamRotateLeft = new KeyOnlyBinding(KeyCode.LeftArrow);

        [XmlElement("KeyCamRotateRight")]
        public KeyOnlyBinding XMLKeyCamRotateRight { get => KeyCamRotateRight; set => KeyCamRotateRight = value; }
        [XmlIgnore]
        internal static KeyOnlyBinding KeyCamRotateRight = new KeyOnlyBinding(KeyCode.RightArrow);

        [XmlElement("KeyCamRotateUp")]
        public KeyOnlyBinding XMLKeyCamRotateUp { get => KeyCamRotateUp; set => KeyCamRotateUp = value; }
        [XmlIgnore]
        internal static KeyOnlyBinding KeyCamRotateUp = new KeyOnlyBinding(KeyCode.UpArrow);

        [XmlElement("KeyCamRotateDown")]
        public KeyOnlyBinding XMLKeyCamRotateDown { get => KeyCamRotateDown; set => KeyCamRotateDown = value; }
        [XmlIgnore]
        internal static KeyOnlyBinding KeyCamRotateDown = new KeyOnlyBinding(KeyCode.DownArrow);

        [XmlElement("MainButtonPos")]
        public Vector2 XMLMainButtonPos { get => MainButtonPos; set => MainButtonPos = value; }
        [XmlIgnore]
        internal static Vector2 MainButtonPos = new Vector2(0f, 0f);
    }
}
