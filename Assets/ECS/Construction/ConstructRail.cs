using System.Collections;
using System.Collections.Generic;
using Unity.Entities;
using UnityEngine;

public struct ConstructRail : IComponentData
{
	public PFNode from;
	public PFNode to;
	public BotEntity builder;
}

[InternalBufferCapacity(0)]
public struct RailParent : IBufferElementData
{
	public Entity rail;
}

public class RailConstructor : Constructor<ConstructRail>
{
	private EntityArchetype railArchetype;
	private EntityArchetype railZone;

	public override void InitArchetype()
	{
		components = new ComponentTypes(
			typeof(RailParent),
			typeof(Health));
		railArchetype = EntityManager.CreateArchetype(
			typeof(PreviousRailSection),
			typeof(NextRailSection),
			typeof(RailSection),
			typeof(SubMeshRenderer));
		railZone = EntityManager.CreateArchetype(
			typeof(RailZone),
			typeof(RailZoneRailSection));
	}

	protected override float GetConstructionCost()
	{
		return 3f;
	}

	protected override List<PFTile> GetTilesTaken(ConstructRail constructComponent)
	{
		throw new System.NotImplementedException();
	}

	protected override void InitRender(Entity entity, bool facadeOrConstructing)
	{
		throw new System.NotImplementedException();
	}

	protected override void OnConstructed(Entity entity, ConstructRail constructComponent)
	{
		/*RailSection railSection = new RailSection { distance = 0, railZone = hmm };
		entity.SetData(beltSection);

		BezierCurve.GetRep(
			CreateConveyorBelt(beltSection.from, beltSection.to),
			GetCurvePrecision(Game.map.ConnectionNormal(beltSection.from), Game.map.ConnectionNormal(beltSection.to)),
			out NativeArray<BezierCurveNetDistance> netDistance);

		float sectionDist = netDistance[netDistance.Length - 1].dist;

		Entity beltParent = EntityManager.CreateEntity(beltParentArchetype);
		beltParent.SetData(new BeltParent { dist = sectionDist, input = beltSection.from, output = beltSection.to });
		beltParent.Buffer<BezierCurveNetDistance>().AddRange(netDistance);
		beltParent.Buffer<BeltPath>().Add(new BeltPath { beltSection = entity, dist = 0, curveIndexStart = 0 });
		beltSection.parentBelt = beltParent;
		entity.SetData(beltSection);

		Entity fromEntity = Game.map.GetEntity(beltSection.from.tile);
		if (fromEntity.Has<BeltSection>() &&
			fromEntity.Get<BeltSection>().to.Equals(beltSection.from))
		{
			beltParent = Merge(fromEntity.Get<BeltSection>().parentBelt, beltParent);
		}

		Entity toEntity = Game.map.GetEntity(Game.map.GetToTile(beltSection.to));
		if (toEntity.Has<BeltSection>() &&
			toEntity.Get<BeltSection>().from.Equals(beltSection.to))
		{
			beltParent = Merge(beltParent, toEntity.Get<BeltSection>().parentBelt);
		}
		beltSection.parentBelt = beltParent;
		entity.SetData(beltSection);*/
	}

	protected override void OnDestroy(Entity entity)
	{
		DynamicBuffer<RailParent> beltSection = entity.Buffer<RailParent>();

		for (int i = 0; i < beltSection.Length; i++)
		{
			var previousRails = beltSection[i].rail.Buffer<PreviousRailSection>();
			for (int r = 0; r < previousRails.Length; r++)
			{
				var previousNextRails = previousRails[r].rail.Buffer<NextRailSection>();
				for (int t = 0; t < previousNextRails.Length; t++)
				{
					if (previousNextRails[t].rail == beltSection[i].rail)
					{
						previousNextRails.RemoveAt(t);
						break;
					}
				}
			}
			var nextRails = beltSection[i].rail.Buffer<NextRailSection>();
			for (int r = 0; r < nextRails.Length; r++)
			{
				var nextPreviousRails = nextRails[r].rail.Buffer<PreviousRailSection>();
				for (int t = 0; t < nextPreviousRails.Length; t++)
				{
					if (nextPreviousRails[t].rail == beltSection[i].rail)
					{
						nextPreviousRails.RemoveAt(t);
						break;
					}
				}
			}
			RailSection railSection = beltSection[i].rail.Get<RailSection>();
			
			var railsInZone = railSection.railZone.Buffer<RailZoneRailSection>();
			for (int t = 0; t < railsInZone.Length; t++)
			{
				if (railsInZone[t].rail == beltSection[i].rail)
				{
					railsInZone.RemoveAt(t);
					break;
				}
			}
			// A train was here or is going to run into here no matter what...
			// Option 1 is to just let the train go do its route... but I think a better solution will be needed
			// railSection.railZone
		}
	}
}
