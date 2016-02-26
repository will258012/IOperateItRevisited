using ICities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace UDriveIt.Utils
{
    class TerrainUtils : TerrainExtensionBase
    {
        private static ITerrain mTerrain;

        public static float getHeight(float x, float z)
        {
            return mTerrain == null ? 0f : mTerrain.SampleTerrainHeight(x, z);
        }

        public override void OnCreated(ITerrain terrain)
        {
            mTerrain = terrain;
        }
    }
}
