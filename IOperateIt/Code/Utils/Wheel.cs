using IOperateIt.Settings;
using UnityEngine;

namespace IOperateIt.Utils
{
    public class Wheel : MonoBehaviour
    {
        //public TrailRenderer skidTrail;
        public static int WheelCount { get; private set; } = 0;
        public static int FrontCount { get; private set; } = 0;
        public static int RearCount => WheelCount - FrontCount;

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
        public bool IsSimulated { get; private set; }
        public bool IsPowered { get; private set; }
        public bool IsSteerable { get; private set; }
        public bool IsInvertedSteer { get; private set; }
        public bool IsFront { get; private set; }

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
            w.IsSimulated = isSimulated;
            w.IsPowered = isPowered;
            w.IsSteerable = isSteerable;
            w.IsInvertedSteer = isInvertedSteer;
            w.IsFront = localpos.z > 0.0f;
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
                if (IsSimulated)
                {
                    WheelCount++;
                }

                if (IsFront)
                {
                    FrontCount++;
                }
                registered = true;
            }
        }

        private void DeRegister()
        {
            if (registered)
            {
                if (IsSimulated)
                {
                    WheelCount--;
                }

                if (IsFront)
                {
                    FrontCount--;
                }
                registered = false;
            }
        }
    }

}