using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;

public abstract class Constructor
{
	public static EntityManager EntityManager;

	protected ComponentTypes components;
	public ushort id;

	public abstract void InitArchetype();

	public virtual void StartConstructing(Entity entity)
	{
		entity.Modify((ref Constructing c) =>
		{
			c.facade = false;
			c.progress = 0;
		});
		EntityManager.AddComponentData(entity, new Health { health = 5f });
		// No need to do validity check again...
	}

	public abstract void FinishConstructing(Entity facade);

	public void Destroy(Entity entity)
	{
		OnDestroy(entity);
		DeleteRender(entity);
		DynamicBuffer<TileTaken> tilesTaken = EntityManager.GetBuffer<TileTaken>(entity);
		for (int i = 0; i < tilesTaken.Length; i++)
		{
			Assert.IsEqual(Game.map.GetEntity(tilesTaken[i].tile), entity);
			Game.map.UnsetEntity(tilesTaken[i].tile);
		}
		EntityManager.DestroyEntity(entity);
	}

	protected void DeleteRender(Entity entity)
	{
		for (int i = 0; i < entity.Buffer<SubMeshRenderer>().Length; i++)
		{
			EntityManager.DestroyEntity(entity.Buffer<SubMeshRenderer>()[i].renderer);
		}
		entity.Buffer<SubMeshRenderer>().Clear();
	}

	protected abstract void OnDestroy(Entity entity);
}

public abstract class Constructor<T> : Constructor where T : struct, IComponentData
{
	protected abstract List<PFTile> GetTilesTaken(T constructComponent);
	protected abstract float GetConstructionCost();
	protected abstract void OnConstructed(Entity entity, T constructComponent);
	protected abstract void InitRender(Entity entity, bool facadeOrConstructing);

	// You can't create a facade where there is an entity already... botID is there to indicate building rules only
	public Entity AttemptInitFacade(T constructComponent, int botID)
	{
		List<PFTile> tilesTaken = GetTilesTaken(constructComponent);
		if (tilesTaken != null && IsValid(tilesTaken, botID))
		{
			Entity entity = EntityManager.CreateEntity(EntityManager.World.GetExistingSystem<ConstructionSystem>().facadeArchetype);
			EntityManager.SetComponentData(entity, new Constructing { facade = true, progress = 0, progressToComplete = GetConstructionCost() });
			EntityManager.SetComponentData(entity, new Constructable { constructableID = id, numParentSectionsVisible = 85 });
			EntityManager.AddComponentData(entity, constructComponent);
			DynamicBuffer<TileTaken> tilesTakenBuffer = EntityManager.GetBuffer<TileTaken>(entity);
			tilesTakenBuffer.ResizeUninitialized(tilesTaken.Count);
			for (int i = 0; i < tilesTaken.Count; i++)
			{
				Assert.IsTrue(Game.map.IsEmptyAndBuildable(tilesTaken[i], botID));
				Game.map.SetEntity(tilesTaken[i], entity);
				tilesTakenBuffer[i] = new TileTaken { tile = tilesTaken[i] };
			}
			InitRender(entity, true); // If is visible
			return entity;
		}
		return Entity.Null;
	}

	private bool IsValid(List<PFTile> tilesTaken, int botID)
	{
		Assert.IsTrue(tilesTaken.Count >= 1, "Must be at least one tile taken");
		for (int i = 0; i < tilesTaken.Count; i++)
		{
			if (!Game.map.IsEmptyAndBuildable(tilesTaken[i], botID))
				return false;
		}
		return true;
	}

	public override void FinishConstructing(Entity entity)
	{
		// The entity changes into what it's supposed to be...
		EntityManager.RemoveComponent<Constructing>(entity);
		EntityManager.AddComponents(entity, components);

		OnConstructed(entity, EntityManager.GetComponentData<T>(entity));

		EntityManager.RemoveComponent<T>(entity); // It's just not needed anymore.. not to say we couldn't adapt it to be used
		DeleteRender(entity);
		InitRender(entity, false);
	}
}