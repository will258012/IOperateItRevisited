using AlgernonCommons;
using ColossalFramework;
using IOperateIt.UI;
using IOperateIt.Utils;
using System.Collections.Generic;
using UnityEngine;

namespace IOperateIt
{
    public class DriveController : MonoBehaviour
    {
        private const float FLOAT_ERROR = 0.01f;
        private const float THROTTLE_RESP = 0.5f;
        private const float STEER_RESP = 0.35f;
        private const float GEAR_RESP = 0.25f;
        private const float PARK_SPEED = 0.5f;
        private const float STEER_MAX = 37.0f;
        private const float STEER_DECAY = 0.006f;
        private const float ROAD_WALL_HEIGHT = 0.75f;
        private const float LIGHT_HEADLIGHT_INTENSITY = 5.0f;
        private const float LIGHT_BRAKELIGHT_INTENSITY = 5.0f;
        private const float LIGHT_REARLIGHT_INTENSITY = 0.5f;
        private const float NEIGHBOR_WHEEL_DIST = 0.2f;
        private const float SPRING_DAMP = 6.0f;
        private const float SPRING_OFFSET = -0.1f;
        private const float SPRING_MAX_COMPRESS = 0.2f;
        private const float MASS_FACTOR = 85.0f;
        private const float DRAG_FACTOR = 0.25f;
        private const float MASS_COM_HEIGHT = 0.1f;
        private const float MASS_COM_BIAS = 0.6f;
        private const float DOWN_FORCE = 5.0f;
        private const float DRIVE_BIAS = 0.5f; 
        private const float BRAKE_BIAS = 0.7f;
        private const float VALID_INCLINE = 0.5f;
        private const float GRIP_OVERMATCH = 0.3f;
        private const float GRIP_COEFF = 1.0f;
        private const float GRIP_COEFF_K = 0.8f;
        private const float ACCEL_G = 10f * M_TO_UNIT;
        const float MS_TO_KMPH = 3.6f;
        const float UNIT_TO_M = 25.0f / 54.0f;
        const float M_TO_UNIT = 54.0f / 25.0f;
        const float UNIT_TO_MPH = UNIT_TO_M * 2.23694f;
        const float KN_TO_N = 1000f;
        const float KW_TO_W = 1000f;

        private struct NetInfoBackup
        {
            public NetInfoBackup(NetInfo.Node[] nodes, NetInfo.Segment[] segments)
            {
                this.nodes = nodes;
                this.segments = segments;
            }
            
            public NetInfo.Node[]      nodes;
            public NetInfo.Segment[]   segments;
        }

        private class Wheel : MonoBehaviour
        {
            //public TrailRenderer skidTrail;
            public static int wheelCount { get => wheels; private set => wheels = value; }

            private static int wheels = 0;

            public Wheel xWheel;
            public Wheel zWheel;
            public Vector3 tangent;
            public Vector3 binormal;
            public Vector3 normal;
            public Vector3 heightSample;
            public Vector3 origin;
            public float radius;
            public float power;
            public float brakeForce;
            public float normalImpulse;
            public float compression;
            public bool onGround;
            public bool isSimulated     { get => simulated; private set => simulated = value; }
            public bool isPowered       { get => powered; private set => powered = value; }
            public bool isSteerable     { get => steerable; private set => steerable = value; }
            public bool isInvertedSteer { get => inverted; private set => inverted = value; }

            private bool simulated;
            private bool powered;
            private bool steerable;
            private bool inverted;
            public static Wheel InstanceWheel(Transform parent, Vector3 localpos, float radius, bool isSimulated = true, bool isPowered = true, float power = 0.0f, float brakeForce = 0.0f, bool isSteerable = false, bool isInvertedSteer = false)
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
                w.origin = localpos;
                w.radius = radius;
                w.power = power;
                w.brakeForce = brakeForce;
                w.normalImpulse = 0.0f;
                w.compression = 0.0f;
                w.onGround = false;
                w.isSimulated = isSimulated;
                w.isPowered = isPowered;
                w.isSteerable = isSteerable;
                w.isInvertedSteer = isInvertedSteer;

                return w;
            }

            public void OnEnable()
            {
                if (isSimulated)
                {
                    wheelCount++;
                }
            }

            public void OnDisable()
            {
                if (isSimulated)
                {
                    wheelCount--;
                }
            }

            public void CalcRoadTBN()
            {
                Vector3 tmp = Vector3.Normalize(Vector3.Cross(xWheel.heightSample - this.heightSample, zWheel.heightSample - this.heightSample));
                float dotUp = Vector3.Dot(tmp, Vector3.up);
                if (dotUp < -FLOAT_ERROR)
                {
                    tmp = -tmp;
                }
                else if (dotUp < FLOAT_ERROR)
                {
                    tmp = Vector3.up;
                }

                    this.normal = tmp;
                this.binormal = Vector3.Normalize(Vector3.Cross(this.gameObject.transform.TransformDirection(Vector3.forward), this.normal));
                this.tangent = Vector3.Normalize(Vector3.Cross(this.normal, this.binormal));
            }
        }

        public static DriveController instance { get; private set; }

        private Rigidbody       m_vehicleRigidBody;
        private BoxCollider     m_vehicleCollider;
        private Color           m_vehicleColor;
        private bool            m_setColor;
        private VehicleInfo     m_vehicleInfo;

        private List<Wheel>         m_wheelObjects      = new List<Wheel>();

        private List<LightEffect>   m_lightEffects      = new List<LightEffect>();
        private List<EffectInfo>    m_regularEffects    = new List<EffectInfo>();
        private List<EffectInfo>    m_specialEffects    = new List<EffectInfo>();

        private Dictionary<string, string> m_customUndergroundMappings = new Dictionary<string, string>();
        private Dictionary<NetInfo, NetInfoBackup> m_backupPrefabData = new Dictionary<NetInfo, NetInfoBackup>();
        private Material m_backupUndergroundMaterial = null;

        private DriveColliders m_collidersManager = new DriveColliders();
        private Vector3 m_prevPosition;
        private Vector3 m_prevVelocity;
        private Vector3 m_tangent;
        private Vector3 m_binormal;
        private Vector3 m_normal;
        private Vector4 m_lightState;
        private bool m_isSirenEnabled = false;
        private bool m_isLightEnabled = false;
        private bool m_physicsFallback = false;

        private int m_gear = 0;
        private float m_terrainHeight = 0.0f;
        private float m_distanceTravelled = 0.0f;
        private float m_steer = 0.0f;
        private float m_brake = 0.0f;
        private float m_throttle = 0.0f;
        private float m_rideHeight = 0.0f;
        private float m_roofHeight = 0.0f;
        private float m_compression = 0.0f;
        private float m_prevGearChange = 0.0f;
        private float m_normalImpulse = 0.0f;
        private void Awake()
        {
            instance = this;

            gameObject.AddComponent<MeshFilter>();
            gameObject.AddComponent<MeshRenderer>();
            gameObject.GetComponent<MeshRenderer>().enabled = true;

            m_vehicleRigidBody = gameObject.AddComponent<Rigidbody>();
            m_vehicleRigidBody.isKinematic = false;
            m_vehicleRigidBody.useGravity = false;
            m_vehicleRigidBody.freezeRotation = false;
            m_vehicleRigidBody.interpolation = RigidbodyInterpolation.Interpolate;
            
            PhysicMaterial material = new PhysicMaterial();
            material.bounciness = 0.05f;
            material.staticFriction = 0.1f;

            m_vehicleCollider = gameObject.AddComponent<BoxCollider>();
            m_vehicleCollider.material = material;

            //m_suspensionCollider = gameObject.AddComponent<SphereCollider>();

            StartCoroutine(m_collidersManager.InitializeColliders());
            gameObject.SetActive(false);
            enabled = false;

            // Some tunnel names are atypical and need to be manually mapped.
            m_customUndergroundMappings["HighwayRamp Tunnel"]                        = "HighwayRampElevated";
            m_customUndergroundMappings["Metro Track"]                               = "Metro Track Elevated 01";
            m_customUndergroundMappings["Metro Station Track"]                       = "Metro Station Track Elevated 01";
            m_customUndergroundMappings["Large Oneway Road Tunnel"]                  = "Large Oneway Elevated";
            m_customUndergroundMappings["Metro Station Below Ground Bypass"]         = "Metro Station Track Elevated Bypass";
            m_customUndergroundMappings["Metro Station Below Ground Dual Island"]    = "Metro Station Track Elevated Dual Island";
            m_customUndergroundMappings["Metro Station Below Ground Island"]         = "Metro Station Track Elevated Island Platform";

        }
        private void Update()
        {
            HandleInputOnUpdate();
            PlayEffects();

            MaterialPropertyBlock materialBlock = Singleton<VehicleManager>.instance.m_materialBlock;
            materialBlock.Clear();
            //materialBlock.SetMatrix(Singleton<VehicleManager>.instance.ID_TyreMatrix, value);
            Vector4 tyrePosition = default;
            tyrePosition.x = m_steer * STEER_MAX / 180.0f * Mathf.PI;
            tyrePosition.y = m_distanceTravelled;
            tyrePosition.z = 0f;
            tyrePosition.w = 0f;
            materialBlock.SetVector(Singleton<VehicleManager>.instance.ID_TyrePosition, tyrePosition);

            m_lightState.x = m_isLightEnabled ? LIGHT_HEADLIGHT_INTENSITY : 0.0f;
            m_lightState.y = m_brake > 0.0f ? LIGHT_BRAKELIGHT_INTENSITY : (m_isLightEnabled ? LIGHT_REARLIGHT_INTENSITY : 0.0f);
            materialBlock.SetVector(Singleton<VehicleManager>.instance.ID_LightState, m_lightState);
            if (m_setColor)
            {
                materialBlock.SetColor(Singleton<VehicleManager>.instance.ID_Color, m_vehicleColor);
            }

            if (m_physicsFallback)
            {
                materialBlock.SetMatrix(Singleton < VehicleManager >.instance.ID_TyreMatrix, Matrix4x4.TRS(new Vector3(0.0f, Mathf.Clamp(m_terrainHeight - m_vehicleRigidBody.transform.position.y, SPRING_OFFSET, 0.0f), 0.0f), Quaternion.identity, Vector3.one));
            }
            else
            {
                materialBlock.SetMatrix(Singleton<VehicleManager>.instance.ID_TyreMatrix, Matrix4x4.identity);
            }

            gameObject.GetComponent<MeshRenderer>().SetPropertyBlock(materialBlock);

            DebugHelper.DrawDebugBox(m_vehicleCollider.size, m_vehicleCollider.transform.TransformPoint(m_vehicleCollider.center), m_vehicleCollider.transform.rotation, Color.magenta);

        }
        private void FixedUpdate()
        {
            Vector3 vehiclePos = m_vehicleRigidBody.transform.position;
            Vector3 vehicleVel = m_vehicleRigidBody.velocity;
            Vector3 vehicleAngularVel = m_vehicleRigidBody.angularVelocity;
            float speed = Vector3.Dot(Vector3.forward, m_vehicleRigidBody.transform.InverseTransformDirection(vehicleVel));
            int invert = Mathf.Abs(speed) < PARK_SPEED ? 0 : (speed > 0.0f ? 1 : -1);

            HandleInputOnFixedUpdate(invert);

            if (m_physicsFallback)
            {
                FallbackPhysics(ref vehiclePos, ref vehicleVel, ref vehicleAngularVel, invert);
            }
            else
            {
                WheelPhysics(ref vehiclePos, ref vehicleVel, ref vehicleAngularVel);
            }
            
            LimitVelocity();

            m_collidersManager.UpdateColliders(m_vehicleRigidBody.transform);

            m_distanceTravelled += invert * Vector3.Magnitude(vehiclePos - m_prevPosition);
            m_prevVelocity = vehicleVel;
            m_prevPosition = vehiclePos;
        }

        private void LateUpdate()
        {
        }
        private void OnDestroy()
        {
            m_collidersManager.DestroyColliders();
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

            ColliderContainer container = collision.collider.gameObject.GetComponent<ColliderContainer>();
            if (container.Type == ColliderContainer.ContainerType.TYPE_VEHICLE)
            {
                ref Vehicle otherVehicle = ref Singleton<VehicleManager>.instance.m_vehicles.m_buffer[container.ID];

                ref Vector3 otherVelocity = ref (otherVehicle.m_lastFrame <= 1 ?
                    ref (otherVehicle.m_lastFrame == 0 ? ref otherVehicle.m_frame0.m_velocity : ref otherVehicle.m_frame1.m_velocity) :
                    ref (otherVehicle.m_lastFrame == 2 ? ref otherVehicle.m_frame2.m_velocity : ref otherVehicle.m_frame3.m_velocity));

                float collisionOrientation = Vector3.Dot(Vector3.Normalize(m_vehicleRigidBody.position - collision.collider.transform.position), Vector3.Normalize(otherVelocity));

                otherVelocity = otherVelocity * 0.8f * (0.5f + (-collisionOrientation + 1.0f) * 0.25f);
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

        private void OnGUI()
        {
            if (Logging.DetailLogging)
                GUI.Label(new Rect(50f, 50f, 500f, 200f), "g: " + m_gear + " t: " + m_throttle + " b: " + m_brake + " s: " + m_vehicleRigidBody.velocity.magnitude * UNIT_TO_M * MS_TO_KMPH);
        }

        private void FallbackPhysics(ref Vector3 vehiclePos, ref Vector3 vehicleVel, ref Vector3 vehicleAngularVel, float invert)
        {
            m_vehicleRigidBody.AddForce(Vector3.down * ACCEL_G, ForceMode.Acceleration);

            float height = MapUtils.CalculateHeight(vehiclePos, m_roofHeight);
            bool onGround = vehiclePos.y + SPRING_OFFSET < m_terrainHeight;

            CalculateSlope(ref vehiclePos, ref vehicleVel, ref vehicleAngularVel, height, onGround);
            m_terrainHeight = height;


            if (vehiclePos.y + ROAD_WALL_HEIGHT < m_terrainHeight)
            {
                vehiclePos = m_prevPosition;
                vehicleVel = Vector3.zero;
                m_vehicleRigidBody.transform.position = vehiclePos;
                m_vehicleRigidBody.velocity = vehicleVel;
            }
            else if (onGround)
            {
                if (vehiclePos.y + SPRING_MAX_COMPRESS < m_terrainHeight)
                {
                    vehiclePos = new Vector3(vehiclePos.x, m_terrainHeight - SPRING_MAX_COMPRESS, vehiclePos.z);
                    m_vehicleRigidBody.transform.position = vehiclePos;
                }

                float compression = Mathf.Max(m_terrainHeight - (vehiclePos.y + SPRING_OFFSET), 0.0f);
                float springVel = (compression - m_compression) / Time.fixedDeltaTime;
                float deltaVel = -SPRING_DAMP * Mathf.Exp(-SPRING_DAMP * Time.fixedDeltaTime) * (compression + springVel * Time.fixedDeltaTime) + springVel * Mathf.Exp(-SPRING_DAMP * Time.fixedDeltaTime) - springVel;

                if (deltaVel < 0.0f)
                {
                    m_normalImpulse = -deltaVel * m_vehicleRigidBody.mass;
                }
                else
                {
                    m_normalImpulse = 0.0f;
                }
            }

            if (onGround)
            {
                var relativeVel = m_vehicleRigidBody.transform.InverseTransformDirection(vehicleVel);

                Vector3 longImpulse = Vector3.forward * m_gear * m_throttle * (Settings.ModSettings.EnginePower * KW_TO_W * M_TO_UNIT / (vehicleVel.magnitude + 1.0f)) * Time.fixedDeltaTime;

                if (m_gear == 0)
                {
                    longImpulse -= Vector3.forward * Mathf.Sign(relativeVel.z) * Mathf.Min(m_brake * (Settings.ModSettings.BrakingForce * KN_TO_N) * Time.fixedDeltaTime, Mathf.Abs(relativeVel.z) * m_vehicleRigidBody.mass);
                }
                else
                {
                    longImpulse -= Vector3.forward * m_gear * m_brake * (Settings.ModSettings.BrakingForce * KN_TO_N) * Time.fixedDeltaTime;
                }

                relativeVel.z = 0.0f;
                relativeVel.y = 0.0f;

                Vector3 netImpulse = (1.0f - GRIP_OVERMATCH) * longImpulse;

                netImpulse -= relativeVel * (1.0f - FLOAT_ERROR) * m_vehicleRigidBody.mass;
                netImpulse = Mathf.Min(netImpulse.magnitude, m_normalImpulse * GRIP_COEFF) * Vector3.Normalize(netImpulse);
                netImpulse += GRIP_OVERMATCH * longImpulse;
                netImpulse += Vector3.up * m_normalImpulse;
                netImpulse = m_vehicleRigidBody.transform.TransformDirection(netImpulse);
                m_vehicleRigidBody.AddForceAtPosition(netImpulse, new Vector3(vehiclePos.x, m_terrainHeight, vehiclePos.z), ForceMode.Impulse);

                float speedsteer = Mathf.Min(Mathf.Max(vehicleVel.magnitude * 80f / m_vehicleCollider.size.z, 0f), 60f);
                speedsteer = Mathf.Sign(m_steer) * Mathf.Min(Mathf.Abs(60f * m_steer), speedsteer);

                Vector3 angularTarget = (1.0f - FLOAT_ERROR) * (Vector3.up * invert * speedsteer * Time.fixedDeltaTime) - m_vehicleRigidBody.transform.InverseTransformDirection(vehicleAngularVel);

                m_vehicleRigidBody.AddRelativeTorque(angularTarget, ForceMode.VelocityChange);
            }


            m_compression = Mathf.Max(m_terrainHeight - (vehiclePos.y + SPRING_OFFSET), 0.0f);
        }

        private void WheelPhysics(ref Vector3 vehiclePos, ref Vector3 vehicleVel, ref Vector3 vehicleAngularVel)
        {
            Vector3 upVec = m_vehicleRigidBody.transform.TransformDirection(Vector3.up);

            m_vehicleRigidBody.AddForce(Vector3.down * (ACCEL_G * m_vehicleRigidBody.mass) - upVec * DOWN_FORCE * Mathf.Abs(Vector3.Dot(vehicleVel, m_vehicleRigidBody.transform.TransformDirection(Vector3.forward))), ForceMode.Force);

            foreach (Wheel w in m_wheelObjects) // first calculate the heights at each wheel to prep for road normal calcs
            {
                Vector3 wheelPos = w.gameObject.transform.position;
                w.heightSample = wheelPos;
                w.heightSample.y = MapUtils.CalculateHeight(wheelPos, m_roofHeight);

                if (wheelPos.y + ROAD_WALL_HEIGHT < w.heightSample.y)
                {
                    vehiclePos = m_prevPosition;
                    vehicleVel = -vehicleVel * 0.1f;
                    vehicleAngularVel = Vector3.zero;
                    m_vehicleRigidBody.angularVelocity = vehicleAngularVel;
                    m_vehicleRigidBody.velocity = vehicleVel;
                    m_vehicleRigidBody.transform.position = vehiclePos;
                    return;
                }
            }

            foreach (Wheel w in m_wheelObjects) // calculate the road normals and normal impulses. Update wheel suspension position.
            {
                if (w.isSteerable)
                {
                    w.gameObject.transform.localRotation = Quaternion.Euler(0, (w.isInvertedSteer ? -1.0f : 1.0f) * STEER_MAX * m_steer * Mathf.Clamp(1.0f - STEER_DECAY * vehicleVel.magnitude, 0.1f, 1.0f), 0);
                }
                else
                {
                    w.gameObject.transform.localRotation = Quaternion.Euler(0, 0, 0);
                }

                w.CalcRoadTBN();
                w.onGround = false;
                w.normalImpulse = 0.0f;
                float normDotUp = Vector3.Dot(w.normal, upVec);
                if (normDotUp > VALID_INCLINE)
                {
                    Vector3 originWheelBottom = m_vehicleRigidBody.transform.TransformPoint(w.origin + Vector3.down * w.radius);
                    float compression = Mathf.Max((Vector3.Dot(w.heightSample, w.normal) - Vector3.Dot(originWheelBottom, w.normal)) / normDotUp, 0.0f);
                    float springVel = (compression - w.compression) / Time.fixedDeltaTime;
                    float deltaVel = -SPRING_DAMP * Mathf.Exp(-SPRING_DAMP * Time.fixedDeltaTime) * (compression + springVel * Time.fixedDeltaTime) + springVel * Mathf.Exp(-SPRING_DAMP * Time.fixedDeltaTime) - springVel;

                    if (deltaVel < 0.0f)
                    {
                        w.onGround = true;
                        w.normalImpulse = m_vehicleRigidBody.mass * (-deltaVel) / (Wheel.wheelCount * normDotUp);
                    }

                    w.gameObject.transform.localPosition = new Vector3(w.origin.x, w.origin.y + compression, w.origin.z);
                    w.compression = compression;
                }
                else
                {
                    w.compression = 0.0f;
                    w.gameObject.transform.localPosition = w.origin;
                }
            }

            foreach(Wheel w in m_wheelObjects) // calculate the lateral and longitudinal forces. Apply all forces.
            {
                if (w.onGround)
                {
                    Vector3 netImpulse = Vector3.zero;
                    Vector3 worldContact = w.gameObject.transform.TransformPoint(new Vector3(0.0f, -w.radius, 0.0f));                
                    Vector3 worldVelocity = m_vehicleRigidBody.GetPointVelocity(worldContact);

                    float lateralFract = 0.0f;
                    foreach(Wheel wAlt in m_wheelObjects)
                    {
                        lateralFract += Vector3.Dot(w.binormal, wAlt.binormal) * wAlt.normalImpulse;
                    }
                    lateralFract = w.normalImpulse / lateralFract;

                    float lateralSpeed = Vector3.Dot(worldVelocity, w.binormal);
                    float lateralComponent = lateralFract * m_vehicleRigidBody.mass * lateralSpeed;
                    netImpulse -= lateralComponent * w.binormal;


                    float longSpeed = Vector3.Dot(worldVelocity, w.tangent);
                    float longComponent = 0.0f;

                    if (m_gear == 0)
                    {
                        longComponent -= Mathf.Sign(longSpeed) * Mathf.Min(m_brake * w.brakeForce * Time.fixedDeltaTime, Mathf.Abs(longSpeed) * m_vehicleRigidBody.mass / Wheel.wheelCount);
                    }
                    else
                    {
                        longComponent -= m_gear * m_brake * w.brakeForce * Time.fixedDeltaTime;
                    }
                    longComponent += m_gear * m_throttle * w.power / (Mathf.Abs(longSpeed) + 1.0f) * Time.fixedDeltaTime;

                    Vector3 longImpulse = w.tangent * longComponent;
                    netImpulse += (1.0f - GRIP_OVERMATCH) * longImpulse;

                    if (m_brake > 0.0f && Mathf.Abs(m_steer) > 0.85 && vehicleVel.magnitude > 25.0f) // kick out rear if braking while steering hard.
                    {
                        m_vehicleRigidBody.AddTorque(upVec * Mathf.Sign(m_steer) * (Mathf.Abs(m_steer) - 0.85f) * 0.06f, ForceMode.VelocityChange);
                    }

                    float gripCoefficient = GRIP_COEFF;
                    
                    if (w.isSteerable)
                    {
                        gripCoefficient = Mathf.Lerp(GRIP_COEFF, GRIP_COEFF_K, Mathf.Abs(lateralSpeed) * 0.05f);
                    }
                    else
                    {
                        gripCoefficient = Mathf.Lerp(GRIP_COEFF, GRIP_COEFF_K, Mathf.Abs(lateralSpeed) * 0.1f);
                    }

                    if (gripCoefficient < (GRIP_COEFF + GRIP_COEFF_K) / 2.0f)
                    {
                        DebugHelper.DrawDebugMarker(2.0f, w.transform.position, Color.yellow);
                    }

                    netImpulse = Vector3.Normalize(netImpulse) * Mathf.Min(w.normalImpulse * GRIP_COEFF, netImpulse.magnitude);

                    netImpulse += GRIP_OVERMATCH * longImpulse;

                    netImpulse += w.normalImpulse * w.normal;

                    m_vehicleRigidBody.AddForceAtPosition(netImpulse, worldContact, ForceMode.Impulse);
                }
                else
                {
                    float tireOffset = Mathf.Sqrt(Mathf.Abs(Vector3.Dot(upVec, w.normal)));
                    if (w.transform.position.y - w.radius * tireOffset < w.heightSample.y)
                    {
                        Vector3 pos = m_vehicleRigidBody.transform.position;
                        pos.y += w.heightSample.y - w.transform.position.y + w.radius * tireOffset;
                        m_vehicleRigidBody.transform.position = pos;

                        Vector3 normalVel = Vector3.Dot(w.normal, vehicleVel) * w.normal;

                        m_vehicleRigidBody.AddForce(-(1.0f - FLOAT_ERROR) * normalVel - (vehicleVel - normalVel) * 0.5f * Time.fixedDeltaTime, ForceMode.VelocityChange);

                        m_vehicleRigidBody.AddTorque(-vehicleAngularVel * 0.5f * Time.fixedDeltaTime, ForceMode.VelocityChange);
                        if (vehicleAngularVel.magnitude < 1.0f)
                        {
                            m_vehicleRigidBody.AddTorque(Vector3.Normalize(Vector3.Cross(upVec, w.normal)) * 0.25f, ForceMode.VelocityChange);
                        }
                    }
                }
            }
        }

        private void LimitVelocity()
        {
            if (m_vehicleRigidBody.velocity.magnitude > Settings.ModSettings.MaxVelocity / MS_TO_KMPH * M_TO_UNIT)
            {
                m_vehicleRigidBody.AddForce(m_vehicleRigidBody.velocity.normalized * Settings.ModSettings.MaxVelocity / MS_TO_KMPH * M_TO_UNIT - m_vehicleRigidBody.velocity, ForceMode.VelocityChange);
            }
        }

        public void updateColor(Color color, bool enable)
        {
            m_vehicleColor = color; 
            m_setColor = enable;
        }

        public void updateVehicleInfo(VehicleInfo info)
        {
            m_vehicleInfo = info; 
        }

        public bool isVehicleInfoSet()
        {
            return m_vehicleInfo != null;
        }

        public void StartDriving(Vector3 position, Quaternion rotation) => StartDriving(position, rotation, m_vehicleInfo, m_vehicleColor, m_setColor);
        public void StartDriving(Vector3 position, Quaternion rotation, VehicleInfo vehicleInfo, Color vehicleColor, bool setColor)
        {
            enabled = true;
            SpawnVehicle(position + new Vector3(0.0f, -SPRING_OFFSET, 0.0f), rotation, vehicleInfo, vehicleColor, setColor);
            OverridePrefabs();
            DriveCam.instance.EnableCam(m_vehicleRigidBody, 2.0f * m_vehicleCollider.size.z);
            DriveButtons.instance.SetDisable();
        }
        public void StopDriving()
        {
            StartCoroutine(m_collidersManager.DisableColliders());
            DriveCam.instance.DisableCam();
            DriveButtons.instance.SetEnable();
            RestorePrefabs();
            DestroyVehicle();
            enabled = false;
        }
        private void SpawnVehicle(Vector3 position, Quaternion rotation, VehicleInfo vehicleInfo, Color vehicleColor, bool setColor)
        {
            m_setColor = setColor;
            m_vehicleColor = vehicleColor;
            m_vehicleColor.a = 0; // Make sure blinking is not set.
            m_prevPosition = position;
            m_prevVelocity = Vector3.zero;
            m_lightState = Vector4.zero;
            m_vehicleInfo = vehicleInfo;
            m_gear = 0;
            m_terrainHeight = 0.0f;
            m_distanceTravelled = 0.0f;
            m_steer = 0.0f;
            m_brake = 0.0f;
            m_throttle = 0.0f;
            m_compression = 0.0f;
            m_normalImpulse = 0.0f;
            m_prevGearChange = 0.0f;

            m_vehicleInfo.CalculateGeneratedInfo();

            if (m_vehicleInfo.m_generatedInfo.m_tyres?.Length > 0)
            {
                m_rideHeight = m_vehicleInfo.m_generatedInfo.m_tyres[0].y;

                int wheelCount = m_vehicleInfo.m_generatedInfo.m_tyres.Length;
                int frontCount = 0;
                foreach (Vector4 tirepos in m_vehicleInfo.m_generatedInfo.m_tyres)
                {
                    if (tirepos.z > 0.0f)
                    {
                        frontCount++;
                    }
                }
                int rearCount = wheelCount - frontCount;
                float frontPower = DRIVE_BIAS * Settings.ModSettings.EnginePower * KW_TO_W;
                float rearPower = (1.0f - DRIVE_BIAS) * Settings.ModSettings.EnginePower * KW_TO_W;
                float frontBraking = BRAKE_BIAS * Settings.ModSettings.BrakingForce * KN_TO_N;
                float rearBraking = (1.0f - BRAKE_BIAS) * Settings.ModSettings.BrakingForce * KN_TO_N;

                foreach (Vector4 tirepos in m_vehicleInfo.m_generatedInfo.m_tyres)
                {
                    if (tirepos.z > 0.0f)
                    {
                        m_wheelObjects.Add(Wheel.InstanceWheel(gameObject.transform, new Vector3(tirepos.x, tirepos.y + SPRING_OFFSET, tirepos.z), tirepos.w, true, true, 
                            frontPower / frontCount, frontBraking / frontCount, true));
                    }
                    else
                    {
                        m_wheelObjects.Add(Wheel.InstanceWheel(gameObject.transform, new Vector3(tirepos.x, tirepos.y + SPRING_OFFSET, tirepos.z), tirepos.w, true, true,
                            rearPower / rearCount, rearBraking / rearCount, false));
                    }
                }

                m_physicsFallback = false;

                foreach (Wheel w in m_wheelObjects)
                {
                    float minDistX = float.PositiveInfinity;
                    float minDistZ = float.PositiveInfinity;

                    foreach(Wheel wAlt in m_wheelObjects)
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
                        m_physicsFallback = true;
                    }
                }
            }
            else
            {
                m_rideHeight = 0;
            }

            Mesh vehicleMesh = m_vehicleInfo.m_mesh;
            Vector3 adjustedBounds = m_vehicleInfo.m_lodMesh.bounds.size;

            m_roofHeight = adjustedBounds.y;

            adjustedBounds.y = adjustedBounds.y - m_rideHeight;

            float halfSA = (adjustedBounds.x * adjustedBounds.y + adjustedBounds.x * adjustedBounds.z + adjustedBounds.y * adjustedBounds.z);
            m_vehicleRigidBody.drag = DRAG_FACTOR * adjustedBounds.x * adjustedBounds.y / halfSA;
            m_vehicleRigidBody.angularDrag = DRAG_FACTOR * adjustedBounds.y * adjustedBounds.z / halfSA;
            m_vehicleRigidBody.mass = halfSA * MASS_FACTOR;
            m_vehicleRigidBody.transform.position = position;
            m_vehicleRigidBody.transform.rotation = rotation;
            m_vehicleRigidBody.centerOfMass = new Vector3(0.0f, m_rideHeight + adjustedBounds.y * MASS_COM_HEIGHT, (MASS_COM_BIAS - 0.5f) * adjustedBounds.z * 0.5f);
            Vector3 squares = new Vector3(adjustedBounds.x * adjustedBounds.x, adjustedBounds.y * adjustedBounds.y, adjustedBounds.z * adjustedBounds.z);
            m_vehicleRigidBody.inertiaTensor = 1.0f / 12.0f * m_vehicleRigidBody.mass * new Vector3(squares.y + squares.z, squares.x + squares.z, squares.x + squares.y);
            m_vehicleRigidBody.velocity = Vector3.zero;

            m_vehicleCollider.size = adjustedBounds;
            m_vehicleCollider.center = new Vector3(0.0f, 0.5f * adjustedBounds.y + m_rideHeight, 0.0f);

            gameObject.GetComponent<MeshFilter>().mesh = gameObject.GetComponent<MeshFilter>().sharedMesh = vehicleMesh;
            gameObject.GetComponent<MeshRenderer>().material = gameObject.GetComponent<MeshRenderer>().sharedMaterial = m_vehicleInfo.m_material;
            gameObject.GetComponent<MeshRenderer>().sortingLayerID = m_vehicleInfo.m_prefabDataLayer;

            if (m_setColor)
            {
                MaterialPropertyBlock materialBlock = Singleton<VehicleManager>.instance.m_materialBlock;
                materialBlock.Clear();
                materialBlock.SetColor(Singleton<VehicleManager>.instance.ID_Color, m_vehicleColor);
                gameObject.GetComponent<MeshRenderer>().SetPropertyBlock(materialBlock);
            }

            m_tangent = m_vehicleRigidBody.transform.TransformDirection(Vector3.forward);
            m_normal = m_vehicleRigidBody.transform.TransformDirection(Vector3.up);
            m_binormal = m_vehicleRigidBody.transform.TransformDirection(Vector3.right);

            gameObject.SetActive(true);

            AddEffects();
        }
        private void DestroyVehicle()
        {
            RemoveEffects();
            foreach (Wheel w in m_wheelObjects)
            {
                Object.DestroyImmediate(w.gameObject);
            }
            m_wheelObjects.Clear();
            gameObject.SetActive(false);
            m_vehicleRigidBody.velocity = Vector3.zero;
            m_vehicleRigidBody.angularVelocity = Vector3.zero;

            m_setColor = false;
            m_vehicleColor = default;
            m_vehicleInfo = null;
            m_prevPosition = Vector3.zero;
            m_prevVelocity = Vector3.zero;
            m_tangent = Vector3.zero;
            m_binormal = Vector3.zero;
            m_normal = Vector3.zero;
            m_lightState = Vector4.zero;
            m_isSirenEnabled = false;
            m_isLightEnabled = false;
            m_physicsFallback = false;
            m_gear = 0;
            m_terrainHeight = 0.0f;
            m_distanceTravelled = 0.0f;
            m_steer = 0.0f;
            m_brake = 0.0f;
            m_throttle = 0.0f;
            m_rideHeight = 0.0f;
            m_roofHeight = 0.0f;
            m_compression = 0.0f;
            m_normalImpulse = 0.0f;
            m_prevGearChange = 0.0f;
        }

        private void OverridePrefabs()
        {
            for (uint prefabIndex = 0; prefabIndex < PrefabCollection<VehicleInfo>.PrefabCount(); prefabIndex++)
            {
                VehicleInfo prefabVehicleInfo = PrefabCollection<VehicleInfo>.GetPrefab(prefabIndex);
                prefabVehicleInfo.m_undergroundMaterial = prefabVehicleInfo.m_material;
                prefabVehicleInfo.m_undergroundLodMaterial = prefabVehicleInfo.m_lodMaterialCombined;
                foreach (VehicleInfo.MeshInfo submesh in prefabVehicleInfo.m_subMeshes)
                {
                    if (submesh.m_subInfo)
                    {
                        VehicleInfoSub subVehicleInfo = (VehicleInfoSub)submesh.m_subInfo;
                        subVehicleInfo.m_undergroundMaterial = subVehicleInfo.m_material;
                        subVehicleInfo.m_undergroundLodMaterial = subVehicleInfo.m_lodMaterialCombined;
                    }
                }
            }

            int prefabCount = PrefabCollection<NetInfo>.PrefabCount();

            for (uint prefabIndex = 0; prefabIndex < prefabCount; prefabIndex++)
            {
                NetInfo prefabNetInfo = PrefabCollection<NetInfo>.GetPrefab(prefabIndex);
                NetInfo prefabReplaceInfo = null;

                if (prefabNetInfo.m_class.m_layer == ItemClass.Layer.MetroTunnels)
                {
                    string replaceName;
                    // get underground to elvated mapping.
                    if (!m_customUndergroundMappings.TryGetValue(prefabNetInfo.name, out replaceName))
                    {
                        replaceName = prefabNetInfo.name.Replace(" Tunnel", " Elevated");
                    }

                    // find the elevated counterpart prefab to be used as a reference.
                    for (uint otherPrefabIndex = 0; otherPrefabIndex < prefabCount; otherPrefabIndex++)
                    {
                        NetInfo tmpInfo = PrefabCollection<NetInfo>.GetPrefab(otherPrefabIndex);
                        if (tmpInfo.m_class.m_layer == ItemClass.Layer.Default && tmpInfo.name == replaceName)
                        {
                            prefabReplaceInfo = tmpInfo;
                            break;
                        }
                    }

                    // replace all underground segments and nodes with the elevated couterparts.
                    if (prefabReplaceInfo != null)
                    {
                        m_backupPrefabData[prefabNetInfo] = new NetInfoBackup(prefabNetInfo.m_nodes, prefabNetInfo.m_segments);

                        NetInfo.Segment[] segments = new NetInfo.Segment[prefabReplaceInfo.m_segments.Length];

                        for (int index = 0; index < prefabReplaceInfo.m_segments.Length; index++)
                        {
                            NetInfo.Segment newSegment = CopySegment(prefabReplaceInfo.m_segments[index]);
                            newSegment.m_layer = MapUtils.LAYER_UNDERGROUND;
                            segments[index] = newSegment;
                        }

                        NetInfo.Node[] nodes = new NetInfo.Node[prefabReplaceInfo.m_nodes.Length];

                        for (int index = 0; index < prefabReplaceInfo.m_nodes.Length; index++)
                        {
                            NetInfo.Node newNode = CopyNode(prefabReplaceInfo.m_nodes[index]);
                            newNode.m_layer = MapUtils.LAYER_UNDERGROUND;
                            newNode.m_flagsForbidden = newNode.m_flagsForbidden & ~NetNode.Flags.Underground;
                            nodes[index] = newNode;
                        }

                        prefabNetInfo.m_segments = segments;
                        prefabNetInfo.m_nodes = nodes;
                    }
                    else
                    {
                        Logging.Error("Failed to replace " + prefabNetInfo.name + " with " + replaceName);
                    }
                }
                else if (prefabNetInfo.name.Contains("Slope")) // only slope components have underground transition elements.
                {
                    m_backupPrefabData[prefabNetInfo] = new NetInfoBackup(prefabNetInfo.m_nodes, prefabNetInfo.m_segments);

                    NetInfo.Segment[] segments = new NetInfo.Segment[prefabNetInfo.m_segments.Length];

                    for (int index = 0; index < prefabNetInfo.m_segments.Length; index++)
                    {
                        NetInfo.Segment currSegment = prefabNetInfo.m_segments[index];
                        if (currSegment.m_layer == MapUtils.LAYER_UNDERGROUND) // disable slope underground component from rendering.
                        {
                            NetInfo.Segment newSegment = CopySegment(currSegment);
                            newSegment.m_forwardForbidden = NetSegment.Flags.All;
                            newSegment.m_forwardRequired = NetSegment.Flags.None;
                            newSegment.m_backwardForbidden = NetSegment.Flags.All;
                            newSegment.m_backwardRequired = NetSegment.Flags.None;
                            segments[index] = newSegment;
                        }
                        else
                        {
                            segments[index] = currSegment;
                        }
                    }

                    NetInfo.Node[] nodes = new NetInfo.Node[prefabNetInfo.m_nodes.Length];

                    for (int index = 0; index < prefabNetInfo.m_nodes.Length; index++)
                    {
                        NetInfo.Node newNode = CopyNode(prefabNetInfo.m_nodes[index]);
                        if (newNode.m_layer == MapUtils.LAYER_UNDERGROUND) // disable node underground component from rendering.
                        {
                            newNode.m_flagsForbidden = NetNode.Flags.All;
                            newNode.m_flagsRequired = NetNode.Flags.None;
                            nodes[index] = newNode;
                        }
                        else // allow the surface node to be rendered underground.
                        {
                            newNode.m_flagsForbidden = newNode.m_flagsForbidden & ~NetNode.Flags.Underground;
                            nodes[index] = newNode;
                        }
                    }

                    prefabNetInfo.m_segments = segments;
                    prefabNetInfo.m_nodes = nodes;
                }
            }

            // replace the distant LOD material for underground and update LOD render groups.
            RenderManager rm = Singleton<RenderManager>.instance;
            m_backupUndergroundMaterial = rm.m_groupLayerMaterials[MapUtils.LAYER_UNDERGROUND];
            rm.m_groupLayerMaterials[MapUtils.LAYER_UNDERGROUND] = rm.m_groupLayerMaterials[MapUtils.LAYER_ROAD];
            rm.UpdateGroups(MapUtils.LAYER_UNDERGROUND);
        }

        private void RestorePrefabs()
        {
            // delete all underground vehicle materials. Cities will auto generate new ones.
            for (uint iter = 0; iter < PrefabCollection<VehicleInfo>.PrefabCount(); iter++)
            {
                VehicleInfo prefabVehicleInfo = PrefabCollection<VehicleInfo>.GetPrefab(iter);
                prefabVehicleInfo.m_undergroundMaterial = null;
                prefabVehicleInfo.m_undergroundLodMaterial = null;
                foreach (VehicleInfo.MeshInfo submesh in prefabVehicleInfo.m_subMeshes)
                {
                    if (submesh.m_subInfo)
                    {
                        VehicleInfoSub subVehicleInfo = (VehicleInfoSub)submesh.m_subInfo;
                        subVehicleInfo.m_undergroundMaterial = null;
                        subVehicleInfo.m_undergroundLodMaterial = null;
                    }
                }
            }

            // restore road prefab segment and node data to before driving.
            int prefabCount = PrefabCollection<NetInfo>.PrefabCount();
            for (uint prefabIndex = 0; prefabIndex < prefabCount; prefabIndex++)
            {
                NetInfo prefabNetInfo = PrefabCollection<NetInfo>.GetPrefab(prefabIndex);

                if (prefabNetInfo.m_class.m_layer == ItemClass.Layer.MetroTunnels || prefabNetInfo.name.Contains("Slope"))
                {
                    if (m_backupPrefabData.TryGetValue(prefabNetInfo, out NetInfoBackup backupData))
                    {
                        prefabNetInfo.m_segments = backupData.segments;
                        prefabNetInfo.m_nodes = backupData.nodes;

                        m_backupPrefabData.Remove(prefabNetInfo);
                    }
                }
            }

            m_backupPrefabData.Clear();

            // restore the distant LOD material for underground and update LOD render groups.
            RenderManager rm = Singleton<RenderManager>.instance;
            rm.m_groupLayerMaterials[MapUtils.LAYER_UNDERGROUND] = m_backupUndergroundMaterial;
            m_backupUndergroundMaterial = null;
            rm.UpdateGroups(MapUtils.LAYER_UNDERGROUND);
        }

        private static NetInfo.Node CopyNode(NetInfo.Node node)
        {
            NetInfo.Node retval = new NetInfo.Node();
            retval.m_mesh = node.m_mesh;
            retval.m_lodMesh = node.m_lodMesh;
            retval.m_material = node.m_material;
            retval.m_lodMaterial = node.m_lodMaterial;
            retval.m_flagsRequired = node.m_flagsRequired;
            retval.m_flagsRequired2 = node.m_flagsRequired2;
            retval.m_flagsForbidden = node.m_flagsForbidden;
            retval.m_flagsForbidden2 = node.m_flagsForbidden2;
            retval.m_connectGroup = node.m_connectGroup;
            retval.m_directConnect = node.m_directConnect;
            retval.m_emptyTransparent = node.m_emptyTransparent;
            retval.m_tagsRequired = node.m_tagsRequired;
            retval.m_nodeTagsRequired = node.m_nodeTagsRequired;
            retval.m_tagsForbidden = node.m_tagsForbidden;
            retval.m_nodeTagsForbidden = node.m_nodeTagsForbidden;
            retval.m_forbidAnyTags = node.m_forbidAnyTags;
            retval.m_minSameTags = node.m_minSameTags;
            retval.m_maxSameTags = node.m_maxSameTags;
            retval.m_minOtherTags = node.m_minOtherTags;
            retval.m_maxOtherTags = node.m_maxOtherTags;
            retval.m_nodeMesh = node.m_nodeMesh;
            retval.m_nodeMaterial = node.m_nodeMaterial;
            retval.m_combinedLod = node.m_combinedLod;
            retval.m_lodRenderDistance = node.m_lodRenderDistance;
            retval.m_requireSurfaceMaps = node.m_requireSurfaceMaps;
            retval.m_requireWindSpeed = node.m_requireWindSpeed;
            retval.m_preserveUVs = node.m_preserveUVs;
            retval.m_generateTangents = node.m_generateTangents;
            retval.m_layer = node.m_layer;

            return retval;
        }

        private static NetInfo.Segment CopySegment(NetInfo.Segment segment)
        {
            NetInfo.Segment retval = new NetInfo.Segment();
            retval.m_mesh = segment.m_mesh;
            retval.m_lodMesh = segment.m_lodMesh;
            retval.m_material = segment.m_material;
            retval.m_lodMaterial = segment.m_lodMaterial;
            retval.m_forwardRequired = segment.m_forwardRequired;
            retval.m_forwardForbidden = segment.m_forwardForbidden;
            retval.m_backwardRequired = segment.m_backwardRequired;
            retval.m_backwardForbidden = segment.m_backwardForbidden;
            retval.m_emptyTransparent = segment.m_emptyTransparent;
            retval.m_disableBendNodes = segment.m_disableBendNodes;
            retval.m_segmentMesh = segment.m_segmentMesh;
            retval.m_segmentMaterial = segment.m_segmentMaterial;
            retval.m_combinedLod = segment.m_combinedLod;
            retval.m_lodRenderDistance = segment.m_lodRenderDistance;
            retval.m_requireSurfaceMaps = segment.m_requireSurfaceMaps;
            retval.m_requireHeightMap = segment.m_requireHeightMap;
            retval.m_requireWindSpeed = segment.m_requireWindSpeed;
            retval.m_preserveUVs = segment.m_preserveUVs;
            retval.m_generateTangents = segment.m_generateTangents;
            retval.m_layer = segment.m_layer;

            return retval;
        }

        // there are problems with replacing LodValue. It needs to be registered with NodeManager and/or RenderManager or there will be null reference errors.
        //private static NetInfo.LodValue CopyLodValue(NetInfo.LodValue value)
        //{
        //    NetInfo.LodValue retval = new NetInfo.LodValue();
        //    retval.m_key = value.m_key;
        //    retval.m_material = value.m_material;
        //    retval.m_lodMin = value.m_lodMin;
        //    retval.m_lodMax = value.m_lodMax;
        //    retval.m_surfaceTexA = value.m_surfaceTexA;
        //    retval.m_surfaceTexB = value.m_surfaceTexB;
        //    retval.m_surfaceMapping = value.m_surfaceMapping;
        //    retval.m_heightMap = value.m_heightMap;
        //    retval.m_heightMapping = value.m_heightMapping;

        //    return retval;
        //}
        private void CalculateSlope(ref Vector3 vehiclePos, ref Vector3 vehicleVel, ref Vector3 vehicleAngularVel, float height, bool onGround) // TODO: fix slope calculation
        {
            Vector3 tangent = Vector3.forward;
            Vector3 binorm = Vector3.right;
            Vector3 lateral = m_vehicleRigidBody.transform.TransformDirection(Vector3.right);
            Vector3 forward = m_vehicleRigidBody.transform.TransformDirection(Vector3.forward);

            int slopeMode = onGround ? 2 : 1;

            if (slopeMode == 2)
            {
                tangent = vehiclePos - m_prevPosition;
                if (Vector3.Dot(tangent, forward) < 0.0f)
                {
                    tangent = -tangent;
                }
                tangent.y = height - m_terrainHeight;
                tangent = tangent - Vector3.Dot(tangent, lateral) * lateral;
                tangent = Vector3.Normalize(tangent);
                if (tangent.magnitude < 0.5f || Mathf.Abs(Vector3.Dot(tangent, Vector3.up)) < FLOAT_ERROR)
                {
                    slopeMode = 0;
                }
            }

            if (slopeMode == 1)
            {
                tangent = vehicleVel;
                if (Vector3.Dot(tangent, forward) < 0.0f)
                {
                    tangent = -tangent;
                }
                Vector3.Normalize(tangent - Vector3.Dot(tangent, lateral) * lateral);
                if (tangent.magnitude < 0.5f || Mathf.Abs(Vector3.Dot(tangent, Vector3.up)) < FLOAT_ERROR)
                {
                    slopeMode = 0;
                }
            }

            if (slopeMode == 0)
            {
                tangent = m_tangent;
            }

            binorm = Vector3.Normalize(Vector3.Cross(Vector3.up, tangent));

            if (binorm.magnitude < 0.5f)
            {
                binorm = m_binormal;
            }

            Quaternion lookRot = Quaternion.LookRotation(tangent);

            m_vehicleRigidBody.MoveRotation(lookRot);

            //Vector3 torquet = Vector3.Cross(forward, tangent);
            //Vector3 torqueb = Vector3.Cross(lateral, binorm);

            //if (Vector3.Dot(forward, tangent) < 0.0f)
            //{
            //    torquet = Vector3.Normalize(torquet);
            //}

            //if (Vector3.Dot(lateral, binorm) < 0.0f)
            //{
            //    torqueb = Vector3.Normalize(torqueb);
            //}

            //m_vehicleRigidBody.AddTorque(-vehicleAngularVel * 0.5f * Time.fixedDeltaTime, ForceMode.VelocityChange); // scuffed as all hell
            //if (vehicleAngularVel.magnitude < 1.0f)
            //{
            //    if (torquet.magnitude > FLOAT_ERROR) m_vehicleRigidBody.AddTorque(60.0f * torquet * Time.fixedDeltaTime, ForceMode.VelocityChange);
            //    if (torqueb.magnitude > FLOAT_ERROR) m_vehicleRigidBody.AddTorque(60.0f * torqueb * Time.fixedDeltaTime, ForceMode.VelocityChange);
            //}


            m_tangent = tangent;
            m_binormal = binorm;
        }

        private void HandleInputOnFixedUpdate(int invert)
        {
            bool throttling = false;
            bool braking = false;
            if (Input.GetKey((KeyCode)Settings.ModSettings.KeyMoveForward.Key))
            {
                if (invert < 0)
                {
                    if (m_gear <= 0)
                    {
                        m_throttle = 0.0f;
                        m_brake = Mathf.Clamp(m_brake + Time.fixedDeltaTime / THROTTLE_RESP, 0.0f, 1.0f);
                        braking = true;
                    }
                }
                else if (m_throttle == 0.0f && Time.time > m_prevGearChange + GEAR_RESP && m_gear <= 0)
                {
                    m_gear++;
                    m_prevGearChange = Time.time;
                }

                if (m_gear > 0)
                {
                    m_brake = 0.0f;
                    m_throttle = Mathf.Clamp(m_throttle + Time.fixedDeltaTime / THROTTLE_RESP, 0.0f, 1.0f);
                    throttling = true;
                }
            }
            else if (Input.GetKey((KeyCode)Settings.ModSettings.KeyMoveBackward.Key))
            {
                if (invert > 0)
                {
                    if (m_gear >= 0)
                    {
                        m_throttle = 0.0f;
                        m_brake = Mathf.Clamp(m_brake + Time.fixedDeltaTime / THROTTLE_RESP, 0.0f, 1.0f);
                        braking = true;
                    }
                }
                else if (m_throttle == 0.0f && Time.time > m_prevGearChange + GEAR_RESP && m_gear >= 0)
                {
                    m_gear--;
                    m_prevGearChange = Time.time;
                }

                if (m_gear < 0)
                {
                    m_brake = 0.0f;
                    m_throttle = Mathf.Clamp(m_throttle + Time.fixedDeltaTime / THROTTLE_RESP, 0.0f, 1.0f);
                    throttling = true;
                }
            }
            else if (invert == 0 && m_throttle == 0.0f && Time.time > m_prevGearChange + GEAR_RESP && m_gear >= 0)
            {
                m_gear = 0;
                m_prevGearChange = Time.time;
                m_brake = 1.0f;
                braking = true;
            }
            if (!throttling)
            {
                m_throttle = Mathf.Clamp(m_throttle - Time.fixedDeltaTime / THROTTLE_RESP, 0.0f, 1.0f);
            }
            if (!braking)
            {
                m_brake = Mathf.Clamp(m_brake - Time.fixedDeltaTime / THROTTLE_RESP, 0.0f, 1.0f);
            }

            bool steering = false;
            if (Input.GetKey((KeyCode)Settings.ModSettings.KeyMoveRight.Key))
            {
                m_steer = Mathf.Clamp(m_steer + Time.fixedDeltaTime / STEER_RESP, -1.0f, 1.0f);
                steering = true;
            }
            if (Input.GetKey((KeyCode)Settings.ModSettings.KeyMoveLeft.Key))
            {
                m_steer = Mathf.Clamp(m_steer - Time.fixedDeltaTime / STEER_RESP, -1.0f, 1.0f);
                steering = true;
            }
            if (!steering)
            {
                if (m_steer > 0.0f)
                {
                    m_steer = Mathf.Clamp(m_steer - Time.fixedDeltaTime / STEER_RESP, 0.0f, 1.0f);
                }
                if (m_steer < 0.0f)
                {
                    m_steer = Mathf.Clamp(m_steer + Time.fixedDeltaTime / STEER_RESP, -1.0f, 0.0f);
                }
            }
        }

        private void HandleInputOnUpdate()
        {
            if (Input.GetKeyDown((KeyCode)Settings.ModSettings.KeyLightToggle.Key))
                m_isLightEnabled = !m_isLightEnabled;

            if (Input.GetKeyDown((KeyCode)Settings.ModSettings.KeySirenToggle.Key))
                m_isSirenEnabled = !m_isSirenEnabled;
        }
        private void AddEffects()
        {
            if (m_vehicleInfo.m_effects != null)
            {
                foreach (var effect in m_vehicleInfo.m_effects)
                {
                    {
                        if (effect.m_effect != null)
                        {
                            if (effect.m_vehicleFlagsRequired.IsFlagSet(Vehicle.Flags.Emergency1 | Vehicle.Flags.Emergency2))
                                m_specialEffects.Add(effect.m_effect);
                            else
                            {
                                if (effect.m_effect is MultiEffect multiEffect)
                                {
                                    foreach (var sub in multiEffect.m_effects)
                                        if (sub.m_effect is LightEffect lightEffect)
                                            m_lightEffects.Add(lightEffect);
                                }
                                m_regularEffects.Add(effect.m_effect);
                            }
                        }
                    }
                }
            }
        }
        private void PlayEffects()
        {
            var position = m_vehicleRigidBody.transform.position;
            var rotation = m_vehicleRigidBody.transform.rotation;
            var velocity = m_vehicleRigidBody.velocity;
            var acceleration = ((velocity - m_prevVelocity) / Time.fixedDeltaTime).magnitude;
            var swayPosition = Vector3.zero;
            var scale = Vector3.one;
            var matrix = m_vehicleInfo.m_vehicleAI.CalculateBodyMatrix(Vehicle.Flags.Created | Vehicle.Flags.Spawned, ref position, ref rotation, ref scale, ref swayPosition);
            var area = new EffectInfo.SpawnArea(matrix, m_vehicleInfo.m_lodMeshData);
            var listenerInfo = Singleton<AudioManager>.instance.CurrentListenerInfo;
            var audioGroup = Singleton<VehicleManager>.instance.m_audioGroup;
            RenderGroup.MeshData effectMeshData = m_vehicleInfo.m_vehicleAI.GetEffectMeshData();
            var area2 = new EffectInfo.SpawnArea(matrix, effectMeshData, m_vehicleInfo.m_generatedInfo.m_tyres, m_vehicleInfo.m_lightPositions);

            foreach (var regularEffect in m_regularEffects)
            {
                regularEffect.PlayEffect(default, area, velocity, acceleration, 1f, listenerInfo, audioGroup);
            }
            if (m_isLightEnabled)
                foreach (var light in m_lightEffects)
                {
                    light.RenderEffect(default, area2, velocity, acceleration, 1f, -1f, Singleton<SimulationManager>.instance.m_simulationTimeDelta, Singleton<RenderManager>.instance.CurrentCameraInfo);
                }

            if (m_isSirenEnabled)
                foreach (var specialEffect in m_specialEffects)
                {
                    specialEffect.RenderEffect(default, area2, velocity, acceleration, 1f, -1f, Singleton<SimulationManager>.instance.m_simulationTimeDelta, Singleton<RenderManager>.instance.CurrentCameraInfo);
                    specialEffect.PlayEffect(default, area, velocity, acceleration, 1f, listenerInfo, audioGroup);
                }
        }

        private void RemoveEffects()
        {
            m_lightEffects.Clear();
            m_regularEffects.Clear();
            m_specialEffects.Clear();
        }
    }
}