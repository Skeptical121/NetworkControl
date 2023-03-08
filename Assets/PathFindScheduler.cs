using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;


/*
public class BuildBuilding<T> where T : Building, new()
{
	private static Building displayed = null; // Only one thing displayed?
	private bool preview;
	public BuildBuilding(bool preview)
	{
		this.preview = preview;
	}

	public static void ClearDisplayed()
	{
		if (displayed != null)
		{
			displayed.DestroyRender();
			displayed = null;
		}
	}

	public T Build(PFTile center)
	{
		if (preview)
		{
			ClearDisplayed();
		}
		HashSet<PFTile> tiles = EntireBorderInfo.GetTilesIfValid(center, 3, 4);
		if (tiles != null)
		{
			T building = new T();
			building.Add(tiles.ToList(), preview);
			if (preview)
			{
				building.InitRender(true);
				displayed = building;
			}
			return building;
		}
		return null;
	}
}*/

public class PathFindScheduler
{
	private Action<List<PFNode>> callback = null;

	public void Schedule(List<PFNode> from, List<PFNode> to, float3 toPos, BotEntity builder, Action<List<PFNode>> callback = null)
	{
		this.callback = callback;
		PathFinderSystem.AddPathFindJob(new Request { requestBuilder = this, startNodes = from, endNodes = to, endPos = toPos, builder = builder });
	}

	public void PathFindingCompletion(List<PFNode> path)
	{
		OnPathFindingCompletion(path);
		callback?.Invoke(path);
	}
	protected virtual void OnPathFindingCompletion(List<PFNode> path)
	{
	}
}
/*
public class PreviewBuilder : PathFindScheduler
{
	private static List<OEntity> displayed = null; // Only one thing displayed?
	private readonly Func<PFNode, PFNode, OEntity> CreateEntity;

	public PreviewBuilder(Func<PFNode, PFNode, OEntity> CreateEntity)
	{
		this.CreateEntity = CreateEntity;
	}

	public static void ClearDisplayed()
	{
		if (displayed != null)
		{
			// Remove path:
			foreach (OEntity display in displayed)
			{
				display.DestroyRender();
			}
			displayed = null;
		}
	}

	protected override void OnPathFindingCompletion(List<PFNode> path)
	{
		ClearDisplayed();
		// If path.Count = 0, invalid path I guess
		if (path.Count > 0)
		{
			displayed = new List<OEntity>();
			// TODO: Confirm entire path is valid before this loop...
			for (int i = 0; i < path.Count - 1; i++)
			{
				displayed.Add(CreateEntity(path[i], path[i + 1]));
				displayed[displayed.Count - 1].InitRender(true);
			}
		}
	}
}*/