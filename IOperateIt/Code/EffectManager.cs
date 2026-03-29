using ColossalFramework;
using System.Collections.Generic;
using UnityEngine;

namespace IOperateIt;

public class EffectManager
{
    public bool IsSirenEnabled { get; set; }
    public bool IsLightEnabled { get; set; }
    public bool IsDusty { get; set; }

    private Rigidbody rigidBody;
    private VehicleInfo info;

    private List<LightEffect> lightEffects = [];
    private List<EffectInfo> regularEffects = [];
    private List<EffectInfo> specialEffects = [];
    private List<EffectInfo> dustEffects = [];
    public void AddEffects(Rigidbody vehicleRigidBody, VehicleInfo vehicleInfo)
    {
        rigidBody ??= vehicleRigidBody;
        info ??= vehicleInfo;

        if (info.m_effects != null)
        {
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
    }
    public void PlayEffects(Vector3 prevVelocity)
    {
        var position = rigidBody.transform.position;
        var rotation = rigidBody.transform.rotation;
        var velocity = rigidBody.velocity;
        var acceleration = ((velocity - prevVelocity) / Time.fixedDeltaTime).magnitude;
        var swayPosition = Vector3.zero;
        var scale = Vector3.one;
        var matrix = info.m_vehicleAI.CalculateBodyMatrix(Vehicle.Flags.Created | Vehicle.Flags.Spawned, ref position, ref rotation, ref scale, ref swayPosition);
        var area = new EffectInfo.SpawnArea(matrix, info.m_lodMeshData);
        var listenerInfo = Singleton<AudioManager>.instance.CurrentListenerInfo;
        var audioGroup = Singleton<AudioManager>.instance.DefaultGroup;
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
        lightEffects.Clear();
        regularEffects.Clear();
        dustEffects.Clear();
        specialEffects.Clear();
    }
}
