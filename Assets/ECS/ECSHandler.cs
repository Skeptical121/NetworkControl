using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using UnityEngine;

public class ECSHandler : MonoBehaviour
{
	public static float interp = 0; // From 0-1, the global interp value for rendering
	private List<JobComponentSystemWithCallback> systemsWithCallback = new List<JobComponentSystemWithCallback>();

	// Start is called before the first frame update
	void Awake()
	{
		ECSExtensions.EntityManager = World.Active.EntityManager;
		World.Active.GetExistingSystem<SimSystemGroup>().Enabled = false;
		// NativeQueue.ParallelWriter is non-deterministic. NativeStream and EntityCommandBuffer are deterministic though and will probably meet your needs.

		// Ideally we do this by reflection:
		foreach (Type type in typeof(JobComponentSystemWithCallback).Assembly.GetTypes().Where(type => type.IsSubclassOf(typeof(JobComponentSystemWithCallback))))
		{
			if (!type.IsAbstract)
			{
				systemsWithCallback.Add((JobComponentSystemWithCallback)World.Active.GetExistingSystem(type));
			}
		}
	}

	float lastUpdateTime = 0;
	// I believe this runs before ECS stuff is run
	void Update()
	{
		interp = Mathf.Clamp01((Time.time - lastUpdateTime) / Game.GetTickTime());
	}

	void FixedUpdate()
	{
		SimSystemGroup simSystemGroup = World.Active.GetExistingSystem<SimSystemGroup>();
		simSystemGroup.Enabled = true;
		simSystemGroup.Update();
		simSystemGroup.Enabled = false;
		for (int i = 0; i < systemsWithCallback.Count; i++)
		{
			systemsWithCallback[i].MainThreadSimulationCallbackTick();
		}
		lastUpdateTime = Time.time;
	}
}

public static class ECSExtensions
{
	// Assumes one world:
	public static EntityManager EntityManager;

	public delegate void Update<T>(ref T c) where T : struct, IComponentData;

	//public static void ModifyComponentData<T>(this EntityManager EntityManager, Entity entity, Update<T> setVar) where T : struct, IComponentData
	//{
	//	T componentData = EntityManager.GetComponentData<T>(entity);
	//	setVar(ref componentData);
	//	EntityManager.SetComponentData(entity, componentData);
	//}

	public static void Modify<T>(this Entity entity, Update<T> setVar) where T : struct, IComponentData
	{
		T componentData = entity.Get<T>();
		setVar(ref componentData);
		entity.SetData(componentData);
	}

	public static DynamicBuffer<T> Buffer<T>(this Entity entity) where T : struct, IBufferElementData
	{
		return EntityManager.GetBuffer<T>(entity);
	}

	public static bool Has<T>(this Entity entity)
	{
		return EntityManager.HasComponent<T>(entity);
	}

	public static T Get<T>(this Entity entity) where T : struct, IComponentData
	{
		return EntityManager.GetComponentData<T>(entity);
	}

	public static void SetData<T>(this Entity entity, T data) where T : struct, IComponentData
	{
		EntityManager.SetComponentData(entity, data);
	}

	public static void SetSharedData<T>(this Entity entity, T data) where T : struct, ISharedComponentData
	{
		EntityManager.SetSharedComponentData(entity, data);
	}

	// Not terribly efficient..
	public static U Get<T, U>(this NativeMultiHashMap<T, U> map, T key, int index) where T : struct, IEquatable<T> where U : struct
	{
		NativeMultiHashMap<T, U>.Enumerator enumerator = map.GetValuesForKey(key);

		int count = 0;
		foreach (U value in enumerator)
		{
			if (index == count++)
				return value;
		}
		Assert.Fail("Did not find index: " + index + " at key " + key);
		return default(U);
	}
}