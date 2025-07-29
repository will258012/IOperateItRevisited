using IOperateIt.Settings;
using UnityEngine;

namespace IOperateIt.Utils
{
    public class Wheel : MonoBehaviour
    {
        //public TrailRenderer skidTrail;
        public static int wheelCount { get => wheels; private set => wheels = value; }
        public static int frontCount { get => fronts; private set => fronts = value; }
        public static int rearCount { get => wheels - fronts; }

        private static int wheels = 0;
        private static int fronts = 0;

        public Wheel xWheel;
        public Wheel zWheel;
        public Vector3 tangent;
        public Vector3 binormal;
        public Vector3 normal;
        public Vector3 heightSample;
        public Vector3 contactPoint;
        public Vector3 contactVelocity;
        public Vector3 origin;
        public float moment;
        public float radius;
        public float torqueFract;
        public float radps;
        public float brakeForce;
        public float normalImpulse;
        public float binormalImpulse;
        public float tangentImpulse;
        public float compression;
        public float frictionCoeff;
        public bool onGround;
        public bool isSimulated { get => simulated; private set => simulated = value; }
        public bool isPowered { get => powered; private set => powered = value; }
        public bool isSteerable { get => steerable; private set => steerable = value; }
        public bool isInvertedSteer { get => inverted; private set => inverted = value; }
        public bool isFront { get => front; private set => front = value; }

        private bool simulated;
        private bool powered;
        private bool steerable;
        private bool inverted;
        private bool front;
        private bool registered;
        public static Wheel InstanceWheel(Transform parent, Vector3 localpos, float moment, float radius, bool isSimulated = true, bool isPowered = true, float torque = 0.0f, float brakeForce = 0.0f, bool isSteerable = false, bool isInvertedSteer = false)
        {
            GameObject go = new GameObject("Wheel");
            Wheel w = go.AddComponent<Wheel>();
            go.transform.SetParent(parent);
            go.transform.localPosition = localpos;
            w.xWheel = null;
            w.zWheel = null;
            w.tangent = Vector3.zero;
            w.binormal = Vector3.zero;
            w.normal = Vector3.zero;
            w.heightSample = Vector3.zero;
            w.contactPoint = Vector3.zero;
            w.contactVelocity = Vector3.zero;
            w.origin = localpos;
            w.moment = moment;
            w.radius = radius;
            w.torqueFract = torque;
            w.radps = 0.0f;
            w.brakeForce = brakeForce;
            w.normalImpulse = 0.0f;
            w.binormalImpulse = 0.0f;
            w.tangentImpulse = 0.0f;
            w.compression = 0.0f;
            w.frictionCoeff = ModSettings.GripCoeffK;
            w.onGround = false;
            w.isSimulated = isSimulated;
            w.isPowered = isPowered;
            w.isSteerable = isSteerable;
            w.isInvertedSteer = isInvertedSteer;
            w.isFront = localpos.z > 0.0f;
            w.registered = false;

            w.Register();

            return w;
        }

        public void OnEnable()
        {
            Register();
        }

        public void OnDisable()
        {
            DeRegister();
        }

        public void CalcRoadTBN()
        {
            Vector3 tmp = Vector3.Normalize(Vector3.Cross(xWheel.heightSample - heightSample, zWheel.heightSample - heightSample));
            float dotUp = Vector3.Dot(tmp, Vector3.up);
            if (dotUp < -0.01f)
            {
                tmp = -tmp;
            }
            else if (dotUp < 0.01f)
            {
                tmp = Vector3.up;
            }

            normal = tmp;
            binormal = Vector3.Normalize(Vector3.Cross(gameObject.transform.TransformDirection(Vector3.forward), normal));
            tangent = Vector3.Normalize(Vector3.Cross(normal, binormal));
        }

        private void Register()
        {
            if (!registered)
            {
                if (isSimulated)
                {
                    wheelCount++;
                }

                if (isFront)
                {
                    fronts++;
                }
                registered = true;
            }
        }

        private void DeRegister()
        {
            if (registered)
            {
                if (isSimulated)
                {
                    wheelCount--;
                }

                if (isFront)
                {
                    fronts--;
                }
                registered = false;
            }
        }
    }

}