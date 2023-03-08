using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

public class BuildingState
{
	// There's this internal knowledge that the player has we want to emulate here
	public int expectedImportRate = 0;
	public int expectedExportRate = 0;
}

// So we want some combination of a behaviour tree, and a finite state machine

public enum Result
{
	Invalid,
	Failure,
	InProgress,
	Success
}

public abstract class Task
{
	public abstract Result Execute(Bot bot, float tickTime);

	// Returns success or failure
	protected static Result End(bool success)
	{
		if (success)
			return Result.Success;
		else
			return Result.Failure;
	}
}

public class FindNearbyCCTask : Task
{
	Entity nearTo;
	Func<BuildingInfo, DynamicBuffer<NCOElement>, bool> entityCondition;
	Action<Entity> OutBuilding;
	public bool Init(Entity nearTo, Func<BuildingInfo, DynamicBuffer<NCOElement>, bool> entityCondition, Action<Entity> OutBuilding)
	{
		this.nearTo = nearTo;
		this.entityCondition = entityCondition;
		this.OutBuilding = OutBuilding;
		return true;
	}

	public override Result Execute(Bot bot, float tickTime)
	{
		if (!Bot.EM.HasComponent<CenterTile>(nearTo))
			return Result.Failure;
		float closestDistSqr = float.PositiveInfinity;
		Entity closestValid = Entity.Null;
		foreach (Entity entity in World.Active.GetExistingSystem<BuildingSystem>().buildings)
		{
			if (entity != Entity.Null && Bot.EM.HasComponent<BuildingInfo>(entity) && 
				entityCondition(Bot.EM.GetComponentData<BuildingInfo>(entity), Bot.EM.GetBuffer<NCOElement>(entity)))
			{
				float guessDistanceSq = Game.map.GuessDistanceSq(Bot.EM.GetComponentData<CenterTile>(nearTo).tile, Bot.EM.GetComponentData<CenterTile>(entity).tile);
				if (guessDistanceSq < closestDistSqr)
				{
					closestDistSqr = guessDistanceSq;
					closestValid = entity;
				}
			}
		}
		OutBuilding(closestValid);
		return End(closestValid != Entity.Null);
	}
}

public class WalkToBuildableDistance : Task
{
	const float BUILDABLE_DIST = 7f; // A little closer than the bot needs to be...

	public bool Init(Bot bot, PFTile tile)
	{
		new PathFindScheduler().Schedule(bot.onTile.GetAllAdjacentPFRs(PFR.Horizontal), tile.GetAllAdjacentPFRs(PFR.Horizontal), Game.map.WorldPosition(tile), bot.botEntity,
			(path) => {
				remainingPath = path;
				pathIsValid = path.Count > 0;
			});
		return true;
	}

	// Bot pathfinding is trivial, but still wait a frame before starting it...
	List<PFNode> remainingPath = null;
	bool pathIsValid = false;

	public override Result Execute(Bot bot, float tickTime)
	{
		if (remainingPath == null) // Waiting for pathfinding result
			return Result.InProgress;

		// Debug.Log("Path is valid:" + pathIsValid);
		
		if (!pathIsValid)
			return Result.Failure;

		if (remainingPath.Count == 0)
			return Result.Success;

		if (bot.WalkTo(remainingPath[0].tile, tickTime))
		{
			remainingPath.RemoveAt(0);
		}
		return Result.InProgress;
	}
}

public class BuildConstructableTask<T> : Task where T : struct, IComponentData
{
	Constructor<T> constructor;
	T constructComponent;
	Action<Entity> OutBuilding;
	public bool Init(Constructor<T> constructor, T constructComponent, Action<Entity> OutBuilding)
	{
		this.constructor = constructor;
		this.constructComponent = constructComponent;
		this.OutBuilding = OutBuilding;
		return true;
	}

	public override Result Execute(Bot bot, float tickTime)
	{
		Entity outBuilding = constructor.AttemptInitFacade(constructComponent, bot.id);
		if (outBuilding != Entity.Null)
		{
			outBuilding.Modify((ref Constructing c) => c.progress = -1);
		}
		OutBuilding(outBuilding);
		if (outBuilding != Entity.Null)
			return Result.Success;
		else
			return Result.Failure;
	}
}

public abstract class TaskBoxBox
{
	public TaskBoxBox success = null;
	public TaskBoxBox failure = null;
	public abstract void Execute(Bot bot, float tickTime);
}

public class TaskBox<T> : TaskBoxBox where T : Task, new()
{
	private readonly Func<T, bool> taskInit;
	private T task = null;

	private readonly Action<T> onSuccess = null;
	private readonly Action<T> onFailure = null;
	public TaskBox(Func<T, bool> taskInit, Action<T> onSuccess = null, Action<T> onFailure = null)
	{
		this.taskInit = taskInit;
		this.onSuccess = onSuccess;
		this.onFailure = onFailure;
	}
	public override void Execute(Bot bot, float tickTime)
	{
		if (task == null)
		{
			task = new T();
			if (!taskInit(task))
			{
				OnTaskRun(bot, Result.Failure);
				return;
			}
		}
		OnTaskRun(bot, task.Execute(bot, tickTime));
	}

	public void OnTaskRun(Bot bot, Result result)
	{
		switch (result)
		{
			case Result.Success: onSuccess?.Invoke(task); bot.currentTask = success; break;
			case Result.Failure: onFailure?.Invoke(task); bot.currentTask = failure; break;
		}
	}
}

public static class TaskBuilder
{

	public static TaskBoxBox CreateSuccessChain(params TaskBoxBox[] tasks)
	{
		Assert.IsTrue(tasks.Length > 0);
		for (int i = 1; i < tasks.Length; i++)
		{
			tasks[i - 1].success = tasks[i];
		}
		return tasks[0];
	}
}

// Same here..
public class BuildRoute : Task
{
	public bool Init(BotEntity builder, PFR pfr, Entity from, Entity to)
	{
		if (!from.Has<OutBorder>() || !to.Has<OutBorder>())
			return false;
		Init(builder, OutBorder.GetPFNodes(from, pfr, true),
			   OutBorder.GetPFNodes(to, pfr, false),
			   World.Active.EntityManager.GetComponentData<CenterTile>(to).pos);
		return true;
	}

	public void Init(BotEntity builder, PFR pfr, List<PFNode> fromBorder, Entity to)
	{
		Init(builder, fromBorder, OutBorder.GetPFNodes(to, pfr, false),
			World.Active.EntityManager.GetComponentData<CenterTile>(to).pos);
	}

	public void Init(BotEntity builder, List<PFNode> fromBorder, List<PFNode> toBorder, float3 to)
	{
		new PathFindScheduler().Schedule(fromBorder, toBorder, to, builder,
			   (path) =>
			   {
				   Entity entity = Entity.Null;
				   BeltConstructor beltConstructor = ConstructionSystem.GetConstructor<BeltConstructor>();
				   for (int i = 0; i < path.Count - 1; i++)
				   {
					   entity = beltConstructor.AttemptInitFacade(new ConstructBelt { builder = builder, from = path[i], to = path[i + 1] }, builder.id);
				   }
				   result = entity != Entity.Null ? Result.Success : Result.Failure; // Hmm
			   });
	}

	Result result = Result.InProgress;

	public override Result Execute(Bot bot, float tickTime)
	{
		return result;
	}
}

public class Bot
{
	public static GameObject resource;
	public static EntityManager EM;

	public GameObject gameRep; // gameRep always exists for bot

	public const float WALK_SPEED = 5f;

	public const int MAX_BOTS = 500; // Number of resources to load
	public const int INVALID = MAX_BOTS;

	public static List<Bot> bots = new List<Bot>();
	// public HashSet<Building> controlled = new HashSet<Building>();

	// public List<Building> facadeBuildings = new List<Building>();

	public int id;

	private static List<Color> color = new List<Color>();

	public TaskBoxBox currentTask = null;
	public PFTile onTile;


	// State Information
	public Entity OwnedCC { get; set; }
	public Entity FromCC { get; set; }
	public Entity ToCC { get; set; }

	public Entity entity;

	public BotEntity botEntity { get => new BotEntity { entity = entity, id = id }; }
	public BotMapInfo botMapInfo;

	public float Credit
	{ 
		get => EM.GetComponentData<BotCredit>(entity).credit;
		set => EM.SetComponentData(entity, new BotCredit { credit = value });
	}

	public Bot(PFTile onTile, int id)
	{
		this.onTile = onTile;
		this.id = id;
		gameRep = UnityEngine.Object.Instantiate(resource);
		gameRep.name = "Bot " + id;
		gameRep.GetComponent<MeshRenderer>().material.color = GetControlColor(id);
		gameRep.transform.position = Game.map.WorldPosition(onTile);
		entity = EM.CreateEntity(typeof(BotCredit));
		botMapInfo = new BotMapInfo { border = new NativeHashMap<int, PFTile>(0, Allocator.Persistent) };
	}

	// public void SetMainCC(Entity entity) { MainCC = entity; }

	public static void Init()
	{
		EM = World.Active.EntityManager;
		for (int i = 0; i < MAX_BOTS; i++)
		{
			color.Add(new Color(Variance.Range(0f, 1f), Variance.Range(0f, 1f), Variance.Range(0f, 1f)));
		}
	}

	public static Color GetControlColor(int ownerID)
	{
		if (ownerID == INVALID)
			return Color.grey;
		else
			return color[ownerID];
	}

	public void Tick(float tickTime)
	{
		if (currentTask != null)
		{
			currentTask.Execute(this, tickTime);
		}
		else if (Variance.Chance(0.02f))
		{
			Think();
		}
	}

	public bool WalkTo(PFTile tile, float tickTime)
	{
		float3 to = Game.map.WorldPosition(tile);
		float walkDist = WALK_SPEED * tickTime;
		float distSqr = math.distancesq(gameRep.transform.position, to);
		if (walkDist * walkDist < distSqr)
		{
			gameRep.transform.position += (Vector3)(math.normalize(to - (float3)gameRep.transform.position) * walkDist);
			// Check if tile is different:
			PFTile tileOn = EntireBorderInfo.OpenSetSearch(PFR.Horizontal, new List<PFTile> { onTile }, 5, (testTile, offset) => Game.map.TileContains(testTile, gameRep.transform.position));
			if (tileOn.IsValid())
			{
				onTile = tileOn;
			}
			else
			{
				// Mostly this should be fine..
				// Assert.Fail("Could not find tile for bot! " + gameRep.transform.position + ", " + id);
			}
			return false;
		}
		else
		{
			onTile = tile;
			gameRep.transform.position = to;
			return true;
		}
	}

	// Control Center
	public void Think()
	{
		/*if (!EM.Exists(OwnedCC))
		{
			currentTask = new TaskBox<BuildConstructableTask<ConstructBuilding>>((task) => task.Init(ConstructionSystem.GetConstructor<BuildingConstructor>(), 
				new ConstructBuilding{ buildingType = BuildingType.Main, owner = botEntity, center = onTile, height = 4, radius = 4 },
				(entity) => OwnedCC = entity));
		}
		else
		{*/

		// So... how do we abstractify what you have?
		if (botMapInfo.border.Length > 0)
		{
			PFTile tile = botMapInfo.border.GetValueArray(Allocator.Temp)[Variance.NextInt(botMapInfo.border.Length)];
			currentTask = TaskBuilder.CreateSuccessChain(
				new TaskBox<WalkToBuildableDistance>((task) => task.Init(this, tile)),
				new TaskBox<BuildConstructableTask<ConstructBuilding>>((task) => task.Init(ConstructionSystem.GetConstructor<BuildingConstructor>(),
					new ConstructBuilding { buildingType = BuildingType.EnergyTower, owner = botEntity, center = onTile, height = 4, radius = 1 },
					(entity) => OwnedCC = entity)));
		}
		else
		{


			currentTask = TaskBuilder.CreateSuccessChain(
				new TaskBox<FindNearbyCCTask>((task) => task.Init(OwnedCC, (ncoInfo, ncoElements) => Variance.Chance(0.4f), (entity) => FromCC = entity)),
				new TaskBox<FindNearbyCCTask>((task) => task.Init(OwnedCC, (ncoInfo, ncoElements) => Variance.Chance(0.4f), (entity) => ToCC = entity)),
				new TaskBox<WalkToBuildableDistance>((task) => { if (FromCC.Has<CenterTile>()) return task.Init(this, FromCC.Get<CenterTile>().tile); else return false; }),
				//new TaskBox((bot) => new BuildConstructableTask<ConstructMiningDrill>(bot.CoalSpawn.tile),
				//	(bot, task) => bot.MiningDrill = ((BuildConstructableTask<ConstructMiningDrill>)task).outBuilding),
				new TaskBox<WalkToBuildableDistance>((task) => { if (ToCC.Has<CenterTile>()) return task.Init(this, ToCC.Get<CenterTile>().tile); else return false; }),
				new TaskBox<BuildRoute>((task) => task.Init(botEntity, PFR.BeltNormal, FromCC, ToCC)));
			//,
			//new TaskBox((bot) => new WalkToBuildableDistance(bot, World.Active.EntityManager.GetComponentData<CenterTile>(bot.NearbyBuilding).center)),
			//new TaskBox((bot) => new BuildRoute(new ConnectConveyorBelts(), PFR.BeltNormal, bot.NearbyBuilding, bot.MainCC)));
			//}
		}
		

		/*foreach (Entity owned in EM.World.GetExistingSystem<BuildingSystem>().buildings)
		{
			if (EM.GetComponentData<BuildingInfo>(owned).owner.id == id)
			{
				foreach (Entity building in EM.World.GetExistingSystem<BuildingSystem>().buildings)
				{
					float distance = math.distance(EM.GetComponentData<CenterTile>(owned).pos, EM.GetComponentData<CenterTile>(building).pos);

				}
			}
		}*/
	}
}
