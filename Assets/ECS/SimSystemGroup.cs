using System.Collections;
using System.Collections.Generic;
using Unity.Burst;
using Unity.Entities;
using Unity.Jobs;
using UnityEngine;
using UnityEngine.Experimental.PlayerLoop;


public abstract class JobComponentSystemWithCallback : JobComponentSystem
{
	private EndMainSimEntityCommandBufferSystem barrier;
	protected override void OnCreate()
	{
		barrier = World.GetOrCreateSystem<EndMainSimEntityCommandBufferSystem>();
		base.OnCreate();
	}
	public abstract void MainThreadSimulationCallbackTick();

	protected override JobHandle OnUpdate(JobHandle inputDeps)
	{
		barrier.AddJobHandleForProducer(inputDeps);
		return inputDeps;
	}
}

public class SimSystemGroup : ComponentSystemGroup
{
    
}

[UpdateInGroup(typeof(SimSystemGroup))]
public class MainSimSystemGroup : ComponentSystemGroup
{

}

[UpdateInGroup(typeof(PresentationSystemGroup))]
public class RenderSystemGroup : ComponentSystemGroup
{

}

[UpdateInGroup(typeof(SimSystemGroup))]
[UpdateAfter(typeof(MainSimSystemGroup))]
public class EndMainSimEntityCommandBufferSystem : EntityCommandBufferSystem
{

}