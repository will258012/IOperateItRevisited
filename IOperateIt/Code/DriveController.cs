extern alias FPC;
using AlgernonCommons;
using ColossalFramework;
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
        private const float FLOAT_ERROR = 0.01f;
        private const float THROTTLE_RESP = 0.5f;
        private const float STEER_RESP = 0.5f;
        private const float GEAR_RESP = 0.25f;
        private const float PARK_SPEED = 0.5f;
        private const float STEER_MAX = 37f;
        private const float STEER_DECAY = 0.01f;
        private const float ROAD_WALL_HEIGHT = 0.75f;
        private const float LIGHT_HEADLIGHT_INTENSITY = 5f;
        private const float LIGHT_BRAKELIGHT_INTENSITY = 5f;
        private const float LIGHT_REARLIGHT_INTENSITY = 0.5f;
        private const float NEIGHBOR_WHEEL_DIST = 0.2f;
        //private const float SPRING_DAMP = 6f;
        //private const float SPRING_OFFSET = -0.1f;
        private const float SPRING_MAX_COMPRESS = 0.2f;
        //private const float MASS_FACTOR = 85f;
        private const float DRAG_FACTOR = 0.25f;
        private const float DRAG_WHEEL = 0.04f;
        //private const float MASS_COM_HEIGHT = 0.1f;
        //private const float MASS_COM_BIAS = 0.6f;
        private const float MOMENT_WHEEL = 1.5f;
        //private const float DOWN_FORCE = 5f;
        //private const float DRIVE_BIAS = 0.5f; 
        //private const float BRAKE_BIAS = 0.7f;
        private const float VALID_INCLINE = 0.5f;
        //private const float GRIP_OVERMATCH = 0.3f;
        //private const float GRIP_COEFF = 1f;
        //private const float GRIP_COEFF_K = 0.8f;
        private const float GRIP_MAX_SLIP = 0.4f;
        private const float ENGINE_PEAK_POWER_RPS = 600f;
        private const float ENGINE_GEAR_RATIO = 4f;
        private const float ACCEL_G = 10f;
        const float MS_TO_KMPH = 3.6f;
        const float UNIT_TO_M = 25f / 54f;
        const float M_TO_UNIT = 54f / 25f;
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

            public NetInfo.Node[] nodes;
            public NetInfo.Segment[] segments;
        }

        public static DriveController instance { get; private set; }

        private Rigidbody vehicleRigidBody;
        private BoxCollider vehicleCollider;
        private Color vehicleColor;
        private bool setColor;
        private VehicleInfo vehicleInfo;

        private List<Wheel> wheelObjects = new List<Wheel>();

        private List<LightEffect> lightEffects = new List<LightEffect>();
        private List<EffectInfo> regularEffects = new List<EffectInfo>();
        private List<EffectInfo> specialEffects = new List<EffectInfo>();

        private Dictionary<string, string> customUndergroundMappings = new Dictionary<string, string>();
        private Dictionary<NetInfo, NetInfoBackup> backupPrefabData = new Dictionary<NetInfo, NetInfoBackup>();
        private Material backupUndergroundMaterial = null;

        private DriveColliders collidersManager = new DriveColliders();
        private Vector3 prevPosition;
        private Vector3 prevVelocity;
        private Vector3 tangent;
        private Vector3 binormal;
        private Vector3 normal;
        private Vector4 lightState;
        private bool isSirenEnabled = false;
        private bool isLightEnabled = false;
        private bool physicsFallback = false;

        private int m_gear = 0;
        private float m_terrainHeight = 0f;
        private float m_distanceTravelled = 0f;
        private float m_steer = 0f;
        private float brake = 0f;
        private float throttle = 0f;
        private float rideHeight = 0f;
        private float roofHeight = 0f;
        private float compression = 0f;
        private float prevGearChange = 0f;
        private float normalImpulse = 0f;
        private float radps = 0f;
        private float torque = 0f;

        private void Awake()
        {
            instance = this;

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

            // Some tunnel names are atypical and need to be manually mapped.
            customUndergroundMappings["HighwayRamp Tunnel"] = "HighwayRampElevated";
            customUndergroundMappings["Metro Track"] = "Metro Track Elevated 01";
            customUndergroundMappings["Metro Station Track"] = "Metro Station Track Elevated 01";
            customUndergroundMappings["Large Oneway Road Tunnel"] = "Large Oneway Elevated";
            customUndergroundMappings["Metro Station Below Ground Bypass"] = "Metro Station Track Elevated Bypass";
            customUndergroundMappings["Metro Station Below Ground Dual Island"] = "Metro Station Track Elevated Dual Island";
            customUndergroundMappings["Metro Station Below Ground Island"] = "Metro Station Track Elevated Island Platform";

        }
        private void Update()
        {
            HandleInputOnUpdate();
            PlayEffects();

            MaterialPropertyBlock materialBlock = Singleton<VehicleManager>.instance.m_materialBlock;
            materialBlock.Clear();
            //materialBlock.SetMatrix(Singleton<VehicleManager>.instance.ID_TyreMatrix, value);
            Vector4 tyrePosition = default;
            tyrePosition.x = m_steer * STEER_MAX / 180f * Mathf.PI;
            tyrePosition.y = m_distanceTravelled;
            tyrePosition.z = 0f;
            tyrePosition.w = 0f;
            materialBlock.SetVector(Singleton<VehicleManager>.instance.ID_TyrePosition, tyrePosition);

            lightState.x = isLightEnabled ? LIGHT_HEADLIGHT_INTENSITY : 0f;
            lightState.y = brake > 0f ? LIGHT_BRAKELIGHT_INTENSITY : (isLightEnabled ? LIGHT_REARLIGHT_INTENSITY : 0f);
            materialBlock.SetVector(Singleton<VehicleManager>.instance.ID_LightState, lightState);
            if (setColor)
            {
                materialBlock.SetColor(Singleton<VehicleManager>.instance.ID_Color, vehicleColor);
            }

            if (physicsFallback)
            {
                materialBlock.SetMatrix(Singleton<VehicleManager>.instance.ID_TyreMatrix, Matrix4x4.TRS(new Vector3(0f, Mathf.Clamp(m_terrainHeight - vehicleRigidBody.transform.position.y, ModSettings.SpringOffset, 0f), 0f), Quaternion.identity, Vector3.one));
            }
            else
            {
                materialBlock.SetMatrix(Singleton<VehicleManager>.instance.ID_TyreMatrix, Matrix4x4.identity);
            }

            gameObject.GetComponent<MeshRenderer>().SetPropertyBlock(materialBlock);
#if DEBUG
            DebugHelper.DrawDebugBox(vehicleCollider.size, vehicleCollider.transform.TransformPoint(vehicleCollider.center), vehicleCollider.transform.rotation, Color.magenta);
#endif
        }
        private void FixedUpdate()
        {
            Vector3 vehiclePos = vehicleRigidBody.transform.position;
            Vector3 vehicleVel = vehicleRigidBody.velocity;
            Vector3 vehicleAngularVel = vehicleRigidBody.angularVelocity;
            float speed = Vector3.Dot(Vector3.forward, vehicleRigidBody.transform.InverseTransformDirection(vehicleVel));
            int invert = Mathf.Abs(speed) < PARK_SPEED ? 0 : (speed > 0f ? 1 : -1);

            HandleInputOnFixedUpdate(invert);

            if (physicsFallback)
            {
                FallbackPhysics(ref vehiclePos, ref vehicleVel, ref vehicleAngularVel, invert);
            }
            else
            {
                WheelPhysics(ref vehiclePos, ref vehicleVel, ref vehicleAngularVel);
            }

            LimitVelocity();

            collidersManager.UpdateColliders(vehicleRigidBody.transform);

            m_distanceTravelled += invert * Vector3.Magnitude(vehiclePos - prevPosition);
            prevVelocity = vehicleVel;
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

            ColliderContainer container = collision.collider.gameObject.GetComponent<ColliderContainer>();
            if (container.Type == ColliderContainer.ContainerType.TYPE_VEHICLE)
            {
                ref Vehicle otherVehicle = ref Singleton<VehicleManager>.instance.m_vehicles.m_buffer[container.ID];

                ref Vector3 otherVelocity = ref (otherVehicle.m_lastFrame <= 1 ?
                    ref (otherVehicle.m_lastFrame == 0 ? ref otherVehicle.m_frame0.m_velocity : ref otherVehicle.m_frame1.m_velocity) :
                    ref (otherVehicle.m_lastFrame == 2 ? ref otherVehicle.m_frame2.m_velocity : ref otherVehicle.m_frame3.m_velocity));

                float collisionOrientation = Vector3.Dot(Vector3.Normalize(vehicleRigidBody.position - collision.collider.transform.position), Vector3.Normalize(otherVelocity));

                otherVelocity = otherVelocity * 0.8f * (0.5f + (-collisionOrientation + 1f) * 0.25f);
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
#if DEBUG
            GUI.Label(new Rect(50f, 50f, 500f, 500f), "g: " + m_gear + "\nt: " + throttle + "\nb: " + brake +
                    "\ns: " + vehicleRigidBody.velocity.magnitude * MS_TO_KMPH + "\nrps: " + radps + "\n w: " + Wheel.FrontCount + " " + Wheel.RearCount);
#endif
        }

        private void FallbackPhysics(ref Vector3 vehiclePos, ref Vector3 vehicleVel, ref Vector3 vehicleAngularVel, float invert)
        {
            vehicleRigidBody.AddForce(Vector3.down * ACCEL_G, ForceMode.Acceleration);

            float height = MapUtils.CalculateHeight(vehiclePos, roofHeight);
            bool onGround = vehiclePos.y + ModSettings.SpringOffset < m_terrainHeight;

            CalculateSlope(ref vehiclePos, ref vehicleVel, ref vehicleAngularVel, height, onGround);
            m_terrainHeight = height;


            if (vehiclePos.y + ROAD_WALL_HEIGHT < m_terrainHeight)
            {
                vehiclePos = prevPosition;
                vehicleVel = Vector3.zero;
                vehicleRigidBody.transform.position = vehiclePos;
                vehicleRigidBody.velocity = vehicleVel;
            }
            else if (onGround)
            {
                if (vehiclePos.y + SPRING_MAX_COMPRESS < m_terrainHeight)
                {
                    vehiclePos = new Vector3(vehiclePos.x, m_terrainHeight - SPRING_MAX_COMPRESS, vehiclePos.z);
                    vehicleRigidBody.transform.position = vehiclePos;
                }

                float compression = Mathf.Max(m_terrainHeight - (vehiclePos.y + ModSettings.SpringOffset), 0f);
                float springVel = (compression - this.compression) / Time.fixedDeltaTime;
                float deltaVel = -ModSettings.SpringDamp * Mathf.Exp(-ModSettings.SpringDamp * Time.fixedDeltaTime) * (compression + springVel * Time.fixedDeltaTime) + springVel * Mathf.Exp(-ModSettings.SpringDamp * Time.fixedDeltaTime) - springVel;

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

                Vector3 longImpulse = Vector3.forward * m_gear * throttle * (Settings.ModSettings.EnginePower * KW_TO_W / (vehicleVel.magnitude + 1f)) * Time.fixedDeltaTime;

                if (m_gear == 0)
                {
                    longImpulse -= Vector3.forward * Mathf.Sign(relativeVel.z) * Mathf.Min(brake * (Settings.ModSettings.BrakingForce * KN_TO_N) * Time.fixedDeltaTime, Mathf.Abs(relativeVel.z) * vehicleRigidBody.mass);
                }
                else
                {
                    longImpulse -= Vector3.forward * m_gear * brake * (Settings.ModSettings.BrakingForce * KN_TO_N) * Time.fixedDeltaTime;
                }

                relativeVel.z = 0f;
                relativeVel.y = 0f;

                Vector3 netImpulse = (1f - ModSettings.GripOvermatch) * longImpulse;

                netImpulse -= relativeVel * (1f - FLOAT_ERROR) * vehicleRigidBody.mass;
                netImpulse = Mathf.Min(netImpulse.magnitude, normalImpulse * ModSettings.GripCoeffS) * Vector3.Normalize(netImpulse);
                netImpulse += ModSettings.GripOvermatch * longImpulse;
                netImpulse += Vector3.up * normalImpulse;
                netImpulse = vehicleRigidBody.transform.TransformDirection(netImpulse);
                vehicleRigidBody.AddForceAtPosition(netImpulse, new Vector3(vehiclePos.x, m_terrainHeight, vehiclePos.z), ForceMode.Impulse);

                float speedsteer = Mathf.Min(Mathf.Max(vehicleVel.magnitude * 80f / vehicleCollider.size.z, 0f), 60f);
                speedsteer = Mathf.Sign(m_steer) * Mathf.Min(Mathf.Abs(60f * m_steer), speedsteer);

                Vector3 angularTarget = (1f - FLOAT_ERROR) * (Vector3.up * invert * speedsteer * Time.fixedDeltaTime) - vehicleRigidBody.transform.InverseTransformDirection(vehicleAngularVel);

                vehicleRigidBody.AddRelativeTorque(angularTarget, ForceMode.VelocityChange);
            }


            compression = Mathf.Max(m_terrainHeight - (vehiclePos.y + ModSettings.SpringOffset), 0f);
        }

        private void WheelPhysics(ref Vector3 vehiclePos, ref Vector3 vehicleVel, ref Vector3 vehicleAngularVel)
        {
            Vector3 upVec = vehicleRigidBody.transform.TransformDirection(Vector3.up);

            vehicleRigidBody.AddForce(Vector3.down * (ACCEL_G * vehicleRigidBody.mass) - upVec * ModSettings.DownForce * Mathf.Abs(Vector3.Dot(vehicleVel, vehicleRigidBody.transform.TransformDirection(Vector3.forward))), ForceMode.Force);

            foreach (Wheel w in wheelObjects) // first calculate the heights at each wheel to prep for road normal calcs
            {
                Vector3 wheelPos = w.gameObject.transform.position;
                w.heightSample = wheelPos;
                w.heightSample.y = MapUtils.CalculateHeight(wheelPos, roofHeight);

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

            foreach (Wheel w in wheelObjects) // calculate the road normals. apply angular friction from previous tick. calculate normal impulses. Update wheel suspension position.
            {
                if (w.IsSteerable)
                {
                    w.gameObject.transform.localRotation = Quaternion.Euler(0, (w.IsInvertedSteer ? -1f : 1f) * STEER_MAX * m_steer, 0);
                }
                else
                {
                    w.gameObject.transform.localRotation = Quaternion.Euler(0, 0, 0);
                }

                w.CalcRoadTBN();

                if (w.onGround)
                {
                    Vector3 prelimContactVel = vehicleRigidBody.GetPointVelocity(w.contactPoint);
                    Vector2 flatImpulses = new Vector2(w.binormalImpulse, w.tangentImpulse);
                    float radDelta = Vector3.Dot(prelimContactVel, w.tangent) / w.radius - w.radps;
                    w.radps += Mathf.Sign(radDelta) * Mathf.Min(Mathf.Abs(radDelta), w.normalImpulse * w.radius * w.frictionCoeff / w.moment);
                }
                w.radps *= 1f - (DRAG_WHEEL * Time.fixedDeltaTime);

                w.onGround = false;
                w.normalImpulse = 0f;
                w.frictionCoeff = ModSettings.GripCoeffK;
                float normDotUp = Vector3.Dot(w.normal, upVec);
                if (normDotUp > VALID_INCLINE)
                {
                    Vector3 originWheelBottom = vehicleRigidBody.transform.TransformPoint(w.origin + Vector3.down * w.radius);
                    float compression = Mathf.Max((Vector3.Dot(w.heightSample, w.normal) - Vector3.Dot(originWheelBottom, w.normal)) / normDotUp, 0f);
                    float springVel = (compression - w.compression) / Time.fixedDeltaTime;
                    float deltaVel = -ModSettings.SpringDamp * Mathf.Exp(-ModSettings.SpringDamp * Time.fixedDeltaTime) * (compression + springVel * Time.fixedDeltaTime) + springVel * Mathf.Exp(-ModSettings.SpringDamp * Time.fixedDeltaTime) - springVel;

                    w.gameObject.transform.localPosition = new Vector3(w.origin.x, w.origin.y + compression, w.origin.z);
                    w.compression = compression;

                    if (deltaVel < 0f)
                    {
                        w.onGround = true;
                        w.normalImpulse = vehicleRigidBody.mass * (-deltaVel) / (Wheel.WheelCount * normDotUp);
                        w.contactPoint = w.gameObject.transform.TransformPoint(new Vector3(0f, -w.radius, 0f));
                        w.contactVelocity = vehicleRigidBody.GetPointVelocity(w.contactPoint);
                        w.frictionCoeff = Mathf.Lerp(ModSettings.GripCoeffS, ModSettings.GripCoeffK,
                            Mathf.Clamp(Vector3.Magnitude(w.contactVelocity - (w.radps * w.radius * w.tangent)) / Mathf.Max(w.contactVelocity.magnitude, 1f) / GRIP_MAX_SLIP, 0f, 1f));
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
            foreach (Wheel w in wheelObjects)
            {
                engineRps += w.radps * w.torqueFract;
            }
            radps = engineRps * ENGINE_GEAR_RATIO;
            torque = -ENGINE_GEAR_RATIO * ModSettings.EnginePower * KW_TO_W * (Mathf.Abs(radps) - 2f * ENGINE_PEAK_POWER_RPS) / (ENGINE_PEAK_POWER_RPS * ENGINE_PEAK_POWER_RPS);



            foreach (Wheel w in wheelObjects) // calculate the lateral and longitudinal forces. Apply all forces.
            {
                if (w.onGround)
                {
                    Vector3 netImpulse = Vector3.zero;

                    float normalContribution = 0f;
                    foreach (Wheel wAlt in wheelObjects)
                    {
                        normalContribution += Vector3.Dot(w.binormal, wAlt.binormal) * wAlt.normalImpulse;
                    }
                    normalContribution = w.normalImpulse / normalContribution;

                    Vector2 flatImpulses = Vector2.zero;

                    float lateralSpeed = Vector3.Dot(w.contactVelocity, w.binormal);
                    float lateralComponent = -normalContribution * vehicleRigidBody.mass * lateralSpeed;

                    flatImpulses.x = lateralComponent;

                    float wheelTorque;
                    wheelTorque = m_gear * throttle * w.torqueFract * torque;
                    w.radps += wheelTorque * Time.fixedDeltaTime / w.moment;
                    wheelTorque = -Mathf.Sign(w.radps) * Mathf.Min(brake * w.brakeForce * w.radius, Mathf.Abs(w.radps) * w.moment / Time.fixedDeltaTime);
                    w.radps += wheelTorque * Time.fixedDeltaTime / w.moment;

                    float longSpeed = Vector3.Dot(w.contactVelocity, w.tangent);
                    float longComponent = normalContribution * vehicleRigidBody.mass * (w.radps * w.radius - longSpeed);

                    flatImpulses.y = longComponent;
#if DEBUG
                    if (w.frictionCoeff < (ModSettings.GripCoeffS + ModSettings.GripCoeffK) / 2f)
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
                    if (w.transform.position.y - w.radius * tireOffset < w.heightSample.y)
                    {
                        Vector3 pos = vehicleRigidBody.transform.position;
                        pos.y += w.heightSample.y - w.transform.position.y + w.radius * tireOffset;
                        vehicleRigidBody.transform.position = pos;

                        Vector3 normalVel = Vector3.Dot(w.normal, vehicleVel) * w.normal;

                        vehicleRigidBody.AddForce(-(1f - FLOAT_ERROR) * normalVel - (vehicleVel - normalVel) * 0.5f * Time.fixedDeltaTime, ForceMode.VelocityChange);

                        vehicleRigidBody.AddTorque(-vehicleAngularVel * 0.5f * Time.fixedDeltaTime, ForceMode.VelocityChange);
                        if (vehicleAngularVel.magnitude < 1f)
                        {
                            vehicleRigidBody.AddTorque(Vector3.Normalize(Vector3.Cross(upVec, w.normal)) * 0.25f, ForceMode.VelocityChange);
                        }
                    }

                    float wheelTorque;
                    wheelTorque = m_gear * throttle * w.torqueFract * torque;
                    w.radps += wheelTorque * Time.fixedDeltaTime / w.moment;
                    wheelTorque = -Mathf.Sign(w.radps) * Mathf.Min(brake * w.brakeForce * w.radius, Mathf.Abs(w.radps) * w.moment / Time.fixedDeltaTime);
                    w.radps += wheelTorque * Time.fixedDeltaTime / w.moment;
                }
            }
        }

        private void LimitVelocity()
        {
            if (vehicleRigidBody.velocity.magnitude > Settings.ModSettings.MaxVelocity / MS_TO_KMPH)
            {
                vehicleRigidBody.AddForce(vehicleRigidBody.velocity.normalized * Settings.ModSettings.MaxVelocity / MS_TO_KMPH - vehicleRigidBody.velocity, ForceMode.VelocityChange);
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
            enabled = true;
            SpawnVehicle(position + new Vector3(0f, -ModSettings.SpringOffset, 0f), rotation, vehicleInfo, vehicleColor, setColor);
            OverridePrefabs();
            DriveCamController.instance.EnableCam(vehicleRigidBody, 2f * vehicleCollider.size.z);
            DriveButtons.instance.SetDisable();
        }
        public void StopDriving()
        {
            StartCoroutine(collidersManager.DisableColliders());
            DriveCamController.instance.DisableCam();
            DriveButtons.instance.SetEnable();
            RestorePrefabs();
            DestroyVehicle();
            enabled = false;
        }
        private void SpawnVehicle(Vector3 position, Quaternion rotation, VehicleInfo vehicleInfo, Color vehicleColor, bool setColor)
        {
            this.setColor = setColor;
            this.vehicleColor = vehicleColor;
            this.vehicleColor.a = 0; // Make sure blinking is not set.
            prevPosition = position;
            prevVelocity = Vector3.zero;
            lightState = Vector4.zero;
            this.vehicleInfo = vehicleInfo;
            m_gear = 0;
            m_terrainHeight = 0f;
            m_distanceTravelled = 0f;
            m_steer = 0f;
            brake = 0f;
            throttle = 0f;
            compression = 0f;
            normalImpulse = 0f;
            prevGearChange = 0f;

            this.vehicleInfo.CalculateGeneratedInfo();

            if (this.vehicleInfo.m_generatedInfo.m_tyres?.Length > 0)
            {
                rideHeight = this.vehicleInfo.m_generatedInfo.m_tyres[0].y;

                foreach (Vector4 tirepos in this.vehicleInfo.m_generatedInfo.m_tyres)
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
                    float frontBraking = ModSettings.BrakeBias * Settings.ModSettings.BrakingForce * KN_TO_N / Wheel.FrontCount;
                    float rearBraking = (1f - ModSettings.BrakeBias) * Settings.ModSettings.BrakingForce * KN_TO_N / Wheel.RearCount;

                    foreach (Wheel w in wheelObjects)
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

                foreach (Wheel w in wheelObjects)
                {
                    float minDistX = float.PositiveInfinity;
                    float minDistZ = float.PositiveInfinity;

                    foreach (Wheel wAlt in wheelObjects)
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
            }

            Mesh vehicleMesh = this.vehicleInfo.m_mesh;
            Vector3 adjustedBounds = this.vehicleInfo.m_lodMesh.bounds.size;

            roofHeight = adjustedBounds.y;

            adjustedBounds.y = adjustedBounds.y - rideHeight;

            float halfSA = (adjustedBounds.x * adjustedBounds.y + adjustedBounds.x * adjustedBounds.z + adjustedBounds.y * adjustedBounds.z);
            vehicleRigidBody.drag = DRAG_FACTOR * adjustedBounds.x * adjustedBounds.y / halfSA;
            vehicleRigidBody.angularDrag = DRAG_FACTOR * adjustedBounds.y * adjustedBounds.z / halfSA;
            vehicleRigidBody.mass = halfSA * ModSettings.MassFactor;
            vehicleRigidBody.transform.position = position;
            vehicleRigidBody.transform.rotation = rotation;
            vehicleRigidBody.centerOfMass = new Vector3(0f, rideHeight + adjustedBounds.y * ModSettings.MassCenterHeight, (ModSettings.MassCenterBias - 0.5f) * adjustedBounds.z * 0.5f);
            Vector3 squares = new Vector3(adjustedBounds.x * adjustedBounds.x, adjustedBounds.y * adjustedBounds.y, adjustedBounds.z * adjustedBounds.z);
            vehicleRigidBody.inertiaTensor = 1f / 12f * vehicleRigidBody.mass * new Vector3(squares.y + squares.z, squares.x + squares.z, squares.x + squares.y);
            vehicleRigidBody.velocity = Vector3.zero;

            vehicleCollider.size = adjustedBounds;
            vehicleCollider.center = new Vector3(0f, 0.5f * adjustedBounds.y + rideHeight, 0f);

            gameObject.GetComponent<MeshFilter>().mesh = gameObject.GetComponent<MeshFilter>().sharedMesh = vehicleMesh;
            gameObject.GetComponent<MeshRenderer>().material = gameObject.GetComponent<MeshRenderer>().sharedMaterial = this.vehicleInfo.m_material;
            gameObject.GetComponent<MeshRenderer>().sortingLayerID = this.vehicleInfo.m_prefabDataLayer;

            if (this.setColor)
            {
                MaterialPropertyBlock materialBlock = Singleton<VehicleManager>.instance.m_materialBlock;
                materialBlock.Clear();
                materialBlock.SetColor(Singleton<VehicleManager>.instance.ID_Color, this.vehicleColor);
                gameObject.GetComponent<MeshRenderer>().SetPropertyBlock(materialBlock);
            }

            tangent = vehicleRigidBody.transform.TransformDirection(Vector3.forward);
            normal = vehicleRigidBody.transform.TransformDirection(Vector3.up);
            binormal = vehicleRigidBody.transform.TransformDirection(Vector3.right);

            gameObject.SetActive(true);

            AddEffects();
        }
        private void DestroyVehicle()
        {
            RemoveEffects();
            foreach (Wheel w in wheelObjects)
            {
                Object.DestroyImmediate(w.gameObject);
            }
            wheelObjects.Clear();
            gameObject.SetActive(false);
            vehicleRigidBody.velocity = Vector3.zero;
            vehicleRigidBody.angularVelocity = Vector3.zero;

            setColor = false;
            vehicleColor = default;
            vehicleInfo = null;
            prevPosition = Vector3.zero;
            prevVelocity = Vector3.zero;
            tangent = Vector3.zero;
            binormal = Vector3.zero;
            normal = Vector3.zero;
            lightState = Vector4.zero;
            isSirenEnabled = false;
            isLightEnabled = false;
            physicsFallback = false;
            m_gear = 0;
            m_terrainHeight = 0f;
            m_distanceTravelled = 0f;
            m_steer = 0f;
            brake = 0f;
            throttle = 0f;
            rideHeight = 0f;
            roofHeight = 0f;
            compression = 0f;
            normalImpulse = 0f;
            prevGearChange = 0f;
        }

        private void OverridePrefabs()
        {
            for (uint prefabIndex = 0; prefabIndex < PrefabCollection<VehicleInfo>.PrefabCount(); prefabIndex++)
            {
                VehicleInfo prefabVehicleInfo = PrefabCollection<VehicleInfo>.GetPrefab(prefabIndex);
                if (prefabVehicleInfo == null) continue;
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
                if (prefabNetInfo == null) continue;
                NetInfo prefabReplaceInfo = null;

                if (prefabNetInfo.m_class.m_layer == ItemClass.Layer.MetroTunnels)
                {
                    string replaceName;
                    // get underground to elvated mapping.
                    if (!customUndergroundMappings.TryGetValue(prefabNetInfo.name, out replaceName))
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
                        backupPrefabData[prefabNetInfo] = new NetInfoBackup(prefabNetInfo.m_nodes, prefabNetInfo.m_segments);

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
                    backupPrefabData[prefabNetInfo] = new NetInfoBackup(prefabNetInfo.m_nodes, prefabNetInfo.m_segments);

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
            backupUndergroundMaterial = rm.m_groupLayerMaterials[MapUtils.LAYER_UNDERGROUND];
            rm.m_groupLayerMaterials[MapUtils.LAYER_UNDERGROUND] = rm.m_groupLayerMaterials[MapUtils.LAYER_ROAD];
            rm.UpdateGroups(MapUtils.LAYER_UNDERGROUND);
        }

        private void RestorePrefabs()
        {
            // delete all underground vehicle materials. Cities will auto generate new ones.
            for (uint iter = 0; iter < PrefabCollection<VehicleInfo>.PrefabCount(); iter++)
            {
                VehicleInfo prefabVehicleInfo = PrefabCollection<VehicleInfo>.GetPrefab(iter);
                if (prefabVehicleInfo == null) continue;
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
                if (prefabNetInfo == null) continue;
                if (prefabNetInfo.m_class.m_layer == ItemClass.Layer.MetroTunnels || prefabNetInfo.name.Contains("Slope"))
                {
                    if (backupPrefabData.TryGetValue(prefabNetInfo, out NetInfoBackup backupData))
                    {
                        prefabNetInfo.m_segments = backupData.segments;
                        prefabNetInfo.m_nodes = backupData.nodes;

                        backupPrefabData.Remove(prefabNetInfo);
                    }
                }
            }

            backupPrefabData.Clear();

            // restore the distant LOD material for underground and update LOD render groups.
            RenderManager rm = Singleton<RenderManager>.instance;
            rm.m_groupLayerMaterials[MapUtils.LAYER_UNDERGROUND] = backupUndergroundMaterial;
            backupUndergroundMaterial = null;
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
            Vector3 lateral = vehicleRigidBody.transform.TransformDirection(Vector3.right);
            Vector3 forward = vehicleRigidBody.transform.TransformDirection(Vector3.forward);

            int slopeMode = onGround ? 2 : 1;

            if (slopeMode == 2)
            {
                tangent = vehiclePos - prevPosition;
                if (Vector3.Dot(tangent, forward) < 0f)
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
                if (Vector3.Dot(tangent, forward) < 0f)
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

        private void HandleInputOnFixedUpdate(int invert)
        {
            bool throttling = false;
            bool braking = false;
            if (FPCModSettings.Instance.XMLKeyMoveForward.IsPressed())
            {
                if (invert < 0)
                {
                    if (m_gear <= 0)
                    {
                        throttle = 0f;
                        brake = Mathf.Clamp(brake + Time.fixedDeltaTime / THROTTLE_RESP, 0f, 1f);
                        braking = true;
                    }
                }
                else if (throttle == 0f && Time.time > prevGearChange + GEAR_RESP && m_gear <= 0)
                {
                    m_gear++;
                    prevGearChange = Time.time;
                }

                if (m_gear > 0)
                {
                    brake = 0f;
                    throttle = Mathf.Clamp(throttle + Time.fixedDeltaTime / THROTTLE_RESP, 0f, 1f);
                    throttling = true;
                }
            }
            else if (FPCModSettings.Instance.XMLKeyMoveBackward.IsPressed())
            {
                if (invert > 0)
                {
                    if (m_gear >= 0)
                    {
                        throttle = 0f;
                        brake = Mathf.Clamp(brake + Time.fixedDeltaTime / THROTTLE_RESP, 0f, 1f);
                        braking = true;
                    }
                }
                else if (throttle == 0f && Time.time > prevGearChange + GEAR_RESP && m_gear >= 0)
                {
                    m_gear--;
                    prevGearChange = Time.time;
                }

                if (m_gear < 0)
                {
                    brake = 0f;
                    throttle = Mathf.Clamp(throttle + Time.fixedDeltaTime / THROTTLE_RESP, 0f, 1f);
                    throttling = true;
                }
            }
            else if (invert == 0 && throttle == 0f && Time.time > prevGearChange + GEAR_RESP && m_gear >= 0)
            {
                m_gear = 0;
                prevGearChange = Time.time;
                brake = 1f;
                braking = true;
            }
            if (!throttling)
            {
                throttle = Mathf.Clamp(throttle - Time.fixedDeltaTime / THROTTLE_RESP, 0f, 1f);
            }
            if (!braking)
            {
                brake = Mathf.Clamp(brake - Time.fixedDeltaTime / THROTTLE_RESP, 0f, 1f);
            }

            bool steering = false;
            float steerLimit = Mathf.Clamp(1f - STEER_DECAY * vehicleRigidBody.velocity.magnitude, 0.04f, 1f);
            if (FPCModSettings.Instance.XMLKeyMoveRight.IsPressed())
            {
                m_steer = Mathf.Clamp(m_steer + Time.fixedDeltaTime / STEER_RESP, -steerLimit, steerLimit);
                steering = true;
            }
            if (FPCModSettings.Instance.XMLKeyMoveLeft.IsPressed())
            {
                m_steer = Mathf.Clamp(m_steer - Time.fixedDeltaTime / STEER_RESP, -steerLimit, steerLimit);
                steering = true;
            }
            if (!steering)
            {
                if (m_steer > 0f)
                {
                    m_steer = Mathf.Clamp(m_steer - Time.fixedDeltaTime / STEER_RESP, 0f, steerLimit);
                }
                if (m_steer < 0f)
                {
                    m_steer = Mathf.Clamp(m_steer + Time.fixedDeltaTime / STEER_RESP, -steerLimit, 0f);
                }
            }
        }

        private void HandleInputOnUpdate()
        {
            if (Input.GetKeyDown((KeyCode)Settings.ModSettings.KeyLightToggle.Key))
                isLightEnabled = !isLightEnabled;

            if (Input.GetKeyDown((KeyCode)Settings.ModSettings.KeySirenToggle.Key))
                isSirenEnabled = !isSirenEnabled;
        }
        private void AddEffects()
        {
            if (vehicleInfo.m_effects != null)
            {
                foreach (var effect in vehicleInfo.m_effects)
                {
                    {
                        if (effect.m_effect != null)
                        {
                            if (effect.m_vehicleFlagsRequired.IsFlagSet(Vehicle.Flags.Emergency1 | Vehicle.Flags.Emergency2))
                                specialEffects.Add(effect.m_effect);
                            else
                            {
                                if (effect.m_effect is MultiEffect multiEffect)
                                {
                                    foreach (var sub in multiEffect.m_effects)
                                        if (sub.m_effect is LightEffect lightEffect)
                                            lightEffects.Add(lightEffect);
                                }
                                regularEffects.Add(effect.m_effect);
                            }
                        }
                    }
                }
            }
        }
        private void PlayEffects()
        {
            var position = vehicleRigidBody.transform.position;
            var rotation = vehicleRigidBody.transform.rotation;
            var velocity = vehicleRigidBody.velocity;
            var acceleration = ((velocity - prevVelocity) / Time.fixedDeltaTime).magnitude;
            var swayPosition = Vector3.zero;
            var scale = Vector3.one;
            var matrix = vehicleInfo.m_vehicleAI.CalculateBodyMatrix(Vehicle.Flags.Created | Vehicle.Flags.Spawned, ref position, ref rotation, ref scale, ref swayPosition);
            var area = new EffectInfo.SpawnArea(matrix, vehicleInfo.m_lodMeshData);
            var listenerInfo = Singleton<AudioManager>.instance.CurrentListenerInfo;
            var audioGroup = Singleton<VehicleManager>.instance.m_audioGroup;
            RenderGroup.MeshData effectMeshData = vehicleInfo.m_vehicleAI.GetEffectMeshData();
            var area2 = new EffectInfo.SpawnArea(matrix, effectMeshData, vehicleInfo.m_generatedInfo.m_tyres, vehicleInfo.m_lightPositions);

            foreach (var regularEffect in regularEffects)
            {
                regularEffect.PlayEffect(default, area, velocity, acceleration, 1f, listenerInfo, audioGroup);
            }
            if (isLightEnabled)
                foreach (var light in lightEffects)
                {
                    light.RenderEffect(default, area2, velocity, acceleration, 1f, -1f, Singleton<SimulationManager>.instance.m_simulationTimeDelta, Singleton<RenderManager>.instance.CurrentCameraInfo);
                }

            if (isSirenEnabled)
                foreach (var specialEffect in specialEffects)
                {
                    specialEffect.RenderEffect(default, area2, velocity, acceleration, 1f, -1f, Singleton<SimulationManager>.instance.m_simulationTimeDelta, Singleton<RenderManager>.instance.CurrentCameraInfo);
                    specialEffect.PlayEffect(default, area, velocity, acceleration, 1f, listenerInfo, audioGroup);
                }
        }

        private void RemoveEffects()
        {
            lightEffects.Clear();
            regularEffects.Clear();
            specialEffects.Clear();
        }
    }
}