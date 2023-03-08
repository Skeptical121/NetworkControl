using System.Collections;
using System.Collections.Generic;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Rendering;
using UnityEngine;

public struct Pusher : IComponentData
{
	public const float epsilon = 0.0001f;

	public ResourceInfo resInfo;
	public float progress;
	// public Entity fromBelt;

	public float maxPushDistance;
	// public Entity toBelt;

	public bool IsWaitingAtFrom()
	{
		return progress == 0 && resInfo.IsInvalid();
	}

	public bool IsBlockingFrom()
	{
		return progress <= 1f - GetBlocking() && !resInfo.IsInvalid();
	}

	public bool IsBlockingTo()
	{
		return progress > GetBlocking() && !resInfo.IsInvalid();
	}

	public float GetBlocking()
	{
		return 0.6f;
	}
}

[UpdateInGroup(typeof(MainSimSystemGroup))]
public class PusherSystem : JobComponentSystem
{
	protected override void OnCreate()
	{
	}

	[BurstCompile]
	struct PusherTick : IJobForEach<Pusher>
	{
		public float tickTime;
		[NativeDisableParallelForRestriction] public ComponentDataFromEntity<BeltObjectInterp> beltInterp;

		public void Execute(ref Pusher pusher)
		{
			float speed = 1f;
			if (!pusher.resInfo.IsInvalid())
			{
				pusher.progress += speed * tickTime;
				if (pusher.progress >= pusher.maxPushDistance)
				{
					pusher.progress = pusher.maxPushDistance;
				}
				BeltObjectRenderSystem.SetInterp(beltInterp, pusher.progress, pusher.resInfo);
			}
			else if (pusher.progress > 0)
			{
				pusher.progress -= speed * tickTime;
				if (pusher.progress <= 0f)
				{
					pusher.progress = 0;
				}
			}
		}
	}

	protected override JobHandle OnUpdate(JobHandle inputDeps)
	{
		// inputDeps = new ProducerTick().Schedule(this, inputDeps);

		inputDeps = new PusherTick
		{
			tickTime = Game.GetTickTime(),
			beltInterp = GetComponentDataFromEntity<BeltObjectInterp>()
		}.Schedule(this, inputDeps);

		return inputDeps;
	}

	protected override void OnDestroy()
	{
		base.OnDestroy();
	}


}
