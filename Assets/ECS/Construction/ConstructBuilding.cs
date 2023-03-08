using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Rendering;
using Unity.Transforms;
using UnityEngine;

public enum BuildingType : byte
{
	Main,
	Barracks,
	EnergyTower
}

public static class BuildingExt
{
	public static void AddComponent(this BuildingType type, Entity entity, EntityManager EntityManager)
	{
		switch (type)
		{
			case BuildingType.Main: 
				EntityManager.AddBuffer<OutBorder>(entity);
				EntityManager.AddComponentData(entity, new EnergyTower { radius = 16 });
				Game.map.SetPathFindingBorder(entity, PFR.BeltNormal, true);
				return;
			case BuildingType.Barracks: EntityManager.AddComponentData(entity, new Barracks { progress = 0 }); return;
			case BuildingType.EnergyTower: EntityManager.AddComponentData(entity, new EnergyTower { radius = 10 }); return;
		}
		Assert.Fail("BuildingType not loaded!");
	}
}

public struct ConstructBuilding : IComponentData
{
	// Something to set it to a barracks to build the catapults
	public BuildingType buildingType;
	public PFTile center;
	public BotEntity owner;
	public int radius;
	public int height;
	public float health;
}

public class BuildingConstructor : Constructor<ConstructBuilding>
{
	public override void InitArchetype()
	{
		components = new ComponentTypes(new ComponentType[] {
			// typeof(OutBorder),
			typeof(NCOElement), // We no longer do this version of ownership...
			typeof(BuildingInfo),
			typeof(CenterTile),
			typeof(Translation),
			typeof(Rotation),
			typeof(PhysicsCollider) });
	}

	public override void StartConstructing(Entity entity)
	{
		base.StartConstructing(entity);
		EntityManager.SetComponentData(entity, new Health { health = 5000f });
	}

	protected override List<PFTile> GetTilesTaken(ConstructBuilding constructBuilding)
	{
		return EntireBorderInfo.GetTilesIfValid(constructBuilding.center, constructBuilding.radius, constructBuilding.height);
	}

	protected override void OnConstructed(Entity entity, ConstructBuilding constructBuilding)
	{
		constructBuilding.buildingType.AddComponent(entity, EntityManager);
		if (constructBuilding.owner.id != Bot.INVALID)
		{
			entity.Buffer<NCOElement>().Add(new NCOElement { owner = constructBuilding.owner, nco = 200f });
		}
		entity.SetData(new BuildingInfo { owner = BotEntity.Invalid });
		entity.SetData(new CenterTile { tile = constructBuilding.center, pos = Game.map.WorldPosition(constructBuilding.center) });
		World.Active.GetExistingSystem<BuildingSystem>().buildings.Add(entity);
	}

	protected override void InitRender(Entity entity, bool facadeOrConstructing)
	{
		PFTile centerTile = facadeOrConstructing ? EntityManager.GetComponentData<ConstructBuilding>(entity).center : EntityManager.GetComponentData<CenterTile>(entity).tile;
		EntireBorderInfo entireBorder = new EntireBorderInfo(centerTile, (tile) => tile.IsValid() && Game.map.IsEntity(tile, entity), (tile) => tile.IsValid());


		Entity renderer = EntityManager.CreateEntity(ConstructionSystem.subMeshRenderer);

		World.Active.EntityManager.GetBuffer<SubMeshRenderer>(entity).Add(new SubMeshRenderer { renderer = renderer });

		Mesh mesh = entireBorder.CreateBorderedMesh(MapInfo.TILE_HEIGHT * 4, -MapInfo.TILE_LENGTH * 0f, false);

		BlobAssetReference<Unity.Physics.Collider> collider = Unity.Physics.MeshCollider.Create(
			new NativeArray<float3>(Array.ConvertAll(mesh.vertices, (item) => (float3)item), Allocator.Temp),
			new NativeArray<int>(mesh.triangles, Allocator.Temp));

		if (!facadeOrConstructing)
		{
			entity.SetData(new PhysicsCollider { Value = collider });
			EntityManager.SetSharedComponentData(renderer, new RenderMesh { mesh = mesh, material = RenderInfo.Building[EntityManager.GetComponentData<BuildingInfo>(entity).owner.id] });
		}
		else
		{
			EntityManager.SetSharedComponentData(renderer, new RenderMesh { mesh = mesh, material = RenderInfo.Building[Bot.INVALID] });
		}
	}

	protected override void OnDestroy(Entity entity)
	{
		/*if (EntityManager.HasComponent<CenterTile>(entity))
		{
			Game.map.SetTilesTakenToNull(entity, EntityManager.GetComponentData<CenterTile>(entity).tile);
		}
		else
		{
			Game.map.SetTilesTakenToNull(entity, EntityManager.GetComponentData<ConstructBuilding>(entity).center);
		}*/
	}

	protected override float GetConstructionCost()
	{
		return 100f;
	}
}