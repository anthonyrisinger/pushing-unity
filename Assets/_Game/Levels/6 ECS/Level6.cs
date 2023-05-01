using System.Runtime.InteropServices;

using Unity.Burst;
using Unity.Entities;
using Unity.Transforms;
using Unity.Mathematics;

using UnityEngine;

public class Level6 : MonoBehaviour
{
    SystemHandle _system;

    World _world;

    System.Collections.IEnumerator Start()
    {
        SceneTools.Instance.SetCountText(SceneTools.GetCount);
        SceneTools.Instance.SetNameText("ECS + Jobs + Burst");

        yield return new WaitForSeconds(0.5f);

        _world = World.DefaultGameObjectInjectionWorld;
        _system = _world.CreateSystem<SpawnerSystem>();
    }

    void Update()
    {
        if (_world == null) return;
        _system.Update(_world.Unmanaged);
    }

    void OnDestroy()
    {
        _world?.DestroySystem(_system);
    }
}

public struct SpawnerPosition : IComponentData
{
    public float3 Value;
}

[BurstCompile]
[DisableAutoCreation]
public partial struct SpawnerSystem : ISystem
{
    public void OnCreate(ref SystemState state)
    {
        var prefab = SystemAPI.GetSingleton<Spawner>().Prefab;
        var scale = SystemAPI.GetComponent<LocalTransform>(prefab).Scale;
        var buffer = SystemAPI.GetSingleton<BeginSimulationEntityCommandBufferSystem.Singleton>().CreateCommandBuffer(state.WorldUnmanaged);
        SceneTools.LoopPositions((_, pos) =>
        {
            var entity = buffer.Instantiate(prefab);
            buffer.AddComponent(entity, new SpawnerPosition { Value = pos.y });
            buffer.SetComponent(entity, LocalTransform.FromPosition(pos).ApplyScale(scale));
        });
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state) =>
        new SpawnerJob { Moment = (float)SystemAPI.Time.ElapsedTime }.ScheduleParallel(state.Dependency).Complete();
}

[BurstCompile]
[StructLayout(LayoutKind.Auto)]
public partial struct SpawnerJob : IJobEntity
{
    public float Moment;

    [BurstCompile]
    void Execute(SpawnerAspect aspect) => aspect.UpdateLocalTransformFromSpawnerPosition(Moment);
}

public readonly partial struct SpawnerAspect : IAspect
{
    readonly RefRW<LocalTransform> _localTransform;

    readonly RefRO<SpawnerPosition> _targetPosition;

    public void UpdateLocalTransformFromSpawnerPosition(float moment) =>
        (_localTransform.ValueRW.Position, _localTransform.ValueRW.Rotation) =
            _localTransform.ValueRW.Position.CalculatePosBurst(_targetPosition.ValueRO.Value.y, moment);
}
