using ICities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace IOperateIt
{
    public class IOperateIt : IUserMod
    {
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
    }
}
