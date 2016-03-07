using ColossalFramework.UI;
using ICities;
using IOperateIt.Manager;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEngine;

namespace IOperateIt
{
    enum KeyCodeSelectType
    {
        none,
        forward,
        back,
        left,
        right,
    }
    
    class IOperateItOptions : MonoBehaviour
    {
        public static bool misInGame = false;
        public static bool mhasOpenedPanel = false;

        private static UISlider mMaxVelocitySlider = null;
        private static UISlider mAccelerationForceSlider = null;
        private static UISlider mBreakingForceSlider = null;
        private static UISlider mcameraXAxisOffsetSliderr = null;
        private static UISlider mcameraYAxisOffsetSliderr = null;
        private static UISlider mcloseUpXAxisOffsetSliderr = null;
        private static UISlider mcloseUpXAxisOffsetSlider = null;
        private static UIButton forwardKeyBtn = null;
        private static UIButton backKeyBtn = null;
        private static UIButton leftKeyBtn = null;
        private static UIButton rightKeyBtn = null;
        private static UIButton saveBtn = null;
        private static UIHelperBase vehicleGroup = null;
        private static UIHelperBase fwdBindingGroup = null;
        private static UIHelperBase backBindingGroup = null;
        private static UIHelperBase leftBindingGroup = null;
        private static UIHelperBase rightBindingGroup = null;
        public static KeyCodeSelectType keyCodeSelctMode = KeyCodeSelectType.none;

        public void Update()
        {
            if (keyCodeSelctMode != KeyCodeSelectType.none)
            {
                KeyCode pressedKey = findKeyPressed();
                if (pressedKey != KeyCode.None)
                {
                    switch (keyCodeSelctMode)
                    {
                        case KeyCodeSelectType.forward:
                                OptionsManager.Instance().forwardKey = pressedKey;
                                forwardKeyBtn.text = OptionsManager.Instance().forwardKey.ToString();
                                keyCodeSelctMode = KeyCodeSelectType.none;
                            break;
                        case KeyCodeSelectType.back:
                                OptionsManager.Instance().backKey = pressedKey;
                                backKeyBtn.text = OptionsManager.Instance().backKey.ToString();
                                keyCodeSelctMode = KeyCodeSelectType.none;
                            break;
                        case KeyCodeSelectType.left:
                                OptionsManager.Instance().leftKey = pressedKey;
                                leftKeyBtn.text = OptionsManager.Instance().leftKey.ToString();
                                keyCodeSelctMode = KeyCodeSelectType.none;
                            break;
                        case KeyCodeSelectType.right:
                                OptionsManager.Instance().rightKey = pressedKey;
                                rightKeyBtn.text = OptionsManager.Instance().rightKey.ToString();
                                keyCodeSelctMode = KeyCodeSelectType.none;
                            break;
                    }
                }
            }
           
        }

        public void generateSettings(UIHelperBase helper)
        {
            OptionsManager optionsManager = OptionsManager.Instance();
            saveBtn = helper.AddButton("Save Settings", onSaveBtnPressed) as UIButton;
            vehicleGroup = helper.AddGroup("Vehicle settings");
            // This is temporarily disabled, seems like maximum velocity isn't being hit 
            //mMaxVelocitySlider = vehicleGroup.AddSlider("Maximum velocity", 50, 250, 5, optionsManager.mMaxVelocity, onMaxVelocityChanged) as UISlider;
            mAccelerationForceSlider = vehicleGroup.AddSlider("Acceleration force", 10, 200, 10, optionsManager.mAccelerationForce, onAccelerationChanged) as UISlider;
            mBreakingForceSlider = vehicleGroup.AddSlider("Turn breaking force", 10, 100, 5, optionsManager.mBreakingForce, onBreakingChanged) as UISlider;

            mcameraXAxisOffsetSliderr = vehicleGroup.AddSlider("Camera X axis offset", -70, 50, 5, optionsManager.mcameraXAxisOffset, onCameraXAxisChange) as UISlider;
            mcameraYAxisOffsetSliderr = vehicleGroup.AddSlider("Camera Y axis offset", 0, 90, 5, optionsManager.mcameraYAxisOffset, onCameraYAxisChange) as UISlider;
            mcloseUpXAxisOffsetSliderr = vehicleGroup.AddSlider("Close-up camera X axis offset", -7, 5, 0.25f, optionsManager.mcloseupXAxisOffset, onCloseupXAxisChange) as UISlider;
            mcloseUpXAxisOffsetSlider = vehicleGroup.AddSlider("Close-up camera Y axis offset", 0, 5, 0.25f, optionsManager.mcloseupYAxisOffset, onCloseupYAxisChange) as UISlider;

            fwdBindingGroup = helper.AddGroup("Forward Key");
            forwardKeyBtn = fwdBindingGroup.AddButton(optionsManager.forwardKey.ToString(), onForwardKeyBtnPressed) as UIButton;
            backBindingGroup = helper.AddGroup("Back Key");
            backKeyBtn = backBindingGroup.AddButton(optionsManager.backKey.ToString(), onBackKeyBtnPressed) as UIButton;
            leftBindingGroup = helper.AddGroup("Left Key");
            leftKeyBtn = leftBindingGroup.AddButton(optionsManager.leftKey.ToString(), onLeftKeyBtnPressed) as UIButton;
            rightBindingGroup = helper.AddGroup("Right Key");
            rightKeyBtn = rightBindingGroup.AddButton(optionsManager.rightKey.ToString(), onRightKeyBtnPressed) as UIButton;
        }

        private KeyCode findKeyPressed()
        {
            foreach( KeyCode code in Enum.GetValues(typeof(KeyCode)))
            {
                if (Input.GetKeyDown(code))
                {
                    return code;
                }
            }
            return KeyCode.None;
        }

        private void onSaveBtnPressed()
        {
            OptionsManager.Instance().SaveOptions();
        }

        private void onLeftKeyBtnPressed()
        {
            leftKeyBtn.text = "Press a Key";
            keyCodeSelctMode = KeyCodeSelectType.left;
        }

        private void onRightKeyBtnPressed()
        {
            rightKeyBtn.text = "Press a Key";
            keyCodeSelctMode = KeyCodeSelectType.right;
        }

        private void onBackKeyBtnPressed()
        {
            backKeyBtn.text = "Press a Key";
            keyCodeSelctMode = KeyCodeSelectType.back;
        }

        private void onForwardKeyBtnPressed()
        {
            forwardKeyBtn.text = "Press a Key";
            keyCodeSelctMode = KeyCodeSelectType.forward;
        }

        private void onCloseupYAxisChange(float val)
        {
            OptionsManager.Instance().mcloseupYAxisOffset = val;
        }

        private void onCloseupXAxisChange(float val)
        {
            OptionsManager.Instance().mcloseupXAxisOffset = val;
        }

        private void onCameraYAxisChange(float val)
        {
            OptionsManager.Instance().mcameraYAxisOffset = val;
        }

        private void onCameraXAxisChange(float val)
        {
            OptionsManager.Instance().mcameraXAxisOffset = val;
        }

        private void onBreakingChanged(float val)
        {
            OptionsManager.Instance().mBreakingForce = val;
        }

        private void onAccelerationChanged(float val)
        {
            OptionsManager.Instance().mAccelerationForce = val;
        }

        private void onMaxVelocityChanged(float val)
        {
            OptionsManager.Instance().mMaxVelocity = val;
            OptionsManager.Instance().mMaxVelocitySquared = (float)Math.Pow(val, 2);
        }
    }
}
