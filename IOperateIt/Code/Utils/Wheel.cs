using IOperateIt.Settings;
using UnityEngine;

namespace IOperateIt.Utils
{
    public class Wheel : MonoBehaviour
    {
        public static int WheelCount { get; private set; } = 0;
        public static int FrontCount { get; private set; } = 0;
        public static int RearCount => WheelCount - FrontCount;

        public static Rigidbody RigidBody { private get; set; }

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
        public float slip;
        public MapUtils.CollisionTypes collisionType;
        /// <summary>
        /// X = <see cref="binormalImpulse"/><br/>
        /// Y = <see cref="normalImpulse"/><br/>
        /// Z = <see cref="tangentImpulse"/>
        /// </summary>
        public Vector3 Impulse { 
            get => new(binormalImpulse, normalImpulse, tangentImpulse); 
            set { binormalImpulse = value.x; normalImpulse = value.y; tangentImpulse = value.z; } 
        }
        public bool IsOnGround { get; private set; }
        public bool IsSimulated { get; private set; }
        public bool IsPowered { get => isPowered && (torqueFract > 0.0f); private set => isPowered = value; }
        public bool IsSteerable { get; private set; }
        public bool IsInvertedSteer { get; private set; }
        public bool IsFront { get; private set; }

        private bool isPowered;
        private bool isRegistered;
        public static Wheel InstanceWheel(Transform parent, Vector3 localpos, float moment, float radius, bool isSimulated, bool isPowered, float torque, float brakeForce, bool isSteerable, bool isInvertedSteer)
        {
            GameObject go = new GameObject("Wheel");
            Wheel w = go.AddComponent<Wheel>();
            go.transform.SetParent(parent);
            go.transform.localPosition = localpos;
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
            w.Impulse = Vector3.zero;
            w.compression = 0.0f;
            w.frictionCoeff = ModSettings.GripCoeffK;
            w.slip = 0.0f;
            w.IsOnGround = false;
            w.IsSimulated = isSimulated;
            w.IsPowered = isPowered;
            w.IsSteerable = isSteerable;
            w.IsInvertedSteer = isInvertedSteer;
            w.IsFront = localpos.z > 0.0f;
            w.isRegistered = false;

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
        /// <summary>
        /// Calculate the road tbn and height at the wheel position.
        /// </summary>
        public void CalcRoadState(out ushort segmentId)
        {
            var pos = gameObject.transform.position;
            var xdisp = pos;
            var zdisp = pos;

            xdisp.x += 0.1f;
            zdisp.z += 0.1f;
            pos.y = MapUtils.CalculateHeight(pos, 0f, out collisionType, out segmentId);
            xdisp.y = MapUtils.CalculateHeight(xdisp, 0f, out var _, out _);
            zdisp.y = MapUtils.CalculateHeight(zdisp, 0f, out var _, out _);

            heightSample = pos;
            normal = Vector3.Normalize(Vector3.Cross(zdisp - pos, xdisp - pos));
            binormal = Vector3.Normalize(Vector3.Cross(gameObject.transform.TransformDirection(Vector3.forward), normal));
            tangent = Vector3.Normalize(Vector3.Cross(normal, binormal));
        }

        private void Register()
        {
            if (!isRegistered)
            {
                if (IsSimulated)
                {
                    WheelCount++;
                }

                if (IsFront)
                {
                    FrontCount++;
                }
                isRegistered = true;
            }
        }

        private void DeRegister()
        {
            if (isRegistered)
            {
                if (IsSimulated)
                {
                    WheelCount--;
                }

                if (IsFront)
                {
                    FrontCount--;
                }
                isRegistered = false;
            }
        }
        /// <summary>
        /// Adjust current wheel speed with previous tick sim and calculate new suspension position and state.
        /// </summary>
        public void CalcState(Vector3 up)
        {
            if (IsOnGround)
            {
                var prelimContactVel = RigidBody.GetPointVelocity(contactPoint);
                var flatImpulses = new Vector2(binormalImpulse, tangentImpulse);
                float radDelta = (Vector3.Dot(prelimContactVel, tangent) / radius) - radps;
                radps += Mathf.Sign(radDelta) * Mathf.Min(Mathf.Abs(radDelta), normalImpulse * radius * frictionCoeff / moment);
            }

            // Apply drag
            radps *= 1.0f - ((IsPowered ? DriveController.DRAG_WHEEL_POWERED : DriveController.DRAG_WHEEL) * Time.fixedDeltaTime);

            // calculate fist pass normal impulses. Update wheel suspension position.
            IsOnGround = false;
            normalImpulse = 0.0f;
            slip = 1.0f;
            frictionCoeff = ModSettings.GripCoeffK;
            float normDotUp = Vector3.Dot(normal, up);
            if (normDotUp > DriveController.VALID_INCLINE)
            {
                var originWheelBottom = RigidBody.transform.TransformPoint(origin + (Vector3.down * radius));
                float compression = Mathf.Max((Vector3.Dot(heightSample, normal) - Vector3.Dot(originWheelBottom, normal)) / normDotUp, 0f);
                float springVel = (compression - this.compression) / Time.fixedDeltaTime;
                float deltaVel = (-ModSettings.SpringDamp * Mathf.Exp(-ModSettings.SpringDamp * Time.fixedDeltaTime) * (compression + (springVel * Time.fixedDeltaTime))) + (springVel * Mathf.Exp(-ModSettings.SpringDamp * Time.fixedDeltaTime)) - springVel;

                gameObject.transform.localPosition = new Vector3(origin.x, origin.y + compression, origin.z);
                this.compression = compression;

                if (deltaVel < 0f)
                {
                    IsOnGround = true;
                    normalImpulse = RigidBody.mass * (-deltaVel) / (WheelCount * normDotUp);
                    contactPoint = gameObject.transform.TransformPoint(new Vector3(0.0f, -radius, 0.0f));
                    contactVelocity = RigidBody.GetPointVelocity(contactPoint);
                    var flatVel = contactVelocity - Vector3.Dot(contactVelocity, normal) * normal;
                    slip = Mathf.Clamp01(Vector3.Magnitude(flatVel - (radps * radius * tangent)) / Mathf.Clamp(flatVel.magnitude, 8f, 40f) / DriveController.GRIP_MAX_SLIP);
                    frictionCoeff = Mathf.Lerp(ModSettings.GripCoeffS, ModSettings.GripCoeffK, Mathf.Max((slip - DriveController.GRIP_OPTIM_SLIP) / (1.0f - DriveController.GRIP_OPTIM_SLIP), 0.0f));
                }
            }
            else
            {
                compression = 0f;
                gameObject.transform.localPosition = origin;
            }
        }
    }

}