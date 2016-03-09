using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using IOperateIt.Utils;
using UnityEngine;

namespace IOperateIt.Manager
{
    class VehicleHolder
    {

        static VehicleHolder mInstance = null;
        private VehicleInfo mVehicleInfo;
        private Camera cameraController;
        private GameObject vehicle;
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

        public void setActive(Vector3 position, Vector3 eulerRotation)
        {
            if(vehicle!= null)
            {
                vehicle.GetComponent<VehicleControler>().setInactive();
            }
            vehicle = new GameObject("UDIVehicle");
            vehicle.AddComponent<VehicleControler>();
            vehicle.GetComponent<VehicleControler>().setActive(position, mVehicleInfo, eulerRotation);
        }

        public void setInactive()
        {
            if(vehicle != null)
            {
                vehicle.GetComponent<VehicleControler>().setInactive();
                vehicle = null;
            }
        }

    }
}
