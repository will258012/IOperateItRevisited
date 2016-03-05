using IOperateIt.Utils;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Xml.Serialization;
using UnityEngine;

namespace IOperateIt.Manager
{
    [Serializable()]
    [XmlRoot(ElementName = "SavedOptions")]
    public class OptionsManager
    {
        [NonSerialized]
        private static readonly string FILE_NAME = "IOperateIt.xml";

        [NonSerialized]
        private static OptionsManager mInstance = null;

        public static OptionsManager Instance()
        {
            if (mInstance == null)
            {
                mInstance = new OptionsManager();
            }
            return mInstance;
        }

        [XmlElement(ElementName="MaxVelocity", IsNullable=false)]
        public float mMaxVelocity = 125f;
        public float mMaxVelocitySquared = 125f * 125f;
        [XmlElement(ElementName = "Acceleration", IsNullable = false)]
        public float mAccelerationForce = 60f;
        [XmlElement(ElementName = "Breaking", IsNullable = false)]
        public float mBreakingForce = 35f;
        [XmlElement(ElementName = "CameraX", IsNullable = false)]
        public float mcameraXAxisOffset = -35f;
        [XmlElement(ElementName = "CameraY", IsNullable = false)]
        public float mcameraYAxisOffset = 50f;
        [XmlElement(ElementName = "CloseUpX", IsNullable = false)]
        public float mcloseupXAxisOffset = -3.75f;
        [XmlElement(ElementName = "CloseUpY", IsNullable = false)]
        public float mcloseupYAxisOffset = 1.5f;

        [XmlElement(ElementName = "FwdKey", IsNullable = false)]
        public KeyCode forwardKey = KeyCode.UpArrow;
        [XmlElement(ElementName = "DownKey", IsNullable = false)]
        public KeyCode backKey = KeyCode.DownArrow;
        [XmlElement(ElementName = "LeftKey", IsNullable = false)]
        public KeyCode leftKey = KeyCode.LeftArrow;
        [XmlElement(ElementName = "RightKey", IsNullable = false)]
        public KeyCode rightKey = KeyCode.RightArrow;

        public void SaveOptions()
        {
            XmlSerializer serializer = new XmlSerializer(typeof(OptionsManager));
            StreamWriter writer = new StreamWriter(FILE_NAME);
            serializer.Serialize(writer, OptionsManager.mInstance);
            writer.Close();
            LoggerUtils.Log("Settings saved");
        }

        public void LoadOptions()
        {
            LoggerUtils.Log("Starting settings load");
            if (File.Exists(FILE_NAME))
            {
                XmlSerializer xmlSerialiser = new XmlSerializer(typeof(OptionsManager));
                StreamReader reader = new StreamReader(FILE_NAME);
                OptionsManager optionsManager = xmlSerialiser.Deserialize(reader) as OptionsManager;
                reader.Close();
                LoggerUtils.Log(optionsManager.mMaxVelocity.ToString());
                if( optionsManager != null)
                {
                    mInstance = optionsManager;
                }
                LoggerUtils.Log("Settings read");
            }
        }
    }
}
