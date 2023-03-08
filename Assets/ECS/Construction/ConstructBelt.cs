using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Rendering;
using UnityEngine;
using System;
using System.Collections.Generic;

public struct ConstructBelt : IComponentData
{
	public PFNode from;
	public PFNode to;
	public BotEntity builder;
}

public class BeltConstructor : Constructor<ConstructBelt>
{
	private EntityArchetype beltParentArchetype;

	public override void InitArchetype()
	{
		components = new ComponentTypes(
			typeof(BeltSection),
			typeof(Health));
		beltParentArchetype = EntityManager.CreateArchetype(
			typeof(BeltParent),
			typeof(BeltObject),
			typeof(BezierCurveNetDistance),
			typeof(SubMeshRenderer),
			typeof(BeltPath));
	}

	protected override List<PFTile> GetTilesTaken(ConstructBelt constructBelt)
	{
		NativeList<PFTile> tiles = new NativeList<PFTile>(Allocator.Temp);
		Game.map.GetNextPFNode(constructBelt.from.pfr, Game.map.GetToTile(constructBelt.from), constructBelt.to.dir, constructBelt.to.pfr, tiles);
		List<PFTile> tilesTaken = new List<PFTile>(tiles.ToArray());
		tiles.Dispose();
		return tilesTaken;
	}

	protected override void InitRender(Entity entity, bool facadeOrConstructing)
	{
		if (facadeOrConstructing)
		{
			SetRenderer(entity, true);
		}
		else
		{
			SetRenderer(entity.Get<BeltSection>().parentBelt, false);
		}
	}

	protected override void OnConstructed(Entity entity, ConstructBelt constructBelt)
	{
		BeltSection beltSection = new BeltSection { parentBelt = Entity.Null, from = constructBelt.from, to = constructBelt.to };
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
		entity.SetData(beltSection);
	}

	private float3[] CreateConveyorBelt(PFNode from, PFNode to)
	{
		return new float3[]{ Game.map.ConnectionPoint(from),
			Game.map.ConnectionPoint(from) + Game.map.ConnectionNormal(from) * MapInfo.TILE_LENGTH / 3f,
			Game.map.ConnectionPoint(to) - Game.map.ConnectionNormal(to) * MapInfo.TILE_LENGTH / 3f,
			Game.map.ConnectionPoint(to) };
	}

	private int GetCurvePrecision(float3 dir1, float3 dir2)
	{
		float extra = VectorMath.GetAngleMultiplier(dir1, dir2); // num is from 0 - 2, 0 being straight, 2 being directly backwards
		return 1 + (int)(extra * 24f); // extra = 1 when angle is 90 degrees..
	}

	protected override void OnDestroy(Entity entity)
	{
		// Kill it:
		/*if (EntityManager.HasComponent<BeltSection>(entity))
		{
			Game.map.SetTilesTakenToNull(entity, EntityManager.GetComponentData<BeltSection>(entity).to.tile);
		}
		else
		{
			Game.map.SetTilesTakenToNull(entity, EntityManager.GetComponentData<ConstructBelt>(entity).to.tile);
		}*/
		


		// if (entity.Has<BeltSection>())
		// {
		BeltSection beltSection = entity.Get<BeltSection>();
		DynamicBuffer<BeltPath> route = beltSection.parentBelt.Buffer<BeltPath>();
		int routeLength = route.Length;
		for (int i = 0; i < routeLength; i++)
		{
			if (route[i].beltSection == entity)
			{
				Entity before = Entity.Null;
				Entity after = Entity.Null;
				if (i > 0)
				{
					before = Trim(beltSection, beltSection.parentBelt, i, true);
				}

				if (i < routeLength - 1)
				{
					after = Trim(beltSection, beltSection.parentBelt, i, false);
				}
				TrimBeltObjects(beltSection.parentBelt, i, before, after);

				Delete(beltSection.parentBelt);
				break;
			}
		}
		// } // Else it should be fine...
	}

	private Entity Merge(Entity fromBelt, Entity toBelt)
	{
		// Merges into the from belt
		BeltParent fromBeltParent = fromBelt.Get<BeltParent>();
		BeltParent toBeltParent = toBelt.Get<BeltParent>();
		float fromBeltDist = fromBeltParent.dist;
		fromBeltParent.dist += toBeltParent.dist;
		fromBeltParent.output = toBeltParent.output; // No need to set input
		fromBelt.SetData(fromBeltParent);

		DynamicBuffer<BeltObject> fromBeltObjects = fromBelt.Buffer<BeltObject>();
		NativeArray<BeltObject> before = fromBeltObjects.ToNativeArray(Allocator.Temp);
		DynamicBuffer<BeltObject> toBeltObjects = toBelt.Buffer<BeltObject>();
		NativeArray<BeltObject> after = new NativeArray<BeltObject>(toBeltObjects.Length, Allocator.Temp);
		for (int i = 0; i < toBeltObjects.Length; i++)
		{
			BeltObject beltObject = toBeltObjects[i];
			beltObject.pos += fromBeltDist;
			SetRenderEntityParent(beltObject.resInfo.renderEntity, fromBelt);
			after[i] = beltObject;
		}
		fromBeltObjects.Clear();
		// Backwards order:
		fromBeltObjects.AddRange(after);
		fromBeltObjects.AddRange(before);

		// These are in order... however
		DynamicBuffer<BezierCurveNetDistance> fromNetDistance = fromBelt.Buffer<BezierCurveNetDistance>();
		int numFromCurveIndices = fromNetDistance.Length;
		NativeArray<BezierCurveNetDistance> toNetDistance = toBelt.Buffer<BezierCurveNetDistance>().ToNativeArray(Allocator.Temp);
		for (int i = 0; i < toNetDistance.Length; i++)
		{
			BezierCurveNetDistance bcnd = toNetDistance[i];
			bcnd.dist += fromBeltDist;
			toNetDistance[i] = bcnd;
		}

		fromNetDistance.AddRange(toNetDistance);


		DynamicBuffer<BeltPath> fromRoute = fromBelt.Buffer<BeltPath>();
		NativeArray<BeltPath> toRoute = toBelt.Buffer<BeltPath>().ToNativeArray(Allocator.Temp);
		for (int i = 0; i < toRoute.Length; i++)
		{
			BeltPath beltPath = toRoute[i];
			beltPath.dist += fromBeltDist;
			beltPath.curveIndexStart += numFromCurveIndices;
			toRoute[i] = beltPath;
			toRoute[i].beltSection.Modify((ref BeltSection bs) => bs.parentBelt = fromBelt);
		}
		fromRoute.AddRange(toRoute);

		// Delete toBelt:
		Delete(toBelt);
		return fromBelt;
	}

	private Entity Trim(BeltSection beltSection, Entity belt, int index, bool before)
	{
		Entity newBelt = EntityManager.CreateEntity(beltParentArchetype);

		DynamicBuffer<BeltPath> route = belt.Buffer<BeltPath>();
		BeltParent beltParent = belt.Get<BeltParent>();
		if (before)
		{
			newBelt.SetData(new BeltParent { dist = route[index].dist, input = beltParent.input, output = beltSection.from });
		}
		else
		{
			newBelt.SetData(new BeltParent { dist = beltParent.dist - route[index + 1].dist, input = beltSection.to, output = beltParent.output });
		}

		DynamicBuffer<BeltPath> newRoute = newBelt.Buffer<BeltPath>();
		for (int i = before ? 0 : index + 1; i < (before ? index : route.Length); i++)
		{
			BeltPath bp = route[i];
			if (!before)
			{
				bp.curveIndexStart -= route[index + 1].curveIndexStart;
				bp.dist -= route[index + 1].dist;
			}
			newRoute.Add(bp);
			route[i].beltSection.Modify((ref BeltSection bs) => bs.parentBelt = newBelt);
		}


		DynamicBuffer<BezierCurveNetDistance> netDistance = belt.Buffer<BezierCurveNetDistance>();
		DynamicBuffer<BezierCurveNetDistance> newNetDistance = newBelt.Buffer<BezierCurveNetDistance>();

		for (int i = before ? 0 : route[index + 1].curveIndexStart; i < (before ? route[index].curveIndexStart : netDistance.Length); i++)
		{
			if (before)
			{
				newNetDistance.Add(netDistance[i]);
			}
			else
			{
				BezierCurveNetDistance bcnd = netDistance[i];
				bcnd.dist -= route[index + 1].dist;
				newNetDistance.Add(bcnd);
			}
		}
		SetRenderer(newBelt, false);
		return newBelt;
	}

	private void SetRenderEntityParent(Entity renderEntity, Entity belt)
	{
		if (renderEntity != Entity.Null)
		{
			renderEntity.Modify((ref BeltObjectInterp boi) => boi.parent = belt);
		}
	}

	public void TrimBeltObjects(Entity belt, int index, Entity from, Entity to)
	{
		DynamicBuffer<BeltPath> route = belt.Buffer<BeltPath>();
		DynamicBuffer<BeltObject> beltObjects = belt.Buffer<BeltObject>();
		int startIndexToDestroy = 0;
		int lastIndexToDestroy = beltObjects.Length;
		if (index > 0)
		{
			lastIndexToDestroy = 0;
			DynamicBuffer<BeltObject> fromBeltObjects = from.Buffer<BeltObject>();
			for (int i = 0; i < beltObjects.Length; i++)
			{
				if (beltObjects[i].pos <= route[index].dist)
				{
					// Include this and anything past it
					fromBeltObjects.Add(beltObjects[i]);
					SetRenderEntityParent(beltObjects[i].resInfo.renderEntity, from);
				}
				else
				{
					lastIndexToDestroy = i + 1;
				}
			}
		}
		if (index < route.Length - 1)
		{
			startIndexToDestroy = beltObjects.Length;
			DynamicBuffer<BeltObject> toBeltObjects = to.Buffer<BeltObject>();
			for (int i = 0; i < beltObjects.Length; i++)
			{
				if (beltObjects[i].pos < route[index + 1].dist)
				{
					// Don't include this and anything past it
					startIndexToDestroy = i;
					break;
				}
				BeltObject beltObject = beltObjects[i];
				beltObject.pos -= route[index + 1].dist;
				toBeltObjects.Add(beltObjects[i]);
				SetRenderEntityParent(beltObjects[i].resInfo.renderEntity, to);
			}
		}
		for (int i = startIndexToDestroy; i < lastIndexToDestroy; i++)
		{
			if (belt.Buffer<BeltObject>()[i].resInfo.renderEntity != Entity.Null)
			{
				EntityManager.DestroyEntity(belt.Buffer<BeltObject>()[i].resInfo.renderEntity);
			}
		}
	}

	private void Delete(Entity parentBelt)
	{
		for (int i = 0; i < parentBelt.Buffer<SubMeshRenderer>().Length; i++)
		{
			EntityManager.DestroyEntity(parentBelt.Buffer<SubMeshRenderer>()[i].renderer);
		}
		EntityManager.DestroyEntity(parentBelt);
	}

	private void SetRenderer(Entity entity, bool facade)
	{
		for (int i = 0; i < entity.Buffer<SubMeshRenderer>().Length; i++)
		{
			EntityManager.DestroyEntity(entity.Buffer<SubMeshRenderer>()[i].renderer);
		}
		EntityManager.GetBuffer<SubMeshRenderer>(entity).Clear();

		Mesh mesh;
		Material[] materials;
		if (facade)
		{
			ConstructBelt constructBelt = entity.Get<ConstructBelt>();
			BezierCurve.GetRep(
				CreateConveyorBelt(constructBelt.from, constructBelt.to),
				GetCurvePrecision(Game.map.ConnectionNormal(constructBelt.from), Game.map.ConnectionNormal(constructBelt.to)),
				out NativeArray<BezierCurveNetDistance> netDistance);
			mesh = CreateMesh(netDistance);
			materials = new Material[]{ RenderInfo.Facade[constructBelt.builder.id], RenderInfo.Facade[constructBelt.builder.id] };
		}
		else
		{
			mesh = CreateMesh(entity.Buffer<BezierCurveNetDistance>().AsNativeArray());
			materials = new Material[] { RenderInfo.self.conveyorBeltMat, RenderInfo.Building[Bot.INVALID] };
		}

		
		for (int n = 0; n < mesh.subMeshCount; n++)
		{
			Entity renderEntity = EntityManager.CreateEntity(ConstructionSystem.subMeshRenderer);
			EntityManager.SetSharedComponentData(renderEntity, new RenderMesh { mesh = mesh, material = materials[n], subMesh = n });
			entity.Buffer<SubMeshRenderer>().Add(new SubMeshRenderer { renderer = renderEntity });
		}
	}

	private Mesh CreateMesh(NativeArray<BezierCurveNetDistance> points)
	{
		Vector2[] offsets = { new Vector2(-MapInfo.TILE_LENGTH * 0.3f, 0), new Vector2(MapInfo.TILE_LENGTH * 0.3f, 0) };
		float[] uv = { 0f, 1f };
		Vector2[] normals = { new Vector2(0, 1) };
		Mesh mesh1 = BezierCurve.CreateRenderMesh(points, offsets, uv, new float3(0, 1, 0), false, true, normals);

		offsets = new Vector2[] {
			new Vector2(MapInfo.TILE_LENGTH * 0.3f, 0),
			new Vector2(MapInfo.TILE_LENGTH * 0.3f, MapInfo.TILE_LENGTH * 0.1f),
			new Vector2(MapInfo.TILE_LENGTH * 0.35f, MapInfo.TILE_LENGTH * 0.1f),
			new Vector2(MapInfo.TILE_LENGTH * 0.35f, -MapInfo.TILE_LENGTH * 0.1f),
			new Vector2(-MapInfo.TILE_LENGTH * 0.35f, -MapInfo.TILE_LENGTH * 0.1f),
			new Vector2(-MapInfo.TILE_LENGTH * 0.35f, MapInfo.TILE_LENGTH * 0.1f),
			new Vector2(-MapInfo.TILE_LENGTH * 0.3f, MapInfo.TILE_LENGTH * 0.1f),
			new Vector2(-MapInfo.TILE_LENGTH * 0.3f, 0) };
		uv = new float[] { 0f, 0.1f, 0.15f, 0.35f, 1.05f, 1.25f, 1.3f, 1.4f };
		normals = new Vector2[] { Vector2.left, Vector2.up, Vector2.right, Vector2.down, Vector2.left, Vector2.up, Vector2.right };
		Mesh mesh2 = BezierCurve.CreateRenderMesh(points, offsets, uv, new float3(0, 1, 0), false, true, normals);
		return MeshCreator.CombineMeshes(false, mesh1, mesh2);
	}

	protected override float GetConstructionCost()
	{
		return 2f;
	}
}