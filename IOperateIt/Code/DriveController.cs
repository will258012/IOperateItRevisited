extern alias FPC;

using AlgernonCommons;
using ColossalFramework;
using IOperateIt.Managers;
using IOperateIt.Settings;
using IOperateIt.UI;
using IOperateIt.Utils;
using System.Collections.Generic;
using UnityEngine;
using FPCModSettings = FPC.FPSCamera.Settings.ModSettings;
namespace IOperateIt
{
    public class DriveController : MonoBehaviour
    {
        public const float FLOAT_ERROR = 0.01f;
        private const float THROTTLE_RESP = 2.0f;
        private const float STEER_RESP = 1.5f;
        private const float STEER_REST = 1.5f;
        private const float GEAR_RESP = 0.2f;
        private const float BRAKE_SPEED_FACTOR = 0.3f;
        private const float PARK_SPEED = 0.25f;
        private const float DEPEN_VELOCITY = 2.0f;
        private const float STEER_MAX = 45f;
        private const float STEER_DECAY = 0.0075f;
        private const float ROAD_WALL_HEIGHT = 0.75f;
        private const float NEIGHBOR_WHEEL_DIST = 0.2f;
        private const float SPRING_MAX_COMPRESS = 0.2f;
        private const float DRAG_FACTOR = 0.25f;
        private const float DRAG_DRIVETRAIN = 0.15f;
        private const float DRAG_WHEEL_POWERED = 0.25f;
        private const float DRAG_WHEEL = 0.15f;
        private const float MOMENT_WHEEL = 1.5f;
        private const float VALID_INCLINE = 0.5f;
        private const float GRIP_MAX_SLIP = 0.5f;
        private const float GRIP_OPTIM_SLIP = 0.2f;
        private const float ENGINE_PEAK_POWER_RPS = 900.0f;
        private const float ENGINE_IDLE_RPS = 90.0f;
        private const float ENGINE_GEAR_RATIO = 7.0f;
        private const float ACCEL_G = 10f;
        const float MS_TO_KMPH = 3.6f;
        const float UNIT_TO_M = 25.0f / 54.0f;
        const float M_TO_UNIT = 54.0f / 25.0f;
        const float UNIT_TO_MPH = UNIT_TO_M * 2.23694f;
        const float KN_TO_N = 1000f;
        const float KW_TO_W = 1000f;


        public static DriveController Instance { get; private set; }
        public float Brake { get; private set; }
        public Vector3 PrevVelocity { get; private set; }
        public Direction CurrentDirection { get; private set; }
        public Direction Gear { get; private set; }

        private ushort lastValidSegmentId;

        private Rigidbody vehicleRigidBody;
        private BoxCollider vehicleCollider;
        private Color vehicleColor;
        private bool setColor;
        private VehicleInfo vehicleInfo;

        private List<Wheel> wheelObjects = [];
        private readonly CollidersManager collidersManager = new();
        private readonly Managers.EffectManager effectManager = new();
        private Vector3 prevPosition;
        private Vector3 tangent;
        private Vector3 binormal;
        private Vector3 normal;
        private bool physicsFallback = false;
        private float terrainHeight;
        private float distanceTravelled;
        private float steer;
        private float throttle;
        private float rideHeight;
        private float roofHeight;   
        private float compression;
        private float prevGearChange;
        private float normalImpulse;
        private float radps;
        private float torque;

        public enum Direction
        {
            Neutral = 0,
            Forward = 1,
            Reverse = -1,
        }

        private UndergroundRenderer undergroundRenderer = new();


        private void Awake()
        {
            Instance = this;

            gameObject.AddComponent<MeshFilter>();
            gameObject.AddComponent<MeshRenderer>();
            gameObject.GetComponent<MeshRenderer>().enabled = true;

            vehicleRigidBody = gameObject.AddComponent<Rigidbody>();
            vehicleRigidBody.isKinematic = false;
            vehicleRigidBody.useGravity = false;
            vehicleRigidBody.freezeRotation = false;
            vehicleRigidBody.interpolation = RigidbodyInterpolation.Interpolate;

            PhysicMaterial material = new PhysicMaterial();
            material.bounciness = 0.05f;
            material.staticFriction = 0.1f;

            vehicleCollider = gameObject.AddComponent<BoxCollider>();
            vehicleCollider.material = material;

            //m_suspensionCollider = gameObject.AddComponent<SphereCollider>();

            StartCoroutine(collidersManager.InitializeColliders());
            gameObject.SetActive(false);
            enabled = false;
        }
        private void Update()
        {
            HandleInputOnUpdate();
            UpdateMaterialBlock();
            effectManager.PlayEffects();
#if DEBUG
            DebugHelper.DrawDebugBox(vehicleCollider.size, vehicleCollider.transform.TransformPoint(vehicleCollider.center), vehicleCollider.transform.rotation, Color.magenta);
#endif
        }

        private void UpdateMaterialBlock()
        {
            var materialBlock = Singleton<VehicleManager>.instance.m_materialBlock;
            materialBlock.Clear();

            effectManager.UpdateLights(materialBlock);
            if (setColor)
            {
                materialBlock.SetColor(Singleton<VehicleManager>.instance.ID_Color, vehicleColor);
            }
            Vector4 tyrePosition = default;
            tyrePosition.x = steer * STEER_MAX / 180f * Mathf.PI;
            tyrePosition.y = distanceTravelled;
            tyrePosition.z = 0f;
            tyrePosition.w = 0f;
            materialBlock.SetVector(Singleton<VehicleManager>.instance.ID_TyrePosition, tyrePosition);

            if (physicsFallback)
            {
                materialBlock.SetMatrix(Singleton<VehicleManager>.instance.ID_TyreMatrix, Matrix4x4.TRS(new Vector3(0f, Mathf.Clamp(terrainHeight - vehicleRigidBody.transform.position.y, ModSettings.SpringOffset, 0f), 0f), Quaternion.LookRotation(tangent, normal), Vector3.one));
            }
            else
            {
                materialBlock.SetMatrix(Singleton<VehicleManager>.instance.ID_TyreMatrix, Matrix4x4.identity);
            }

            gameObject.GetComponent<MeshRenderer>().SetPropertyBlock(materialBlock);
        }

        private void FixedUpdate()
        {
            var vehiclePos = vehicleRigidBody.transform.position;
            var vehicleVel = vehicleRigidBody.velocity;
            var vehicleAngularVel = vehicleRigidBody.angularVelocity;
            float speed = Vector3.Dot(Vector3.forward, vehicleRigidBody.transform.InverseTransformDirection(vehicleVel));
            CurrentDirection = Mathf.Abs(speed) < PARK_SPEED ? Direction.Neutral : (speed > 0f ? Direction.Forward : Direction.Reverse);

            HandleInputOnFixedUpdate();

            if (physicsFallback)
            {
                FallbackPhysics(ref vehiclePos, ref vehicleVel, ref vehicleAngularVel);
            }
            else
            {
                WheelPhysics(ref vehiclePos, ref vehicleVel, ref vehicleAngularVel);
            }

            LimitVelocity();

            collidersManager.UpdateColliders(vehicleRigidBody.transform);

            PrevVelocity = vehicleVel;
            prevPosition = vehiclePos;
        }

        private void LateUpdate()
        {
        }
        private void OnDestroy()
        {
            collidersManager.DestroyColliders();
        }

        public bool OnEsc()
        {
            if (enabled)
            {
                StopDriving();
                return true;
            }
            return false;
        }

        private void OnCollisionEnter(Collision collision)
        {
            LimitVelocity();
            vehicleRigidBody.velocity *= 0.3f;
            vehicleRigidBody.angularVelocity = Vector3.zero;

            var container = collision.collider.gameObject.GetComponent<ColliderContainer>();
            if (container.Type == ColliderContainer.ContainerTypes.Vehicle)
            {
                ref var otherVehicle = ref Singleton<VehicleManager>.instance.m_vehicles.m_buffer[container.ID];

                ref var otherVelocity = ref (otherVehicle.m_lastFrame <= 1 ?
                    ref (otherVehicle.m_lastFrame == 0 ? ref otherVehicle.m_frame0.m_velocity : ref otherVehicle.m_frame1.m_velocity) :
                    ref (otherVehicle.m_lastFrame == 2 ? ref otherVehicle.m_frame2.m_velocity : ref otherVehicle.m_frame3.m_velocity));

                float collisionOrientation = Vector3.Dot(Vector3.Normalize(vehicleRigidBody.position - collision.collider.transform.position), Vector3.Normalize(otherVelocity));

                otherVelocity = otherVelocity * 0.8f * (0.5f + ((-collisionOrientation + 1f) * 0.25f));
            }
        }

        private void OnCollisionStay(Collision collision)
        {
            LimitVelocity();
        }

        private void OnCollisionExit(Collision collision)
        {
            LimitVelocity();
        }
#if DEBUG
        private void OnGUI()
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"g: {Gear}");
            sb.AppendLine($"t: {throttle:F2}");
            sb.AppendLine($"b: {Brake:F2}");
            sb.AppendLine($"s: {vehicleRigidBody.velocity.magnitude * UNIT_TO_M * MS_TO_KMPH:F1} km/h");
            sb.AppendLine($"rps: {radps:F1}");
            sb.AppendLine($"wct: front={Wheel.FrontCount} rear={Wheel.RearCount}");
            for (int i = 0; i < wheelObjects.Count; i++)
            {
                var w = wheelObjects[i];
                sb.AppendLine($"w{i}: origin={w.origin} slip={w.slip:F2} radps={w.radps:F1}");
            }
            GUI.Label(new Rect(100f, 100f, 700f, 700f), sb.ToString());
        }
#endif
        private void FallbackPhysics(ref Vector3 vehiclePos, ref Vector3 vehicleVel, ref Vector3 vehicleAngularVel)
        {
            vehicleRigidBody.AddForce(Vector3.down * ACCEL_G, ForceMode.Acceleration);

            float height = MapUtils.CalculateHeight(vehiclePos, roofHeight, out var type, out var segmentId);
            if (segmentId != default) lastValidSegmentId = segmentId;
            bool onGround = vehiclePos.y + ModSettings.SpringOffset < terrainHeight;
            effectManager.IsDusty = type == MapUtils.CollisionTypes.Ground && onGround;


            CalculateSlope(ref vehiclePos, ref vehicleVel, ref vehicleAngularVel, height, onGround);
            terrainHeight = height;


            if (vehiclePos.y + ROAD_WALL_HEIGHT < terrainHeight)
            {
                vehiclePos = prevPosition;
                vehicleVel = Vector3.zero;
                vehicleRigidBody.transform.position = vehiclePos;
                vehicleRigidBody.velocity = vehicleVel;
            }
            else if (onGround)
            {
                if (vehiclePos.y + SPRING_MAX_COMPRESS < terrainHeight)
                {
                    vehiclePos = new Vector3(vehiclePos.x, terrainHeight - SPRING_MAX_COMPRESS, vehiclePos.z);
                    vehicleRigidBody.transform.position = vehiclePos;
                }

                float compression = Mathf.Max(terrainHeight - (vehiclePos.y + ModSettings.SpringOffset), 0f);
                float springVel = (compression - this.compression) / Time.fixedDeltaTime;
                float deltaVel = (-ModSettings.SpringDamp * Mathf.Exp(-ModSettings.SpringDamp * Time.fixedDeltaTime) * (compression + (springVel * Time.fixedDeltaTime))) + (springVel * Mathf.Exp(-ModSettings.SpringDamp * Time.fixedDeltaTime)) - springVel;

                if (deltaVel < 0f)
                {
                    normalImpulse = -deltaVel * vehicleRigidBody.mass;
                }
                else
                {
                    normalImpulse = 0f;
                }
            }

            if (onGround)
            {
                var relativeVel = vehicleRigidBody.transform.InverseTransformDirection(vehicleVel);

                var longImpulse = Vector3.forward * (int)Gear * throttle * (ModSettings.EnginePower * KW_TO_W * (1.0f - DRAG_DRIVETRAIN) / (vehicleVel.magnitude + 1.0f)) * Time.fixedDeltaTime;

                float speedBrakeMultiplier = 1f + (vehicleVel.magnitude * BRAKE_SPEED_FACTOR);

                if (Gear == 0)
                {
                    longImpulse -= Vector3.forward * Mathf.Sign(relativeVel.z) * Mathf.Min(
                        Brake * speedBrakeMultiplier * (ModSettings.BrakingForce * KN_TO_N) * Time.fixedDeltaTime,
                        Mathf.Abs(relativeVel.z) * vehicleRigidBody.mass);
                }
                else
                {
                    longImpulse -= Vector3.forward * (int)Gear * Brake * speedBrakeMultiplier * (ModSettings.BrakingForce * KN_TO_N) * Time.fixedDeltaTime;
                }


                relativeVel.z = 0f;
                relativeVel.y = 0f;

                var netImpulse = longImpulse;
                netImpulse -= relativeVel * (1.0f - FLOAT_ERROR) * vehicleRigidBody.mass;
                netImpulse = Mathf.Min(netImpulse.magnitude, normalImpulse * ModSettings.GripCoeffS) * Vector3.Normalize(netImpulse);
                netImpulse += Vector3.up * normalImpulse;
                netImpulse = vehicleRigidBody.transform.TransformDirection(netImpulse);
                vehicleRigidBody.AddForceAtPosition(netImpulse, new Vector3(vehiclePos.x, terrainHeight, vehiclePos.z), ForceMode.Impulse);

                float speedsteer = Mathf.Min(Mathf.Max(vehicleVel.magnitude * 80f / vehicleCollider.size.z, 0f), 60f);
                speedsteer = Mathf.Sign(steer) * Mathf.Min(Mathf.Abs(60f * steer), speedsteer);

                var angularTarget = ((1f - FLOAT_ERROR) * (Vector3.up * (int)CurrentDirection * speedsteer * Time.fixedDeltaTime)) - vehicleRigidBody.transform.InverseTransformDirection(vehicleAngularVel);

                vehicleRigidBody.AddRelativeTorque(angularTarget, ForceMode.VelocityChange);
            }

            compression = Mathf.Max(terrainHeight - (vehiclePos.y + ModSettings.SpringOffset), 0.0f);

            distanceTravelled += (int)CurrentDirection * Vector3.Magnitude(vehiclePos - prevPosition);
        }

        private void WheelPhysics(ref Vector3 vehiclePos, ref Vector3 vehicleVel, ref Vector3 vehicleAngularVel)
        {
            var upVec = vehicleRigidBody.transform.TransformDirection(Vector3.up);

            vehicleRigidBody.AddForce((Vector3.down * (ACCEL_G * vehicleRigidBody.mass)) - (upVec * ModSettings.DownForce * Mathf.Abs(Vector3.Dot(vehicleVel, vehicleRigidBody.transform.TransformDirection(Vector3.forward)))), ForceMode.Force);

            foreach (var w in wheelObjects) // first calculate the heights at each wheel to prep for road normal calcs
            {
                var wheelPos = w.gameObject.transform.position;
                w.heightSample = wheelPos;
                w.heightSample.y = MapUtils.CalculateHeight(wheelPos, roofHeight, out var type, out var segmentId);
                if (segmentId != default) lastValidSegmentId = segmentId;
                effectManager.IsDusty = type == MapUtils.CollisionTypes.Ground && w.onGround;

                if (wheelPos.y + ROAD_WALL_HEIGHT < w.heightSample.y)
                {
                    vehiclePos = prevPosition;
                    vehicleVel = -vehicleVel * 0.1f;
                    vehicleAngularVel = Vector3.zero;
                    vehicleRigidBody.angularVelocity = vehicleAngularVel;
                    vehicleRigidBody.velocity = vehicleVel;
                    vehicleRigidBody.transform.position = vehiclePos;
                    return;
                }
            }

            foreach (var w in wheelObjects) // calculate the road normals. apply angular friction from previous tick. 
            {
                if (w.IsSteerable)
                {
                    w.gameObject.transform.localRotation = Quaternion.Euler(0, (w.IsInvertedSteer ? -1f : 1f) * STEER_MAX * steer, 0);
                }
                else
                {
                    w.gameObject.transform.localRotation = Quaternion.Euler(0, 0, 0);
                }

                w.CalcRoadTBN();

                if (w.onGround)
                {
                    var prelimContactVel = vehicleRigidBody.GetPointVelocity(w.contactPoint);
                    Vector2 flatImpulses = new Vector2(w.binormalImpulse, w.tangentImpulse);
                    float radDelta = (Vector3.Dot(prelimContactVel, w.tangent) / w.radius) - w.radps;
                    w.radps += Mathf.Sign(radDelta) * Mathf.Min(Mathf.Abs(radDelta), w.normalImpulse * w.radius * w.frictionCoeff / w.moment);
                }

                w.radps *= 1.0f - ((w.IsPowered ? DRAG_WHEEL_POWERED : DRAG_WHEEL) * Time.fixedDeltaTime);
            }

            foreach (var w in wheelObjects) // calculate normal impulses.Update wheel suspension position.
            {
                w.onGround = false;
                w.normalImpulse = 0.0f;
                w.slip = 1.0f;
                w.frictionCoeff = ModSettings.GripCoeffK;
                float normDotUp = Vector3.Dot(w.normal, upVec);
                if (normDotUp > VALID_INCLINE)
                {
                    var originWheelBottom = vehicleRigidBody.transform.TransformPoint(w.origin + (Vector3.down * w.radius));
                    float compression = Mathf.Max((Vector3.Dot(w.heightSample, w.normal) - Vector3.Dot(originWheelBottom, w.normal)) / normDotUp, 0f);
                    float springVel = (compression - w.compression) / Time.fixedDeltaTime;
                    float deltaVel = (-ModSettings.SpringDamp * Mathf.Exp(-ModSettings.SpringDamp * Time.fixedDeltaTime) * (compression + (springVel * Time.fixedDeltaTime))) + (springVel * Mathf.Exp(-ModSettings.SpringDamp * Time.fixedDeltaTime)) - springVel;

                    w.gameObject.transform.localPosition = new Vector3(w.origin.x, w.origin.y + compression, w.origin.z);
                    w.compression = compression;

                    if (deltaVel < 0f)
                    {
                        w.onGround = true;
                        w.normalImpulse = vehicleRigidBody.mass * (-deltaVel) / (Wheel.WheelCount * normDotUp);
                        w.contactPoint = w.gameObject.transform.TransformPoint(new Vector3(0.0f, -w.radius, 0.0f));
                        w.contactVelocity = vehicleRigidBody.GetPointVelocity(w.contactPoint);
                        w.slip = Mathf.Clamp(Vector3.Magnitude(w.contactVelocity - (w.radps * w.radius * w.tangent)) / Mathf.Max(w.contactVelocity.magnitude, 1.0f) / GRIP_MAX_SLIP, 0.0f, 1.0f);
                        w.frictionCoeff = Mathf.Lerp(ModSettings.GripCoeffS, ModSettings.GripCoeffK, Mathf.Max((w.slip - GRIP_OPTIM_SLIP) / (1.0f - GRIP_OPTIM_SLIP), 0.0f));
                    }
                }
                else
                {
                    w.compression = 0f;
                    w.gameObject.transform.localPosition = w.origin;
                }

            }

            // calculate new engine angular velocity
            float engineRps = 0f;
            foreach (var w in wheelObjects)
            {
                distanceTravelled += w.radps * w.torqueFract * w.radius * Time.fixedDeltaTime;
                engineRps += w.radps * w.torqueFract;
            }
            radps = engineRps * ENGINE_GEAR_RATIO;
            torque = GetTorque(engineRps) * ENGINE_GEAR_RATIO;

            float avgFrontRps = 0.0f;
            float avgRearRps = 0.0f;
            foreach (var w in wheelObjects) // calcuate first pass wheel angular velocity
            {
                float wheelTorque = (int)Gear * throttle * w.torqueFract * torque;
                w.radps += wheelTorque * Time.fixedDeltaTime / w.moment;

                if (w.IsFront)
                {
                    avgFrontRps += w.radps;
                }
                else
                {
                    avgRearRps += w.radps;
                }
            }

            avgFrontRps /= Wheel.FrontCount;
            avgRearRps /= Wheel.RearCount;

            foreach (var w in wheelObjects) // calculate the lateral and longitudinal forces. Apply all forces.
            {
                if (w.onGround)
                {
                    var netImpulse = Vector3.zero;

                    float normalContribution = 0f;
                    foreach (var wAlt in wheelObjects)
                    {
                        normalContribution += Vector3.Dot(w.binormal, wAlt.binormal) * wAlt.normalImpulse;
                    }
                    normalContribution = w.normalImpulse / normalContribution;

                    var flatImpulses = Vector2.zero;

                    float longSpeed = Vector3.Dot(w.contactVelocity, w.tangent);
                    float lateralSpeed = Vector3.Dot(w.contactVelocity, w.binormal);

                    // limited slip diff
                    float radDelta = w.IsFront ? avgFrontRps : avgRearRps;
                    radDelta -= w.radps;
                    w.radps += Mathf.Sign(radDelta) * Mathf.Min(Mathf.Abs(radDelta), radDelta * radDelta * 10.0f);

                    // braking ABS
                    float speedBrakeMultiplier = 1f + (vehicleVel.magnitude * BRAKE_SPEED_FACTOR);
                    float totalBrake = (w.slip < GRIP_OPTIM_SLIP || !ModSettings.BrakingABS) && w.onGround
                        ? Brake * speedBrakeMultiplier
                        : 0.0f;

                    if (Brake > 0.5f && vehicleVel.magnitude > 20f && w.onGround)
                    {
                        float brakeCut = Brake * Time.fixedDeltaTime * 3f;
                        vehicleVel = Vector3.MoveTowards(vehicleVel, Vector3.zero, brakeCut);
                        vehicleRigidBody.velocity = vehicleVel;
                    }

                    // basic traction control
                    float actionableDelta = Mathf.Max((Mathf.Sign(w.radps) * (w.radps - (longSpeed / w.radius))) - (w.normalImpulse * w.frictionCoeff * Time.fixedDeltaTime * w.radius / w.moment), 0.0f);
                    totalBrake += actionableDelta * w.moment / (w.radius * Time.fixedDeltaTime * w.brakeForce);

                    totalBrake = Mathf.Clamp(totalBrake, 0.0f, 1.0f);
                    float wheelTorque = -Mathf.Sign(w.radps) * Mathf.Min(totalBrake * w.brakeForce * w.radius, Mathf.Abs(w.radps) * w.moment / Time.fixedDeltaTime);
                    w.radps += wheelTorque * Time.fixedDeltaTime / w.moment;

                    float longComponent = normalContribution * vehicleRigidBody.mass * ((w.radps * w.radius) - longSpeed);
                    if (Mathf.Abs(longSpeed) < 1.0f || Mathf.Abs(w.radps) < 0.1f) // exaggerated torque at low speeds for better stop and start
                    {
                        longComponent = normalContribution * vehicleRigidBody.mass * (w.radps * w.radius - longSpeed);
                    }

                    flatImpulses.y = longComponent;

                    float lateralComponent = -normalContribution * vehicleRigidBody.mass * lateralSpeed;

                    flatImpulses.x = lateralComponent;
#if DEBUG
                    if (w.slip > GRIP_OPTIM_SLIP)
                    {
                        DebugHelper.DrawDebugMarker(2f, w.contactPoint, Color.yellow);
                    }
#endif
                    float frictionScale = Mathf.Min(w.normalImpulse * w.frictionCoeff, flatImpulses.magnitude) / Mathf.Max(flatImpulses.magnitude, FLOAT_ERROR);

                    w.binormalImpulse = lateralComponent * frictionScale;
                    w.tangentImpulse = longComponent * frictionScale;

                    netImpulse += w.normalImpulse * w.normal;
                    netImpulse += w.binormalImpulse * w.binormal;
                    netImpulse += w.tangentImpulse * w.tangent;

                    vehicleRigidBody.AddForceAtPosition(netImpulse, w.contactPoint, ForceMode.Impulse);
                }
                else
                {
                    float tireOffset = Mathf.Sqrt(Mathf.Abs(Vector3.Dot(upVec, w.normal)));
                    if (w.transform.position.y - (w.radius * tireOffset) < w.heightSample.y)
                    {
                        var pos = vehicleRigidBody.transform.position;
                        pos.y += w.heightSample.y - w.transform.position.y + (w.radius * tireOffset);
                        vehicleRigidBody.transform.position = pos;

                        var normalVel = Vector3.Dot(w.normal, vehicleVel) * w.normal;

                        vehicleRigidBody.AddForce((-(1f - FLOAT_ERROR) * normalVel) - ((vehicleVel - normalVel) * 0.5f * Time.fixedDeltaTime), ForceMode.VelocityChange);

                        vehicleRigidBody.AddTorque(-vehicleAngularVel * 0.5f * Time.fixedDeltaTime, ForceMode.VelocityChange);
                        if (vehicleAngularVel.magnitude < 1f)
                        {
                            vehicleRigidBody.AddTorque(Vector3.Normalize(Vector3.Cross(upVec, w.normal)) * 0.25f, ForceMode.VelocityChange);
                        }
                    }

                    float wheelTorque;
                    wheelTorque = (int)Gear * throttle * w.torqueFract * torque;
                    w.radps += wheelTorque * Time.fixedDeltaTime / w.moment;
                    wheelTorque = -Mathf.Sign(w.radps) * Mathf.Min(Brake * w.brakeForce * w.radius, Mathf.Abs(w.radps) * w.moment / Time.fixedDeltaTime);
                    w.radps += wheelTorque * Time.fixedDeltaTime / w.moment;
                }
            }
        }

        private void LimitVelocity()
        {
            if (vehicleRigidBody.velocity.magnitude > ModSettings.MaxVelocity / MS_TO_KMPH)
            {
                vehicleRigidBody.AddForce((vehicleRigidBody.velocity.normalized * ModSettings.MaxVelocity / MS_TO_KMPH) - vehicleRigidBody.velocity, ForceMode.VelocityChange);
            }
        }
        private void Unstuck()
        {
            vehicleRigidBody.velocity = Vector3.zero;
            vehicleRigidBody.angularVelocity = Vector3.zero;

            var netSegment = NetManager.instance.m_segments.m_buffer[lastValidSegmentId];

            if (netSegment.m_flags.IsFlagSet(NetSegment.Flags.Created))
            {
                netSegment.GetClosestPositionAndDirection(vehicleRigidBody.position, out var newPos, out var dir);
                if (Vector3.Distance(newPos, vehicleRigidBody.position) < 50f)
                    vehicleRigidBody.transform.SetPositionAndRotation(newPos, Quaternion.LookRotation(dir));
                else
                    Fallback();
            }
            else
                Fallback();

            vehicleRigidBody.Sleep();
            DriveCamController.Instance.ResetCam();

            void Fallback()
            {
                var fallBackPos = vehicleRigidBody.position;
                fallBackPos.y = MapUtils.CalculateHeight(vehicleRigidBody.position, roofHeight, out _, out _) + 2f;
                vehicleRigidBody.position = fallBackPos;
                vehicleRigidBody.rotation = Quaternion.Euler(0f, vehicleRigidBody.rotation.eulerAngles.y, 0f);
            }
        }

        public void UpdateColor(Color color, bool enable)
        {
            vehicleColor = color;
            setColor = enable;
        }

        public void UpdateVehicleInfo(VehicleInfo info)
        {
            vehicleInfo = info;
        }

        public bool IsVehicleInfoSet()
        {
            return vehicleInfo != null;
        }

        public void StartDriving(Vector3 position, Quaternion rotation) => StartDriving(position, rotation, vehicleInfo, vehicleColor, setColor);
        public void StartDriving(Vector3 position, Quaternion rotation, VehicleInfo vehicleInfo, Color vehicleColor, bool setColor)
        {
            try
            {
                if (enabled) StopDriving();
                enabled = true;
                SpawnVehicle(position + new Vector3(0f, -ModSettings.SpringOffset, 0f), rotation, vehicleInfo, vehicleColor, setColor);
                if (ModSettings.UndergroundRendering)
                    undergroundRenderer.OverridePrefabs();
                DriveCamController.Instance.EnableCam(vehicleRigidBody);
                DriveButtons.Instance.SetDisable();
            }
            catch (System.Exception e)
            {
                Logging.LogException(e, "Failed to start driving");
                StopDriving();
            }
        }
        public void StopDriving()
        {
            try
            {
                StartCoroutine(collidersManager.DisableColliders());
                DriveCamController.Instance.DisableCam();
                DriveButtons.Instance.SetEnable();
                if (ModSettings.UndergroundRendering)
                    undergroundRenderer.RestorePrefabs();
                DestroyVehicle();
                enabled = false;
            }
            catch (System.Exception e)
            {
                Logging.LogException(e, "Failed to stop driving");
            }
        }
        private void SpawnVehicle(Vector3 position, Quaternion rotation, VehicleInfo vehicleInfo, Color vehicleColor, bool setColor)
        {
            this.setColor = setColor;
            this.vehicleColor = vehicleColor;
            this.vehicleColor.a = 0; // Make sure blinking is not set.
            prevPosition = position;
            PrevVelocity = Vector3.zero;
            this.vehicleInfo = vehicleInfo;
            Gear = Direction.Neutral;
            terrainHeight = 0f;
            distanceTravelled = 0f;
            steer = 0f;
            Brake = 0f;
            throttle = 0f;
            compression = 0f;
            normalImpulse = 0f;
            prevGearChange = 0f;

            InitializeWheels();

            var vehicleMesh = this.vehicleInfo.m_mesh;
            var adjustedBounds = this.vehicleInfo.m_lodMesh.bounds.size;

            roofHeight = adjustedBounds.y;

            adjustedBounds.y -= rideHeight;

            float halfSA = ((adjustedBounds.x * adjustedBounds.y) + (adjustedBounds.x * adjustedBounds.z) + (adjustedBounds.y * adjustedBounds.z));
            vehicleRigidBody.drag = DRAG_FACTOR * adjustedBounds.x * adjustedBounds.y / halfSA;
            vehicleRigidBody.angularDrag = DRAG_FACTOR * adjustedBounds.y * adjustedBounds.z / halfSA;
            vehicleRigidBody.mass = halfSA * ModSettings.MassFactor;
            vehicleRigidBody.transform.position = position;
            vehicleRigidBody.transform.rotation = rotation;
            vehicleRigidBody.centerOfMass = new Vector3(0f, rideHeight + (adjustedBounds.y * ModSettings.MassCenterHeight), (ModSettings.MassCenterBias - 0.5f) * adjustedBounds.z * 0.5f);
            Vector3 squares = new Vector3(adjustedBounds.x * adjustedBounds.x, adjustedBounds.y * adjustedBounds.y, adjustedBounds.z * adjustedBounds.z);
            //m_vehicleRigidBody.inertiaTensor = 1.0f / 12.0f * m_vehicleRigidBody.mass * new Vector3(squares.y + squares.z, squares.x + squares.z, squares.x + squares.y);
            vehicleRigidBody.velocity = Vector3.zero;
            vehicleRigidBody.maxDepenetrationVelocity = DEPEN_VELOCITY;

            vehicleCollider.size = adjustedBounds;
            vehicleCollider.center = new Vector3(0f, (0.5f * adjustedBounds.y) + rideHeight, 0f);

            gameObject.GetComponent<MeshFilter>().mesh = gameObject.GetComponent<MeshFilter>().sharedMesh = vehicleMesh;
            gameObject.GetComponent<MeshRenderer>().material = gameObject.GetComponent<MeshRenderer>().sharedMaterial = vehicleInfo.m_material;
            //gameObject.GetComponent<MeshRenderer>().sortingLayerID = m_vehicleInfo.m_prefabDataLayer;

            if (this.setColor)
            {
                var materialBlock = Singleton<VehicleManager>.instance.m_materialBlock;
                materialBlock.Clear();
                materialBlock.SetColor(Singleton<VehicleManager>.instance.ID_Color, this.vehicleColor);
                gameObject.GetComponent<MeshRenderer>().SetPropertyBlock(materialBlock);
            }

            tangent = vehicleRigidBody.transform.TransformDirection(Vector3.forward);
            normal = vehicleRigidBody.transform.TransformDirection(Vector3.up);
            binormal = vehicleRigidBody.transform.TransformDirection(Vector3.right);

            gameObject.SetActive(true);

            effectManager.AddEffects(vehicleRigidBody, vehicleInfo);
        }

        private void InitializeWheels()
        {
            if (vehicleInfo.m_generatedInfo.m_tyres?.Length > 0)
            {
                rideHeight = vehicleInfo.m_generatedInfo.m_tyres[0].y;

                foreach (var tirepos in vehicleInfo.m_generatedInfo.m_tyres)
                {
                    wheelObjects.Add(Wheel.InstanceWheel(gameObject.transform, new Vector3(tirepos.x, tirepos.y + ModSettings.SpringOffset, tirepos.z), MOMENT_WHEEL, tirepos.w,
                        true, true, 0, 0, tirepos.z > 0f));
                }

                physicsFallback = false;

                if (Wheel.RearCount == 0 || Wheel.FrontCount == 0)
                {
                    physicsFallback = true;
                }
                else
                {
                    float frontTorque = ModSettings.DriveBias / Wheel.FrontCount;
                    float rearTorque = (1f - ModSettings.DriveBias) / Wheel.RearCount;
                    float frontBraking = ModSettings.BrakeBias * ModSettings.BrakingForce * KN_TO_N / Wheel.FrontCount;
                    float rearBraking = (1f - ModSettings.BrakeBias) * ModSettings.BrakingForce * KN_TO_N / Wheel.RearCount;

                    foreach (var w in wheelObjects)
                    {
                        if (w.IsFront)
                        {
                            w.torqueFract = frontTorque;
                            w.brakeForce = frontBraking;
                        }
                        else
                        {
                            w.torqueFract = rearTorque;
                            w.brakeForce = rearBraking;
                        }
                    }
                }

                foreach (var w in wheelObjects)
                {
                    float minDistX = float.PositiveInfinity;
                    float minDistZ = float.PositiveInfinity;

                    foreach (var wAlt in wheelObjects)
                    {
                        float dist = Vector3.Magnitude(w.origin - wAlt.origin);

                        if (Mathf.Abs(w.origin.x - wAlt.origin.x) > NEIGHBOR_WHEEL_DIST && dist < minDistX)
                        {
                            w.xWheel = wAlt;
                            minDistX = dist;
                        }
                        if (Mathf.Abs(w.origin.z - wAlt.origin.z) > NEIGHBOR_WHEEL_DIST && dist < minDistZ)
                        {
                            w.zWheel = wAlt;
                            minDistZ = dist;
                        }
                    }

                    if (w.xWheel == null || w.zWheel == null)
                    {
                        physicsFallback = true;
                    }
                }
            }
            else
            {
                rideHeight = 0;
                physicsFallback = true;
            }
        }

        private void DestroyVehicle()
        {
            effectManager.RemoveEffects();
            foreach (var w in wheelObjects)
            {
                DestroyImmediate(w.gameObject);
            }
            wheelObjects.Clear();
            gameObject.SetActive(false);
            vehicleRigidBody.velocity = Vector3.zero;
            vehicleRigidBody.angularVelocity = Vector3.zero;

            setColor = false;
            vehicleColor = default;
            vehicleInfo = null;
            prevPosition = Vector3.zero;
            PrevVelocity = Vector3.zero;
            tangent = Vector3.zero;
            binormal = Vector3.zero;
            normal = Vector3.zero;
            physicsFallback = false;
            Gear = Direction.Neutral;
            terrainHeight = 0f;
            distanceTravelled = 0f;
            steer = 0f;
            Brake = 0f;
            throttle = 0f;
            rideHeight = 0f;
            roofHeight = 0f;
            compression = 0f;
            normalImpulse = 0f;
            prevGearChange = 0f;
        }
        private static float GetTorque(float radps) // Torque curve 27x(k-x)/(4k^3)+max(3(k/2-x)^3/k^4,0)
        {
            // Check https://www.desmos.com/calculator/fp0csjaazj for formulation.
            float k = ENGINE_PEAK_POWER_RPS;
            float x = Mathf.Max(radps, ENGINE_IDLE_RPS);
            float rawval = (27.0f * x * (k - x) / (4.0f * k * k * k)) + Mathf.Max(3 * Mathf.Pow((k * 0.5f) - x, 3.0f) / (k * k * k * k), 0.0f);
            return ModSettings.EnginePower * KW_TO_W * (1.0f - DRAG_DRIVETRAIN) * rawval;
        }
        private void CalculateSlope(ref Vector3 vehiclePos, ref Vector3 vehicleVel, ref Vector3 vehicleAngularVel, float height, bool onGround) // TODO: fix slope calculation
        {
            var tangent = Vector3.forward;
            var binorm = Vector3.right;
            var lateral = vehicleRigidBody.transform.TransformDirection(Vector3.right);
            var forward = vehicleRigidBody.transform.TransformDirection(Vector3.forward);

            int slopeMode = onGround ? 2 : 1;

            if (slopeMode == 2)
            {
                tangent = vehiclePos - prevPosition;
                if (Vector3.Dot(tangent, forward) < 0f)
                {
                    tangent = -tangent;
                }
                tangent.y = height - terrainHeight;
                tangent -= (Vector3.Dot(tangent, lateral) * lateral);
                tangent = Vector3.Normalize(tangent);
                if (tangent.magnitude < 0.5f || Mathf.Abs(Vector3.Dot(tangent, Vector3.up)) < FLOAT_ERROR)
                {
                    slopeMode = 0;
                }
            }

            if (slopeMode == 1)
            {
                tangent = vehicleVel;
                if (Vector3.Dot(tangent, forward) < 0f)
                {
                    tangent = -tangent;
                }
                Vector3.Normalize(tangent - (Vector3.Dot(tangent, lateral) * lateral));
                if (tangent.magnitude < 0.5f || Mathf.Abs(Vector3.Dot(tangent, Vector3.up)) < FLOAT_ERROR)
                {
                    slopeMode = 0;
                }
            }

            if (slopeMode == 0)
            {
                tangent = this.tangent;
            }

            binorm = Vector3.Normalize(Vector3.Cross(Vector3.up, tangent));

            if (binorm.magnitude < 0.5f)
            {
                binorm = binormal;
            }

            Quaternion lookRot = Quaternion.LookRotation(tangent);

            vehicleRigidBody.MoveRotation(lookRot);

            //Vector3 torquet = Vector3.Cross(forward, tangent);
            //Vector3 torqueb = Vector3.Cross(lateral, binorm);

            //if (Vector3.Dot(forward, tangent) < 0f)
            //{
            //    torquet = Vector3.Normalize(torquet);
            //}

            //if (Vector3.Dot(lateral, binorm) < 0f)
            //{
            //    torqueb = Vector3.Normalize(torqueb);
            //}

            //m_vehicleRigidBody.AddTorque(-vehicleAngularVel * 0.5f * Time.fixedDeltaTime, ForceMode.VelocityChange); // scuffed as all hell
            //if (vehicleAngularVel.magnitude < 1f)
            //{
            //    if (torquet.magnitude > FLOAT_ERROR) m_vehicleRigidBody.AddTorque(60f * torquet * Time.fixedDeltaTime, ForceMode.VelocityChange);
            //    if (torqueb.magnitude > FLOAT_ERROR) m_vehicleRigidBody.AddTorque(60f * torqueb * Time.fixedDeltaTime, ForceMode.VelocityChange);
            //}


            this.tangent = tangent;
            binormal = binorm;
        }

        private void HandleInputOnFixedUpdate()
        {
            bool throttling = false;
            bool braking = false;
            if (FPCModSettings.Instance.XMLKeyMoveForward.IsPressed())
            {
                if (CurrentDirection == Direction.Reverse)
                {
                    if (Gear != Direction.Forward)
                    {
                        throttle = 0f;
                        Brake = Mathf.Clamp01(Brake + (Time.fixedDeltaTime / THROTTLE_RESP));
                        braking = true;
                    }
                }
                else if (throttle == 0f && Time.time > prevGearChange + GEAR_RESP && Gear != Direction.Forward)
                {
                    Gear++;
                    prevGearChange = Time.time;
                }

                if (Gear == Direction.Forward)
                {
                    Brake = 0f;
                    throttle = Mathf.Clamp01(throttle + (Time.fixedDeltaTime / THROTTLE_RESP));
                    throttling = true;
                }
            }
            else if (FPCModSettings.Instance.XMLKeyMoveBackward.IsPressed())
            {
                if (CurrentDirection == Direction.Forward)
                {
                    if (Gear != Direction.Reverse)
                    {
                        throttle = 0f;
                        Brake = Mathf.Clamp01(Brake + (Time.fixedDeltaTime / THROTTLE_RESP));
                        braking = true;
                    }
                }
                else if (throttle == 0f && Time.time > prevGearChange + GEAR_RESP && Gear != Direction.Reverse)
                {
                    Gear--;
                    prevGearChange = Time.time;
                }

                if (Gear == Direction.Reverse)
                {
                    Brake = 0f;
                    throttle = Mathf.Clamp01(throttle + (Time.fixedDeltaTime / THROTTLE_RESP));
                    throttling = true;
                }
            }
            else if (CurrentDirection == Direction.Neutral && throttle == 0f && Time.time > prevGearChange + GEAR_RESP && Gear != Direction.Reverse)
            {
                Gear = Direction.Neutral;
                prevGearChange = Time.time;
                Brake = 1f;
                braking = true;
            }
            if (!throttling)
            {
                throttle = Mathf.Clamp01(throttle - (Time.fixedDeltaTime / THROTTLE_RESP));
            }
            if (!braking)
            {
                Brake = Mathf.Clamp01(Brake - (Time.fixedDeltaTime / THROTTLE_RESP));
            }

            bool steering = false;
            float steerLimit = Mathf.Clamp(1f - (STEER_DECAY * vehicleRigidBody.velocity.magnitude), 0.01f, 1f);
            if (FPCModSettings.Instance.XMLKeyMoveRight.IsPressed())
            {
                float factor = (steer < 0f) ? STEER_RESP + STEER_REST : STEER_RESP;
                steer = Mathf.Clamp(steer + (Time.fixedDeltaTime * factor), -steerLimit, steerLimit);
                steering = true;
            }
            if (FPCModSettings.Instance.XMLKeyMoveLeft.IsPressed())
            {
                float factor = (steer > 0f) ? STEER_RESP + STEER_REST : STEER_RESP;
                steer = Mathf.Clamp(steer - (Time.fixedDeltaTime * factor), -steerLimit, steerLimit);
                steering = true;
            }
            if (!steering)
            {
                if (steer > 0f)
                {
                    steer = Mathf.Clamp(steer - (Time.fixedDeltaTime * STEER_REST), 0f, steerLimit);
                }
                if (steer < 0f)
                {
                    steer = Mathf.Clamp(steer + (Time.fixedDeltaTime * STEER_REST), -steerLimit, 0f);
                }
            }
        }

        private void HandleInputOnUpdate()
        {
            if (Input.GetKeyDown((KeyCode)ModSettings.KeyLightToggle.Key))
                effectManager.IsLightEnabled = !effectManager.IsLightEnabled;

            if (Input.GetKeyDown((KeyCode)ModSettings.KeySirenToggle.Key))
                effectManager.IsSirenEnabled = !effectManager.IsSirenEnabled;

            if (Input.GetKeyDown((KeyCode)ModSettings.KeyUnstuck.Key))
                Unstuck();
        }
    }
}