using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using System.Runtime.InteropServices;
using Unity.Collections;
using Unity.Jobs;
using Unity.Entities;
using Unity.Mathematics;
using System.Runtime.Serialization.Formatters.Binary;
using System.IO;
using System.Linq;

public class Game : MonoBehaviour
{
	private static float gameTime = 0f;
	public static int tickCount = 0;

	public static bool STOP_HEAVY_DUTY_SIMULATION = false;

	public static MapObject mapObject;
	public static MapInfo map;

	// public static long timeSpentPathfinding;
	public static long timeSpentOnAll;
	public static long timeSpentOnBots;
	public static long timeSpentOnEntities;

	public static float GetTickTime()
	{
		return Time.fixedDeltaTime;
	}

	public static float GetGameTime()
	{
		return gameTime;
	}

	public static long NanoTime()
	{
		long nano = 10000L * System.Diagnostics.Stopwatch.GetTimestamp();
		nano /= TimeSpan.TicksPerMillisecond;
		nano *= 100L;
		return nano;
	}

	// How to place the power plant?

	// It should be as close to as many buildings that are not close to a power plant...
	// It's not optimal, but I think it actually might be optimal for achieving what I want out of this- things to be spread out

	private void OnApplicationQuit()
	{
		map.Dispose();
	}

	void Start()
	{

		Variance.Init();

		map.Init();

		Bot.Init();
		GetComponent<RenderInfo>().Init();

		Bot.resource = (GameObject)Resources.Load("Bot");
		Section.resource = (GameObject)Resources.Load("Section");
		Section.sectionMat = (Material)Resources.Load("GroundMat");

		LoadMap();


		// buildingHandler = new BuildingHandler();

		mapObject.UpdateKnownVisibilities();

		// ConstructBuilding.LoadMaterial(ConstructBuilding.mats, "BuilderMat");
		// ResourceExt.Init();
		// ResourceExt.LoadResource(Resource.NCO, beltObjectMesh, "ElectricityMat");


		for (int x = 0; x < 100; x++)
		{
			Section section = mapObject.sections[Variance.NextInt(mapObject.sections.Count)];
			PFTile randomTile = new PFTile((byte)Variance.NextInt(section.size.x), 0, (byte)Variance.NextInt(section.size.z), section.GetID());
			int botID = Bot.bots.Count;
			Bot bot = new Bot(randomTile, botID);
			Bot.bots.Add(bot);
			Entity entity = ConstructionSystem.GetConstructor<BuildingConstructor>().AttemptInitFacade(new ConstructBuilding
			{
				buildingType = BuildingType.Main,
				center = randomTile,
				height = 4,
				radius = 4,
				owner = bot.botEntity
			}, Bot.INVALID);

			if (entity != Entity.Null)
			{
				entity.Modify((ref Constructing c) => c.progress = -1);
				bot.OwnedCC = entity;
			}
		}
	}

	private static void LoadMap()
	{
		string destination = Application.persistentDataPath + "/MapSave.dat";
		Debug.Log("Loading map from " + destination);
		FileStream file;

		BinaryFormatter bf = new BinaryFormatter();
		if (File.Exists(destination))
		{
			file = File.OpenRead(destination);
			mapObject = (MapObject)bf.Deserialize(file);
			// Some init stuff needs to happen...
			foreach (Section s in mapObject.sections)
			{
				s.Init();
			}

			for (int a = 0; a < mapObject.sections.Count; a++)
			{
				for (int b = a + 1; b < mapObject.sections.Count; b++)
				{
					foreach (Side aSide in mapObject.sections[a].linked)
					{
						foreach (Side bSide in mapObject.sections[b].linked)
						{
							if (math.distancesq(aSide.GetCenter(), bSide.GetCenter()) < 0.0001f)
							{
								// Assume merged...
								Section.LinkSides(mapObject.sections[a], aSide, mapObject.sections[b], bSide);
							}
						}
					}
				}
			}

			// Debug.Log("Length: " + map.connectionDirs.Length + ", " + map.entities.Length);

			file.Close();
		}
		else
		{
			mapObject = new MapObject();
			mapObject.Add();
			// Save map:
			file = File.Create(destination);
			bf.Serialize(file, mapObject);
			file.Close();

			return;
		}

	}

	private void Update()
	{
		if (Input.GetKeyDown(KeyCode.Alpha1))
		{
			Time.timeScale /= 2;
			Debug.Log("Time Scale: " + Time.timeScale);
		}
		if (Input.GetKeyDown(KeyCode.Alpha2))
		{
			Time.timeScale *= 2;
			Debug.Log("Time Scale: " + Time.timeScale);
		}
		if (Input.GetKeyDown(KeyCode.H))
		{
			STOP_HEAVY_DUTY_SIMULATION = !STOP_HEAVY_DUTY_SIMULATION;
		}
	}

	private void FixedUpdate()
	{
		long startFixedUpdate = NanoTime();

		DebugDraw.ResetIndex();
		tickCount++;

		float tickTime = GetTickTime();
		gameTime += tickTime;

		// long start = NanoTime();
		// PathFinder.Tick();

		// timeSpentPathfinding += NanoTime() - start;

		DebugDraw.tickTime.Add((NanoTime() - startFixedUpdate) / 1000000000f);
		if (DebugDraw.tickTime.Count > 100)
		{
			DebugDraw.tickTime.RemoveAt(0);
		}


		long start = NanoTime();
		foreach (Bot bot in Bot.bots)
		{
			bot.Tick(tickTime);
		}
		timeSpentOnBots += NanoTime() - start;

		DebugDraw.DisplayMessage("Game Time = " + GetGameTime() + "s. Speed = " + Time.timeScale);
		DebugDraw.DisplayMessage("Tick Time = " + timeSpentOnAll / 1000000000f + "s");
		// DebugDraw.DisplayMessage("Building Time = " + timeSpentPathfinding / 1000000000f + "s");
		DebugDraw.DisplayMessage("Bots Time = " + timeSpentOnBots / 1000000000f + "s");

		for (int i = 0; i < 1; i++)
		{
			int randSection = Variance.NextInt(mapObject.sections.Count);
			int randX = Variance.NextInt(map.sectionSize[randSection].x);
			int randZ = Variance.NextInt(map.sectionSize[randSection].z);
			World.Active.GetExistingSystem<ConstructionSystem>().AttemptDamageAtTile(new PFTile((byte)randX, 0, (byte)randZ, randSection), 6);
		}



		timeSpentOnAll += NanoTime() - startFixedUpdate;
	}
}

public class Variance
{
    public static Unity.Mathematics.Random rand;
    // There are 

    public static void Init()
    {
		rand = new Unity.Mathematics.Random(833);
    }
	
	// Random number from 0 <= r < max
	public static int NextInt(int max)
	{
		return Math.Abs(rand.NextInt()) % max;
	}

	public static int NextInt()
	{
		return rand.NextInt();
	}

    public static double NextDouble()
    {
        return rand.NextDouble();
    }

	public static bool Chance(float chance)
	{
		return Range(0f, 1f) < chance;
	}

    // Inclusive to min, exclusive to max
    public static double Range(double min, double max)
    {
        return min + rand.NextDouble() * (max - min);
    }

	public static float Range(float min, float max)
	{
		return min + (float)rand.NextDouble() * (max - min);
	}

	// Inclusive to min, exclusive to max
	public static int Range(int min, int max)
	{
		return min + NextInt(max - min);
	}

	public static IEnumerable<int> GetRandomOrder(int length)
	{
		return Enumerable.Range(0, length).OrderBy(x => Math.Abs(rand.NextInt()));
	}
}