using UnityEngine;

namespace IOperateIt.Utils
{
    public class MapUtils : ToolBase
    {
        public const string LAYER_UNDERGROUND_NAME = "MetroTunnels";
        public const string LAYER_BUILDINGS_NAME = "Buildings";
        public const string LAYER_VEHICLES_NAME = "Vehicles";
        public const string LAYER_ROAD_NAME = "Road";

        public static readonly int LAYER_UNDERGROUND = LayerMask.NameToLayer(LAYER_UNDERGROUND_NAME); // underground render layer.
        public static readonly int LAYER_BUILDINGS = LayerMask.NameToLayer(LAYER_BUILDINGS_NAME); // building render layer.
        public static readonly int LAYER_VEHICLES = LayerMask.NameToLayer(LAYER_VEHICLES_NAME); // vehicle render layer.
        public static readonly int LAYER_ROAD = LayerMask.NameToLayer(LAYER_ROAD_NAME); // road render layer.
        public static bool RayCast(RaycastInput rayCastInput, out RaycastOutput result)
        {
            result = default;
            // Perform a single raycast if no offset is provided
            if (ToolBase.RayCast(rayCastInput, out var result2))
            {
                result = result2;
                return true;
            }
            else
            {
                return false;
            }
        }

        public static RaycastInput GetRaycastInput(Vector3 position)
            => GetRaycastInput(position, -100f, 100f);

        public static RaycastInput GetRaycastInput(Vector3 position, float min, float max)
            => GetRaycastInput(position + Vector3.up * max, Vector3.down, max - min);

        public static RaycastInput GetRaycastInput(Vector3 position, Vector3 dir, float dist, bool ignoreTerrain = true)
        {
            var input = new RaycastInput(new Ray(position, dir), dist);
            input.m_ignoreTerrain = ignoreTerrain;
            return input;
        }
    }
}
