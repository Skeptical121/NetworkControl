using System.Collections;
using System.Collections.Generic;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Rendering;
using UnityEngine;


[InternalBufferCapacity(2)]
public struct NCOElement : IBufferElementData
{
	public BotEntity owner;
	public float nco;
}

public struct BuildingInfo : IComponentData
{
	public BotEntity owner;
	// public float lastPercentageValue; // Iterated by a number like 0.383

	public float GetTax(float tickTime)
	{
		return 1f * tickTime;
	}
}

public struct Barracks : IComponentData
{
	// Just builds catapults for now...
	public float progress;
}

// The sole purpose of the energy tower is to claim land...
// You still need to pipe a conveyor belt or something to it
public struct EnergyTower : IComponentData
{
	public int radius;
}

[UpdateInGroup(typeof(MainSimSystemGroup))]
public class BuildingSystem : JobComponentSystemWithCallback
{
	// private EndMainSimEntityCommandBufferSystem barrier;
	// private EntityQuery ownerDisplay;
	// private EntityQuery outputRail;
	private NativeList<UpdatedOwner> updatedOwners;
	public NativeList<Entity> buildings;
	protected override void OnCreate()
	{
		World.Active.GetOrCreateSystem<Unity.Physics.Systems.BuildPhysicsWorld>();
		// barrier = World.GetOrCreateSystem<EndMainSimEntityCommandBufferSystem>();
		// ownerDisplay = GetEntityQuery(ComponentType.ReadOnly<NewOwner>(), typeof(NCOContainer), typeof(RenderMesh));
		// ownerDisplay.SetFilterChanged(typeof(NewOwner));
		// outputRail = GetEntityQuery(ComponentType.ReadOnly<OutRail>(), typeof(NCOContainer));
		updatedOwners = new NativeList<UpdatedOwner>(Allocator.Persistent);
		buildings = new NativeList<Entity>(Allocator.Persistent);
		base.OnCreate();
	}
	/*
	[BurstCompile]
	struct OutputToRailTick : IJobForEachWithEntity_EBC<OutRail, BuildingInfo>
	{
		// There is, by definition, only 1 building outputing to any given conveyor belt
		[NativeDisableParallelForRestriction]
		public BufferFromEntity<BeltObject> beltObjectsFromEntity;

		[NativeDisableParallelForRestriction]
		public BufferFromEntity<BeltObject> beltObjectsFromEntity;
		public float worldTime;

		public void Execute(Entity building, int index, [ReadOnly] DynamicBuffer<OutRail> outRails, ref BuildingInfo buildingInfo)
		{
			if ((int)worldTime % 3 == 0 && buildingInfo.owner.id != Bot.INVALID)
			{
				for (int r = 0; r < outRails.Length; r++)
				{
					Entity outRail = outRails[r].rail;
					DynamicBuffer<BeltObject> beltObjects = beltObjectsFromEntity[outRail];

					float length = ResourceInfo.STANDARD_LENGTH;
					if (beltObjects.Length == 0 || beltObjects[beltObjects.Length - 1].pos >= 0)
					{
						beltObjects.Add(new BeltObject
						{
							markerRef = Entity.Null,
							pos = -length,
							resInfo = new ResourceInfo(buildingInfo.owner, worldTime, building, Entity.Null, length)
						});*/
	/*ncoInfo.lastPercentageValue += 0.383f;
	if (ncoInfo.lastPercentageValue >= 1)
	{
		ncoInfo.lastPercentageValue -= 1;
	}
	float value = ncoInfo.totalNCO * ncoInfo.lastPercentageValue;
	float total = 0;
	for (int n = 0; n < ncoElements.Length; n++)
	{
		total += ncoElements[n].nco;
		if (value <= total)
		{
			beltObjects.Add(new BeltObject
			{
				markerRef = Entity.Null,
				pos = -length,
				resInfo = new ResourceInfo(ncoElements[n].ownerID, worldTime, building, Entity.Null, length)
			});
			break;
		}
	}*/
	/*}
}
}
}
}*/

	[BurstCompile]
	public struct NCODecay : IJobForEach_BC<NCOElement, BuildingInfo>
	{
		public float decayMultiplier;
		public void Execute(DynamicBuffer<NCOElement> ncoContainer, ref BuildingInfo buildingInfo)
		{
			for (int i = 0; i < ncoContainer.Length; i++)
			{
				if (ncoContainer[i].nco <= 0.01f)
				{
					ncoContainer.RemoveAt(i);
					i--;
				}
				else
				{
					NCOElement e = ncoContainer[i];
					e.nco *= decayMultiplier;
					ncoContainer[i] = e;
				}
			}
		}
	}

	struct UpdatedOwner
	{
		public Entity entity;
		public BotEntity oldOwner;
	}

	[BurstCompile]
	struct GetOwnerChanged : IJobForEachWithEntity_EBC<NCOElement, BuildingInfo>
	{
		public NativeList<UpdatedOwner> updatedOwners;
		public void Execute(Entity entity, int index, [ReadOnly] DynamicBuffer<NCOElement> ncoContainer, ref BuildingInfo buildingInfo)
		{
			// Calculate owner:
			float max = 0;
			BotEntity newOwner = BotEntity.Invalid;
			for (int i = 0; i < ncoContainer.Length; i++)
			{
				if (ncoContainer[i].nco > max)
				{
					max = ncoContainer[i].nco;
					newOwner = ncoContainer[i].owner;
				}
			}

			if (newOwner.id != buildingInfo.owner.id)
			{
				updatedOwners.Add(new UpdatedOwner { entity = entity, oldOwner = buildingInfo.owner });
				buildingInfo.owner = newOwner;
			}
		}
	}

	[BurstCompile]
	public struct ConstructNearbyRails : IJobForEach_BC<OutBorder, BuildingInfo>
	{
		public MapInfo map;
		public ComponentDataFromEntity<Constructing> constructingFromEntity;
		public float tickTime;
		public void Execute([ReadOnly] DynamicBuffer<OutBorder> outBorder, [ReadOnly] ref BuildingInfo buildingInfo)
		{
			for (int i = 0; i < outBorder.Length; i++)
			{
				Entity entity = map.GetEntity(map.GetToTile(outBorder[i].node));
				if (constructingFromEntity.HasComponent(entity))
				{
					Constructing c = constructingFromEntity[entity];
					c.facade = false;
					c.progress += tickTime; // Hmm
					constructingFromEntity[entity] = c;
				}
			}
		}
	}

	protected override JobHandle OnUpdate(JobHandle inputDeps)
	{
		// inputDeps = new ProducerTick().Schedule(this, inputDeps);

		/*inputDeps = new OutputToRailTick
		{
			beltObjectsFromEntity = GetBufferFromEntity<BeltObject>(false),
			worldTime = Game.GetGameTime(),

		}.Schedule(this, inputDeps);*/

		inputDeps = new NCODecay
		{
			decayMultiplier = math.pow(0.95f, Game.GetTickTime())
		}.Schedule(this, inputDeps);

		inputDeps = new ConstructNearbyRails
		{
			map = Game.map,
			constructingFromEntity = GetComponentDataFromEntity<Constructing>(),
			tickTime = Game.GetTickTime()
		}.ScheduleSingle(this, inputDeps);

		inputDeps = new GetOwnerChanged
		{
			updatedOwners = updatedOwners
		}.ScheduleSingle(this, inputDeps);

		return base.OnUpdate(inputDeps);
	}

	public override void MainThreadSimulationCallbackTick()
	{
		// The jobs are forced to be completed by this point
		for (int i = 0; i < updatedOwners.Length; i++)
		{
			// Set tiles to that owner, IF it is an energy tower...
			if (updatedOwners[i].entity.Has<EnergyTower>())
			{
				EnergyTower energyTower = updatedOwners[i].entity.Get<EnergyTower>();
				EntireBorderInfo.RadialAction(new List<PFTile> { updatedOwners[i].entity.Get<CenterTile>().tile }, energyTower.radius, 
					(tile, offset) => {
						if (updatedOwners[i].oldOwner.id != Bot.INVALID)
							Game.map.AddTileControl(tile, updatedOwners[i].oldOwner.id, -(1 + energyTower.radius - offset));
						Game.map.AddTileControl(tile, updatedOwners[i].entity.Get<BuildingInfo>().owner.id, 1 + energyTower.radius - offset); // 1 is the absolute minimum here, we could use a larger number
					});
			}



			Entity renderer = updatedOwners[i].entity.Buffer<SubMeshRenderer>()[0].renderer;
			RenderMesh rm = EntityManager.GetSharedComponentData<RenderMesh>(renderer);
			rm.material = RenderInfo.Building[updatedOwners[i].entity.Get<BuildingInfo>().owner.id];
			EntityManager.SetSharedComponentData(renderer, rm);
		}
		updatedOwners.Clear();
	}

	protected override void OnDestroy()
	{
		base.OnDestroy();
		updatedOwners.Dispose();
		buildings.Dispose();
	}


}
