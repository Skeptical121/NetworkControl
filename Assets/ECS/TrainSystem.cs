using System.Collections;
using System.Collections.Generic;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using UnityEngine;

public struct TrainPos
{
	public Entity rail;
	public float pos;
}

public struct Train : IComponentData
{
	public TrainPos claimed;
	public float minDistanceTravelledLastTick;
	// public TrainPos front; <-- doesn't need to exist, it would seem. (it's more like it shouldn't exist, because any cases where you try to use this seem to result in potential problems)
	public TrainPos back;

	public float length;
	public float speed;

	// Constants:
	public float maxSpeed;
	public float acceleration;
	public float deceleration;
}

[InternalBufferCapacity(0)]
public struct TrainRoute : IBufferElementData
{
	public Entity rail;
}

[InternalBufferCapacity(2)]
public struct NextRailSection : IBufferElementData
{
	public Entity rail;
}

// For deletion
[InternalBufferCapacity(0)]
public struct PreviousRailSection : IBufferElementData
{
	public Entity rail;
}

// This is very based on how Factorio does it, I believe

// Rails are divided up into sections that are smaller than the minimum space between trains.
public struct RailSection : IComponentData
{
	public const float DEFAULT_LENGTH = 0.1f;
	public float distance;
	public Entity railZone; // Can be multiple rail sections per rail zone, of course
}

public struct RailZone : IComponentData
{
	public byte numTaken; // ref count, essentially... but could just be 0 / 1
}

[InternalBufferCapacity(0)]
public struct RailZoneRailSection : IBufferElementData
{
	public Entity rail;
}

public class TrainSystem : JobComponentSystem
{
	[BurstCompile]
	struct MoveTrains : IJobForEach_BC<TrainRoute, Train>
	{
		public float tickTime;
		public ArchetypeChunkComponentType<Train> train;

		public ComponentDataFromEntity<RailSection> railSection;
		public ComponentDataFromEntity<RailZone> railZone;
		[ReadOnly] public BufferFromEntity<NextRailSection> nextRail;

		public void Execute([ReadOnly] DynamicBuffer<TrainRoute> trainRoute, ref Train train)
		{


			// So full deceleration distance is defined, ideally, completely accurately...

			// v^2 / 2a is the distance
			// This value is what it was last tick, as to indicate that all these sections have already been claimed as taken


			// Try to accelerate
			train.speed += train.acceleration * tickTime;
			if (train.speed >= train.maxSpeed)
			{
				train.speed = train.maxSpeed;
			}

			float minDistanceTravelled = train.speed * tickTime + train.speed * train.speed / (2 * train.deceleration);
			if (minDistanceTravelled > train.minDistanceTravelledLastTick) // This is important
			{
				float extra = minDistanceTravelled - train.minDistanceTravelledLastTick;
				float newExtra = AddDistance(trainRoute, ref train.claimed, extra, true, false);
				train.minDistanceTravelledLastTick += newExtra;
				if (newExtra < extra)
				{
					// Decelerate...
					train.speed -= (train.acceleration + train.deceleration) * tickTime;
					if (train.speed <= 0)
					{
						train.speed = 0;
					}
				}
			}

			// Actual move: (Both the back & front have to move here, so front moves twice)
			AddDistance(trainRoute, ref train.back, train.speed * tickTime, false, true);
			// AddDistance(trainRoute, ref train.front, train.speed * tickTime, false, false);
			// AddDistance(trainRoute, ref train.willHitAtMaxDecelerationToTravel, train.speed * tickTime, true, false); // hmm it needs to be tick perfect

		}

		// If you modify it, you're either claiming the rail section or you are 
		public float AddDistance(DynamicBuffer<TrainRoute> trainRoute, ref TrainPos trainPos, float maxExtra, bool takeAhead, bool removeBehind)
		{
			for (int i = 0; i < trainRoute.Length; i++)
			{
				if (trainPos.rail == trainRoute[i].rail)
				{
					trainPos.pos += maxExtra;
					while (trainPos.pos > railSection[trainPos.rail].distance)
					{
						trainPos.pos -= railSection[trainPos.rail].distance;
						if (removeBehind)
						{
							RailZone rz = railZone[railSection[trainPos.rail].railZone];
							rz.numTaken--;
							railZone[railSection[trainPos.rail].railZone] = rz;
						}
						if (takeAhead && railZone[railSection[trainRoute[i + 1].rail].railZone].numTaken >= 1)
						{
							maxExtra -= (trainPos.pos - railSection[trainPos.rail].distance);
							trainPos.pos = railSection[trainPos.rail].distance;
							return maxExtra;
						}
						trainPos.rail = trainRoute[++i].rail; // Should we allow for trains to leave the tracks? (or at least not crash the game for it?)
						if (takeAhead)
						{
							RailZone rz = railZone[railSection[trainPos.rail].railZone];
							rz.numTaken++;
							railZone[railSection[trainPos.rail].railZone] = rz;
						}
					}
					break;
				}
			}
			return maxExtra;
		}
	}


	protected override JobHandle OnUpdate(JobHandle inputDeps)
	{

		
		return inputDeps;
		// throw new System.NotImplementedException();
	}
}
