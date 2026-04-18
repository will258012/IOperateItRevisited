using ColossalFramework;
using System.Collections.Generic;
using UnityEngine;

namespace IOperateIt.Managers;

public class EffectManager
{
    public bool IsSirenEnabled { get; set; }
    public bool IsLightEnabled { get; set; }
    public bool IsDusty { get; set; }

    private const int MAX_CONCURRENT_SOUNDS = 18;
    private const float LIGHT_TAILLIGHT_INTENSITY = 2f;
    private const float LIGHT_TAILLIGHT_IDLE_INTENSITY = 1f;
    private const float LIGHT_TAILLIGHT_RANGE = 10f;
    private const float LIGHT_TEXTURE_INTENSITY = 5.0f;
    private const float LIGHT_TEXTURE_IDLE_INTENSITY = 0.5f;

    private Rigidbody rigidBody;
    private VehicleInfo info;

    private List<LightEffect> lightEffects = [];
    private List<EffectInfo> regularEffects = [];
    private List<EffectInfo> specialEffects = [];
    private List<EffectInfo> dustEffects = [];

    private GameObject taillightObject;
    private Light taillight;
    private AudioGroup audioGroup = new AudioGroup(MAX_CONCURRENT_SOUNDS, new SavedFloat(global::Settings.effectAudioVolume, global::Settings.gameSettingsFile, DefaultSettings.effectAudioVolume, true));

    private Vector4 lightState = Vector4.zero;

    public void AddEffects(Rigidbody vehicleRigidBody, VehicleInfo vehicleInfo)
    {
        rigidBody = vehicleRigidBody;
        info = vehicleInfo;

        if (info.m_effects == null)
            return;
        if (info.m_lightPositions?.Length > 0)
            AddTaillight();

        foreach (var effect in info.m_effects)
        {
            {
                if (effect.m_effect == null)
                {
                    continue;
                }
                if (effect.m_vehicleFlagsRequired.IsFlagSet(Vehicle.Flags.Emergency1 | Vehicle.Flags.Emergency2))
                    specialEffects.Add(effect.m_effect);
                else if (effect.m_vehicleFlagsRequired.IsFlagSet(Vehicle.Flags.OnGravel))
                {
                    dustEffects.Add(effect.m_effect);
                }
                else if (effect.m_effect is MultiEffect multiEffect)
                {
                    foreach (var sub in multiEffect.m_effects)
                    {
                        if (sub.m_effect is LightEffect lightEffect)
                        {
                            lightEffects.Add(lightEffect);
                        }
                        else
                        {
                            regularEffects.Add(effect.m_effect);
                        }
                    }
                }
                else
                {
                    regularEffects.Add(effect.m_effect);
                }
            }
        }
    }
    private void AddTaillight()
    {
        taillightObject = new GameObject("Taillight");
        taillight = taillightObject.AddComponent<Light>();
        taillight.type = LightType.Point;
        taillight.intensity = LIGHT_TAILLIGHT_IDLE_INTENSITY;
        taillight.range = LIGHT_TAILLIGHT_RANGE;
        taillight.transform.SetParent(rigidBody.transform, false);
        taillight.color = Color.red;
        taillight.enabled = false;

        var vehicleMesh = info.m_mesh;
        var fullBounds = vehicleMesh.bounds.size;
        taillight.transform.localPosition = new Vector3(0.0f, fullBounds.y * .25f, -fullBounds.z * 0.5f - DriveController.FLOAT_ERROR);

    }
    public void UpdateLights(MaterialPropertyBlock materialBlock)
    {
        var brake = DriveController.Instance.Brake;

        lightState.x = IsLightEnabled ? LIGHT_TEXTURE_INTENSITY : 0.0f;
        lightState.y = brake > 0.0f ? LIGHT_TEXTURE_INTENSITY : (IsLightEnabled ? LIGHT_TEXTURE_IDLE_INTENSITY : 0.0f);
        materialBlock.SetVector(VehicleManager.instance.ID_LightState, lightState);

        float tailIntensity = IsLightEnabled ? LIGHT_TEXTURE_IDLE_INTENSITY : 0.0f;
        tailIntensity = brake > 0.0f ? LIGHT_TAILLIGHT_INTENSITY : tailIntensity;
        if (tailIntensity > 0.0f)
        {
            taillight.intensity = tailIntensity;
            taillight.enabled = true;
        }
        else
        {
            taillight.enabled = false;
        }
    }
    public void PlayEffects()
    {
        var position = rigidBody.transform.position;
        var rotation = rigidBody.transform.rotation;
        var velocity = rigidBody.velocity;
        var acceleration = ((velocity - DriveController.Instance.PrevVelocity) / Time.fixedDeltaTime).magnitude;
        var scale = Vector3.one;
        var matrix = Matrix4x4.TRS(position, rotation, scale);
        var area = new EffectInfo.SpawnArea(matrix, info.m_lodMeshData);
        var listenerInfo = AudioManager.instance.CurrentListenerInfo;
        audioGroup.UpdatePlayers(listenerInfo, AudioManager.instance.MasterVolume);

        RenderGroup.MeshData effectMeshData = info.m_vehicleAI.GetEffectMeshData();
        var area2 = new EffectInfo.SpawnArea(matrix, effectMeshData, info.m_generatedInfo.m_tyres, info.m_lightPositions);

        foreach (var regularEffect in regularEffects)
        {
            regularEffect.PlayEffect(default, area, velocity, acceleration, 1f, listenerInfo, audioGroup);
        }
        if (IsLightEnabled)
        {
            foreach (var light in lightEffects)
            {
                light.RenderEffect(default, area2, velocity, acceleration, 1f, -1f, Singleton<SimulationManager>.instance.m_simulationTimeDelta, Singleton<RenderManager>.instance.CurrentCameraInfo);
            }
        }

        if (IsSirenEnabled)
        {
            foreach (var specialEffect in specialEffects)
            {
                specialEffect.RenderEffect(default, area2, velocity, acceleration, 1f, -1f, Singleton<SimulationManager>.instance.m_simulationTimeDelta, Singleton<RenderManager>.instance.CurrentCameraInfo);
                specialEffect.PlayEffect(default, area, velocity, acceleration, 1f, listenerInfo, audioGroup);
            }
        }
        if (IsDusty)
        {
            foreach (var dustEffect in dustEffects)
            {
                dustEffect.RenderEffect(default, area2, velocity, acceleration, 1f, -1f, Singleton<SimulationManager>.instance.m_simulationTimeDelta, Singleton<RenderManager>.instance.CurrentCameraInfo);
            }
        }
    }

    public void RemoveEffects()
    {
        IsSirenEnabled = IsLightEnabled = IsDusty = false;

        lightEffects.Clear();
        regularEffects.Clear();
        dustEffects.Clear();
        specialEffects.Clear();

        audioGroup.Reset();

        Object.Destroy(taillightObject);
    }
}
