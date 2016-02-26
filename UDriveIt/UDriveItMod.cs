using ICities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace UDriveIt
{
    public class UDriveItMod : IUserMod
    {
        public string Name
        {
            get
            {
                return "UDriveIt";
            }
        }

        public string Description
        {
            get
            {
                return "DrivingTest";
            }
        }
    }
}
