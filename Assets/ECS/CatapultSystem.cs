using System.Collections;
using System.Collections.Generic;
using Unity.Entities;
using Unity.Jobs;
using UnityEngine;

// Catapults can move...
public struct Movable : IComponentData
{
	public PFTile from;
	public PFTile to;
	public float moveSpeed;
	public float dist;
	public float progress;
}

public struct Route : IBufferElementData
{
	public PFTile goal;
}

// For now catapults instantly shoot?
public struct Catapult : IComponentData
{
	public float lastTimeFiredAt;
	public ResourceInfo ncoHeld;
	public PFTile target;
}

public struct Wall : IComponentData
{

}

public class CatapultSystem : JobComponentSystem
{
	struct MoveMovableTick : IJobForEach_BC<Route, Movable>
	{
		public float tickTime;
		public void Execute(DynamicBuffer<Route> route, ref Movable movable)
		{
			if (!movable.from.Equals(movable.to))
			{
				movable.progress += movable.moveSpeed * tickTime;
				if (movable.progress >= movable.dist)
				{
					movable.from = movable.to;
					movable.progress = 0;
					if (route.Length > 0)
					{
						movable.to = route[0].goal;
						route.RemoveAt(0);
					}
					else
					{
						movable.dist = 0;
					}
				}
			}
		}
	}

	struct LoadCatapultTick : IJobForEach<Catapult, Movable>
	{
		public void Execute(ref Catapult catapult, ref Movable movable)
		{
			throw new System.NotImplementedException();
		}
	}

	protected override JobHandle OnUpdate(JobHandle inputDeps)
	{
		return inputDeps;
	}
}
