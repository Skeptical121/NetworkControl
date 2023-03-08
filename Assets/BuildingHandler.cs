using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;
/*
public class BuildingHandler
{

	public List<Building> buildings = new List<Building>();
	// public HashSet<Type> entityTypesThatExist = new HashSet<Type>();

	// public Dictionary<Type, Func<Building, bool>> includeBuilding = new Dictionary<Type, Func<Building, bool>>();

	public BuildingHandler()
	{
		// includeBuilding.Add(typeof(ResearchLab), (b) => b.GetType() == typeof(MiningDrill) || b.GetType() == typeof(PowerPlant));
		// includeBuilding.Add(typeof(PowerPlant), (b) => b.AcceptsElectricity());
	}

	public void KMeans()
	{
		
	}

	// Only applies if entityTypeExists == true
	/*public Vector3 GetOptimalPlacement(Type entityType, int kMeans)
	{
		Func<Building, bool> include = includeBuilding[entityType];

		if (!entityTypesThatExist.Contains(entityType))
		{
			float3 sum = float3.zero;
			float total = 0f;
			foreach (Building building in buildings)
			{
				if (include(building))
				{
					sum += Game.map.WorldPosition(building.GetCenterTile(), PFR.Horizontal);
					total++;
				}
			}
			if (total == 0f)
			{
				Debug.LogError("No buildings?");
				return Vector3.zero; // Hmm
			}
			else
			{
				return sum / total;
			}
		}
		else
		{

			// Init means...
			List<Building> includedBuildings = new List<Building>();
			List<int> randomStartPossibilities = new List<int>();
			foreach (Building building in buildings)
			{
				if (include(building))
				{
					includedBuildings.Add(building);
					randomStartPossibilities.Add(randomStartPossibilities.Count);
				}
			}

			while (kMeans != 1 && includedBuildings.Count <= kMeans * 2)
			{
				kMeans--;
			}

			float3[] sum = new float3[kMeans];
			float[] total = new float[kMeans];

			float3[] means = new float3[kMeans];

			for (int k = 0; k < kMeans; k++)
			{
				int rand = Variance.NextInt(randomStartPossibilities.Count);
				means[k] = Game.map.WorldPosition(includedBuildings[randomStartPossibilities[rand]].GetCenterTile(), PFR.Horizontal);
				randomStartPossibilities.RemoveAt(rand);
			}


			Dictionary<Building, int> nearestMean = new Dictionary<Building, int>();
			for (int iteration = 0; iteration < 100; iteration++)
			{


				// ..

				for (int k = 0; k < kMeans; k++)
				{
					sum[k] = float3.zero;
					total[k] = 0;
				}
				bool changed = false;
				foreach (Building building in includedBuildings)
				{
					float closest = float.PositiveInfinity;
					int closestK = 0;
					for (int k = 0; k < kMeans; k++)
					{
						float dist = Vector3.Distance(Game.map.WorldPosition(building.GetCenterTile(), PFR.Horizontal), means[k]);
						if (dist < closest)
						{
							closest = dist;
							closestK = k;
						}
					}
					float valueSquared = building.distanceToNearestBuildingOfType[entityType] * building.distanceToNearestBuildingOfType[entityType];
					sum[closestK] += valueSquared * Game.map.WorldPosition(building.GetCenterTile(), PFR.Horizontal);
					total[closestK] += valueSquared;

					if (!nearestMean.ContainsKey(building) || nearestMean[building] != closestK)
					{
						changed = true;
						nearestMean[building] = closestK;
					}
				}
				if (!changed)
				{
					// Find k with largest sum
					float highestSum = -1f;
					int highestK = 0;
					for (int k = 0; k < kMeans; k++)
					{
						if (total[k] > highestSum)
						{
							highestSum = total[k];
							highestK = k;
						}
					}
					return means[highestK];
				}

				for (int k = 0; k < kMeans; k++)
				{
					means[k] = sum[k] / total[k];
				}
			}
			Debug.LogError("K means failed after 100 iterations..");
			return float3.zero;
		}
	}*/

	/*public PFTile GetApproximateTileClosestTo(float3 pos)
	{
		float closestDist = float.PositiveInfinity;
		Section closestNode = null;
		foreach (Section node in Game.mapObject.sections)
		{
			float dist = Vector3.Distance(pos, node.GetCenter());
			if (dist < closestDist)
			{
				closestDist = dist;
				closestNode = node;
			}
		}
		return closestNode.GetClosestTileWhereYis0(pos);
	}*/
	/*
	public void OnAddBuilding(Building add)
	{

		buildings.Add(add); // This is fine as includeBuilding shouldn't be true for itself anyways

		/*Type type = add.GetType();

		foreach (KeyValuePair<Type, Func<Building, bool>> include in includeBuilding)
		{
			if (entityTypesThatExist.Contains(include.Key) && include.Value(add))
			{
				float closest = float.PositiveInfinity;
				foreach (Building building in buildings)
				{
					if (building.GetType() == include.Key)
					{
						float dist = Vector3.Distance(Game.map.WorldPosition(add.GetCenterTile(), PFR.Horizontal), Game.map.WorldPosition(building.GetCenterTile(), PFR.Horizontal));
						if (dist < closest)
						{
							closest = dist;
						}
					}
				}
				if (closest != float.PositiveInfinity)
				{
					add.distanceToNearestBuildingOfType[include.Key] = closest;
				}
			}
		}

		if (includeBuilding.ContainsKey(type))
		{
			Func<Building, bool> condition = includeBuilding[type];

			bool added = entityTypesThatExist.Add(type);
			foreach (Building building in buildings)
			{
				if (condition(building))
				{
					float dist = Vector3.Distance(Game.map.WorldPosition(add.GetCenterTile(), PFR.Horizontal), Game.map.WorldPosition(building.GetCenterTile(), PFR.Horizontal));
					if (added || dist < building.distanceToNearestBuildingOfType[type])
					{
						building.distanceToNearestBuildingOfType[type] = dist;
					}
				}
			}
		}*/
	/*}

	public void BuildingsTick(float tickTime)
	{
		if (Game.tickCount % 100 == 0)
		{
			foreach (Building building in buildings)
			{
				building.BotControlUpdate();
			}
		}


		long start = Game.NanoTime();

		// Entities might be added to poweredEntities during this loop:
		for (int i = 0; i < buildings.Count; i++)
		{
			buildings[i].Tick(tickTime);
		}
		Game.timeSpentOnEntities += Game.NanoTime() - start;
		DebugDraw.DisplayMessage("Entity Time = " + Game.timeSpentOnEntities / 1000000000f + "s");
	}
}
*/