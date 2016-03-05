using ICities;
using IOperateIt.Manager;
using IOperateIt.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace IOperateIt
{
    public class IOperateIt : IUserMod
    {
        private IOperateItOptions m_optionsManager = null;

        public string Name
        {
            get
            {
                return "IOperateIt";
            }
        }

        public string Description
        {
            get
            {
                return "Drive vehicles around your very own city!";
            }
        }

        public void OnSettingsUI(UIHelperBase helper)
        {
            if (m_optionsManager == null)
            {
                m_optionsManager = new GameObject("IOperateItOptions").AddComponent<IOperateItOptions>();
            }


            OptionsManager.Instance().LoadOptions();
            m_optionsManager.generateSettings(helper);
        }
    }
}
