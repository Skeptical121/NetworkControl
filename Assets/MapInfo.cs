using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Rendering;
using Unity.Transforms;
using UnityEngine;

public struct TileOwner
{
	public int botID;
	public PFTile tile; // Very useful.. (back indexing is not easy)
}

public struct ContestedTile : IEquatable<ContestedTile>
{
	public int botID; // Energy towers compete for land that bots are allowed to build on
	public int botControl;

	public static ContestedTile Invalid { get => new ContestedTile { botID = Bot.INVALID, botControl = 0 }; }

	public bool Equals(ContestedTile other)
	{
		return botID == other.botID;
	}
}

// MapInfo per bot
public struct BotMapInfo
{
	// We need to save the PFTile because right now we don't have a great way of indexing it... also is there even a NativeHashSet?
	[ReadOnly] public NativeHashMap<int, PFTile> border;
}

public struct MapInfo
{
	// For PathFindingNode:
	public const byte MAX_DIRS = 6;

	public const float TILE_LENGTH = 1f;
	public const float TILE_HEIGHT = TILE_LENGTH * 0.7f;
	public const float MAX_TILE_LENGTH_DIFFERENCE = 0.4f;

	[ReadOnly] public NativeHashMap<int, PFTile> linkedTiles; // The ints on the left are PFNodes, with PFR set to 0

	// Indexed by tile
	[ReadOnly] private NativeHashMap<int, Entity> entities;
	// Indexed by tile where y = 0
	[ReadOnly] private NativeHashMap<int, TileOwner> tileOwners;
	[ReadOnly] private NativeMultiHashMap<int, ContestedTile> allTileOwners;
	[ReadOnly] private NativeHashMap<int, Entity> tileOwnerRenderers;

	// Indexed by section
	[ReadOnly] public NativeList<int3> sectionSize; // width, height, length
	[ReadOnly] public NativeList<int> sectionStartIndex;
	[ReadOnly] public NativeList<Entity> sectionRender;

	// Will be indexed by PathfindingNode where y = 0!
	[ReadOnly] private NativeList<float3> connectionPositions;
	[ReadOnly] private NativeList<float3> connectionDirs;

	public int Index(PFTile tile)
	{
		return sectionStartIndex[tile.section] + (tile.x * sectionSize[tile.section].y + tile.y) * sectionSize[tile.section].z + tile.z;
	}

	public int Index(PFNode node)
	{
		return (Index(node.tile) * MAX_DIRS + node.dir) * (byte)PFR.MAX_CONNECTION_TYPES + node.pfr;
	}

	public float3 ConnectionPoint(PFNode node)
	{
		// byte y = node.tile.y;
		// node.tile.y = 0;
		return connectionPositions[Index(node)]; // + new float3(0, y * TILE_HEIGHT, 0);
	}

	public float3 ConnectionNormal(PFNode node)
	{
		// node.tile.y = 0;
		return connectionDirs[Index(node)];
	}

	public PFTile GetToTile(PFNode node)
	{
		return GetToTile(node.tile, node.dir);
	}

	public PFTile GetToTile(PFTile tile, byte dir)
	{
		PFTile nextTile = tile;
		bool isNextValid;
		switch (dir)
		{
			case 0: isNextValid = --nextTile.x != 255; break;
			case 1: isNextValid = ++nextTile.x != sectionSize[tile.section].x; break;
			case 2: isNextValid = --nextTile.z != 255; break;
			case 3: isNextValid = ++nextTile.z != sectionSize[tile.section].z; break;
			case 4: isNextValid = --nextTile.y != 255; break;
			case 5: isNextValid = ++nextTile.y != sectionSize[tile.section].y; break;
			default: isNextValid = true; break; // JOB_ASSERT
		}
		if (isNextValid)
			return nextTile;

		if (linkedTiles.TryGetValue(Index(new PFNode(tile, dir, 0)), out PFTile newTile))
			return newTile;
		else
			return new PFTile(nextTile.x, nextTile.y, nextTile.z, -1);
	}

	public PFNode GetNextPFNode(byte fromPFR, PFTile next, byte dir, byte toPFR, NativeList<PFTile> outTilesTaken)
	{
		if (next.IsValid())
		{
			outTilesTaken.Clear();
			if (fromPFR == (byte)PFR.BeltNormal || fromPFR == (byte)PFR.BeltUp || fromPFR == (byte)PFR.BeltDown)
			{
				outTilesTaken.Add(next);
				if (fromPFR == (byte)PFR.BeltUp || toPFR == (byte)PFR.BeltUp)
				{
					outTilesTaken.Add(GetToTile(next, PFNode.UP));
				}
				else if (fromPFR == (byte)PFR.BeltDown || toPFR == (byte)PFR.BeltDown)
				{
					outTilesTaken.Add(GetToTile(next, PFNode.DOWN));
				}
			} // else no tiles are taken...
			switch ((PFR)fromPFR)
			{
				case PFR.BeltUp: return new PFNode(GetToTile(next, PFNode.UP), dir, toPFR);
				case PFR.BeltDown: return new PFNode(GetToTile(next, PFNode.DOWN), dir, toPFR);
				default: return new PFNode(next, dir, toPFR);
			}
		}
		else
		{
			return new PFNode(next, dir, toPFR); // Invalid
		}
	}

	public Entity GetEntity(PFTile tile)
	{
		if (tile.IsValid() && entities.TryGetValue(Index(tile), out Entity entity))
			return entity;
		else
			return Entity.Null;
	}

	public bool IsEmpty(PFTile tile)
	{
		return !entities.ContainsKey(Index(tile));
	}

	public bool IsEmptyAndBuildable(PFTile tile, int botID)
	{
		if (botID == Bot.INVALID)
			return IsEmpty(tile);

		PFTile tileWhereYIs0 = tile;
		tileWhereYIs0.y = 0;
		if (tileOwners.TryGetValue(Index(tileWhereYIs0), out TileOwner tileOwner))
			return tileOwner.botID == botID && IsEmpty(tile);
		else
			return false;
	}

	public bool IsEntity(PFTile tile, Entity entity)
	{
		if (entities.TryGetValue(Index(tile), out Entity e))
			return e == entity;
		else
			return false;
	}


	// DO NOT CALL THE NEXT FEW METHODS IN A JOB!!!!! DON'T DO IT!!! (They mutate the map)
	public void SetEntity(PFTile tile, Entity entity)
	{
		entities[Index(tile)] = entity;
	}
	public void UnsetEntity(PFTile tile)
	{
		entities.Remove(Index(tile));
	}

	private void TileUpdated(int oldOwner, PFTile tileUpdated)
	{
		OnUpdateTile(oldOwner, tileUpdated);
		for (byte dir = 0; dir < PFR.Horizontal.MaxDir(); dir++)
		{
			PFTile tile = GetToTile(tileUpdated, dir);
			if (tile.IsValid())
			{
				OnUpdateTile(tileOwners.ContainsKey(Index(tile)) ? tileOwners[Index(tile)].botID : Bot.INVALID, tile);
			}
		}
	}

	private void OnUpdateTile(int oldOwner, PFTile tileUpdated)
	{
		if (oldOwner != Bot.INVALID)
		{
			Bot.bots[oldOwner].botMapInfo.border.Remove(Index(tileUpdated));
		}
		if (tileOwners.ContainsKey(Index(tileUpdated)))
		{
			TileOwner tileOwner = tileOwners[Index(tileUpdated)];
			for (byte dir = 0; dir < PFR.Horizontal.MaxDir(); dir++)
			{
				PFTile tile = GetToTile(tileOwner.tile, dir);
				if (tile.IsValid())
				{
					if (tileOwners.TryGetValue(Index(tile), out TileOwner other))
					{
						if (tileOwner.botID != other.botID)
						{
							// Contested location, essentially
							Bot.bots[tileOwner.botID].botMapInfo.border[Index(tileOwner.tile)] = tileOwner.tile;
							break;
						}
					}
					else
					{
						Bot.bots[tileOwner.botID].botMapInfo.border[Index(tileOwner.tile)] = tileOwner.tile;
						break;
					}
				}
			}
		}
	}

	// tile.y should be 0 here
	public void AddTileControl(PFTile tile, int botID, int control)
	{
		Assert.IsEqual(tile.y, 0);
		Assert.IsTrue(control != 0); // Control update should either be negative or positive
		int tileIndex = Index(tile);

		int previousOwner = tileOwners.ContainsKey(tileIndex) ? tileOwners[tileIndex].botID : Bot.INVALID;

		int previous = 0;
		NativeMultiHashMap<int, ContestedTile>.Enumerator owners = allTileOwners.GetValuesForKey(tileIndex);


		foreach (ContestedTile contestedTile in owners)
		{
			if (contestedTile.botID == botID)
			{
				previous = contestedTile.botControl;
				break;
			}
		}
		if (previous > 0)
			allTileOwners.Remove(tileIndex, new ContestedTile { botID = botID, botControl = previous });
		if (previous + control > 0)
			allTileOwners.Add(tileIndex, new ContestedTile { botID = botID, botControl = previous + control });

		owners = allTileOwners.GetValuesForKey(tileIndex);
		ContestedTile max = ContestedTile.Invalid;
		foreach (ContestedTile contestedTile in owners)
		{
			if (contestedTile.botControl > max.botControl)
			{
				max = contestedTile;
			}
		}
		if (max.botID == Bot.INVALID)
		{
			tileOwners.Remove(tileIndex);
			if (tileOwnerRenderers.ContainsKey(tileIndex)) // technically isVisible should be used here I think
			{
				ECSExtensions.EntityManager.DestroyEntity(tileOwnerRenderers[tileIndex]);
				tileOwnerRenderers.Remove(tileIndex);
			}
		}
		else
		{
			Entity entity;
			if (!tileOwnerRenderers.ContainsKey(tileIndex)) // technically isVisible should be used here I think
			{
				entity = ECSExtensions.EntityManager.CreateEntity(ConstructionSystem.subMeshRenderer);
				entity.SetData(new Translation { Value = WorldPosition(tile) });
				entity.SetData(new Rotation { Value = quaternion.LookRotationSafe(ConnectionNormal(new PFNode(tile, 0, (byte)PFR.Horizontal)), new float3(0, 1, 0)) });
				tileOwnerRenderers.TryAdd(tileIndex, entity);
			}
			else
			{
				entity = tileOwnerRenderers[tileIndex];
			}
			entity.SetSharedData(new RenderMesh { material = RenderInfo.Facade[max.botID], mesh = RenderInfo.tileObject });
			tileOwners[tileIndex] = new TileOwner { botID = max.botID, tile = tile };
		}
		TileUpdated(previousOwner, tile);
	}

	public float3 WorldPosition(PFTile tile, PFR pfr = PFR.Horizontal)
	{
		return Game.mapObject.sections[tile.section].GetWorldPosition(tile.x * 2 + 1, tile.y * 2 + 1, tile.z * 2 + 1) + pfr.Offset();
	}

	public float3 CornerPos(PFNode pfn, int c)
	{
		Section section = Game.mapObject.sections[pfn.tile.section];
		switch (pfn.dir)
		{
			case 0: return section.GetWorldPosition(pfn.tile.x * 2, 0, pfn.tile.z * 2 + 2 - c * 2); // Note how order does matter
			case 1: return section.GetWorldPosition(pfn.tile.x * 2 + 2, 0, pfn.tile.z * 2 + c * 2);
			case 2: return section.GetWorldPosition(pfn.tile.x * 2 + c * 2, 0, pfn.tile.z * 2);
			case 3: return section.GetWorldPosition(pfn.tile.x * 2 + 2 - c * 2, 0, pfn.tile.z * 2 + 2);
			default: Assert.Fail("Invalid: " + pfn.dir); return new float3(0, 0, 0);
		}
	}

	public bool TileContains(PFTile tile, float3 pos)
	{
		for (byte dir = 0; dir < PFR.Horizontal.MaxDir(); dir++)
		{
			PFNode node = new PFNode(tile, dir, (byte)PFR.Horizontal);
			if (math.dot(ConnectionNormal(node), pos - ConnectionPoint(node)) > 0) // If the point is facing out, the value will be above 0
			{
				return false;
			}
		}
		return true;
	}

	/*public void SetTilesTakenToNull(Entity entity, PFTile tileContained)
	{
		Assert.IsTrue(IsEntity(tileContained, entity));
		MapUpdater.SetEntity(tileContained, Entity.Null);
		List<PFTile> tilesUnchecked = new List<PFTile>{ tileContained };
		while (tilesUnchecked.Count > 0)
		{
			PFTile checkTile = tilesUnchecked[tilesUnchecked.Count - 1];
			tilesUnchecked.RemoveAt(tilesUnchecked.Count - 1);
			for (byte dir = 0; dir < PFR.All.MaxDir(); dir++)
			{
				PFTile tile = GetToTile(checkTile, dir);
				if (GetEntity(tile) == entity)
				{
					MapUpdater.SetEntity(tile, Entity.Null);
					tilesUnchecked.Add(tile);
				}
			}
		}
	}*/
	/*
	public void UpdateBotBorders()
	{
		botBorders.Clear();
		NativeArray<TileOwner> tiles = tileOwners.GetValueArray(Allocator.Temp);
		foreach (TileOwner tileOwner in tiles)
		{
			for (byte dir = 0; dir < PFR.Horizontal.MaxDir(); dir++)
			{
				PFTile tile = GetToTile(tileOwner.tile, dir);
				if (tile.IsValid())
				{
					if (tileOwners.TryGetValue(Index(tile), out TileOwner other))
					{
						if (tileOwner.botID != other.botID)
						{
							// Contested location, essentially
							botBorders.Add(tileOwner.botID, tileOwner.tile);
							break;
						}
					}
					else
					{
						botBorders.Add(tileOwner.botID, tileOwner.tile);
						break;
					}
				}
			}
		}
	}*/

	public void SetPathFindingBorder(Entity entity, PFR pfr, bool outFacing)
	{
		DynamicBuffer<OutBorder> border = entity.Buffer<OutBorder>();
		DynamicBuffer<PFTile> tilesTaken = entity.Buffer<TileTaken>().Reinterpret<PFTile>();
		foreach (PFTile check in tilesTaken)
		{
			for (byte dir = 0; dir < pfr.MaxDir(); dir++)
			{
				PFTile tile = GetToTile(check, dir);

				// Border could include the invalid tiles?
				if (tile.IsValid() && !IsEntity(tile, entity))
				{
					if (outFacing)
					{
						border.Add(new OutBorder { node = new PFNode(check, dir, (byte)pfr) });
					}
					else
					{
						// Assumes symmetry in the PFR rule...
						for (byte inDir = 0; inDir < pfr.MaxDir(); inDir++)
						{
							if (GetToTile(tile, inDir).Equals(check))
							{
								border.Add(new OutBorder { node = new PFNode(tile, inDir, (byte)pfr) });
								break;
							}
						}
					}
				}
			}
		}
	}

	public float GuessDistanceSq(PFTile a, PFTile b)
	{
		// Every node to node path is saved is the idea
		return math.distancesq(WorldPosition(a), WorldPosition(b));
	}



	public void Init()
	{
		linkedTiles = new NativeHashMap<int, PFTile>(1000, Allocator.Persistent); // The PFTiles on the left hand side are invalid
		entities = new NativeHashMap<int, Entity>(1000, Allocator.Persistent);
		tileOwners = new NativeHashMap<int, TileOwner>(1000, Allocator.Persistent);
		allTileOwners = new NativeMultiHashMap<int, ContestedTile>(1000, Allocator.Persistent);
		tileOwnerRenderers = new NativeHashMap<int, Entity>(1000, Allocator.Persistent);
		sectionSize = new NativeList<int3>(Allocator.Persistent);
		sectionStartIndex = new NativeList<int>(Allocator.Persistent);
		sectionStartIndex.Add(0);
		sectionRender = new NativeList<Entity>(Allocator.Persistent);
		connectionPositions = new NativeList<float3>(Allocator.Persistent);
		connectionDirs = new NativeList<float3>(Allocator.Persistent);
	}

	public void InitSection(Section section)
	{
		for (byte x = 0; x < section.size.x; x++)
		{
			for (byte y = 0; y < section.size.y; y++)
			{
				for (byte z = 0; z < section.size.z; z++)
				{
					/*if (y == 0)
					{
						OnAddTileWhereYIs0(new PFTile(x, 0, z, Game.map.sectionSize.Length - 1));
					}*/
					for (byte dir = 0; dir < MAX_DIRS; dir++)
					{
						for (byte connectionType = 0; connectionType < (byte)PFR.MAX_CONNECTION_TYPES; connectionType++)
						{
							connectionPositions.Add(section.GetConnectionPoint(dir, x, y, z) + ((PFR)connectionType).Offset());
							connectionDirs.Add(math.normalize(section.GetWorldNormal(dir, x, y, z) + ((PFR)connectionType).DirOffset()));
						}
					}
				}
			}
		}
	}

	// Only called on main map:
	public void Dispose()
	{
		linkedTiles.Dispose();
		entities.Dispose();
		tileOwners.Dispose();
		allTileOwners.Dispose();
		tileOwnerRenderers.Dispose();
		sectionSize.Dispose();
		sectionStartIndex.Dispose();
		sectionRender.Dispose();
		connectionPositions.Dispose();
		connectionDirs.Dispose();
		for (int i = 0; i < Bot.bots.Count; i++)
		{
			Bot.bots[i].botMapInfo.border.Dispose();
		}
	}
}

public enum PFR : byte
{
	All,
	Horizontal,
	BeltNormal,
	BeltUp,
	BeltDown,
	MAX_CONNECTION_TYPES
}

public static class PFRExtensions
{
	public static byte MaxDir(this PFR pfr)
	{
		switch (pfr)
		{
			case PFR.All: return 6;
			case PFR.Horizontal: return 4;
			case PFR.BeltNormal: return 4;
			case PFR.BeltUp: return 4;
			case PFR.BeltDown: return 4;
			default: return 0; // JOB_ASSERT Assert.Fail("Invalid: " + pfr);
		}
	}

	public static float3 Offset(this PFR pfr)
	{
		switch (pfr)
		{
			case PFR.BeltNormal: return new float3(0, -MapInfo.TILE_HEIGHT * 0.4f, 0);
			case PFR.BeltUp: return new float3(0, MapInfo.TILE_HEIGHT * 0.1f, 0);
			case PFR.BeltDown: return new float3(0, -MapInfo.TILE_HEIGHT * 0.9f, 0);
			default: return float3.zero;
		}
	}

	public static float3 DirOffset(this PFR pfr)
	{
		switch (pfr)
		{
			case PFR.BeltUp: return new float3(0, MapInfo.TILE_HEIGHT * 0.8f, 0);
			case PFR.BeltDown: return new float3(0, -MapInfo.TILE_HEIGHT * 0.8f, 0);
			default: return float3.zero;
		}
	}

	public static void SetNextPFR(this PFR pfr, NativeList<byte> outPFR)
	{
		outPFR.Clear(); // Capacity doesn't change, so this should be free
		switch (pfr)
		{
			case PFR.BeltNormal: outPFR.Add((byte)PFR.BeltNormal); outPFR.Add((byte)PFR.BeltUp); outPFR.Add((byte)PFR.BeltDown); return;
			case PFR.BeltUp: outPFR.Add((byte)PFR.BeltNormal); return;
			case PFR.BeltDown: outPFR.Add((byte)PFR.BeltNormal); return;
			default: outPFR.Add((byte)pfr); return;
		}
	}
}

public struct PFNode : IEquatable<PFNode>//, IFormattable
{
	public const byte DOWN = 4;
	public const byte UP = 5;

	public PFTile tile;
	public byte dir;
	public byte pfr;

	public static PFNode Invalid { get => new PFNode(PFTile.Invalid, 0, 0); }

	public PFNode(PFTile tile, byte dir, byte connectionType)
	{
		this.tile = tile;
		this.dir = dir;
		this.pfr = connectionType;
	}

	public bool Equals(PFNode other)
	{
		return tile.Equals(other.tile) && dir == other.dir && pfr == other.pfr;
	}

	public override string ToString()
	{
		return tile.ToString() + ", dir = " + dir + ", " + pfr;
	}
}

[Serializable]
public struct PFTile : IEquatable<PFTile>//, IFormattable
{
	public byte x;
	public byte y;
	public byte z;
	public int section;

	public static PFTile Invalid { get => new PFTile(0, 0, 0, -1); }

	public PFTile(byte x, byte y, byte z, int section)
	{
		this.x = x;
		this.y = y;
		this.z = z;
		this.section = section;
	}

	public List<PFNode> GetAllAdjacentPFRs(PFR pfr)
	{
		List<PFNode> nodes = new List<PFNode>();
		for (byte dir = 0; dir < pfr.MaxDir(); dir++)
		{
			nodes.Add(new PFNode(this, dir, (byte)pfr));
		}
		return nodes;
	}

	// This should only be being used on the main thread...
	public override int GetHashCode()
	{
		return Game.map.Index(this);
	}

	public bool Equals(PFTile other)
	{
		return x == other.x && y == other.y && z == other.z && section == other.section;
	}

	public bool IsValid()
	{
		return section != -1;
	}

	public override string ToString()
	{
		return "{" + x + ", " + y + ", " + z + " | " + section + "}";
	}
}