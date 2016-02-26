﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UDriveIt.Utils;
using UnityEngine;

namespace UDriveIt.Manager
{
    class VehicleHolder
    {

        static VehicleHolder mInstance = null;
        private VehicleInfo mVehicleInfo;
        private Camera cameraController;
        private VehicleControler vehicleController;

        public static VehicleHolder getInstance()
        {
            if ( mInstance == null)
            {
                mInstance = new VehicleHolder();
                mInstance.getDefaultVehicle();
                mInstance.cameraController = Camera.main;
                mInstance.vehicleController = mInstance.cameraController.gameObject.AddComponent<VehicleControler>();
            }
            return mInstance;
        }

        private void getDefaultVehicle()
        {
            foreach (VehicleCollection vehicleCollection in UnityEngine.Object.FindObjectsOfType<VehicleCollection>())
            {
                if (vehicleCollection.name.Equals("Residential Low"))
                {
                    foreach (VehicleInfo info in vehicleCollection.m_prefabs)
                    {
                        if (info.name.Equals("Hatchback"))
                        {
                            mVehicleInfo = info;
                        }
                    }
                }
            }
        }

        public VehicleInfo getVehicleInfo()
        {
            return mVehicleInfo;
        }
            
        public void setVehicleInfo( VehicleInfo vehicleInfo)
        {
            mVehicleInfo = vehicleInfo;           
            LoggerUtils.LogToConsole(string.Format("Vehicle Info set:{0}", vehicleInfo.name));
        }

        public void setActive(Vector3 position)
        {
            vehicleController.setActive(position, mVehicleInfo);
        }

    }
}
