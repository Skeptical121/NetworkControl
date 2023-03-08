using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Entities;
using UnityEngine;
using UnityEngine.UI;

/*
public abstract class Building : OEntity
{
	public static GameObject resource;
	// Every "building" is a powered entity

	protected int ownerID = -1;
	private float ownerNCO = 0f;
	private List<OEntity> childEntities = new List<OEntity>();

	// So... either all buildings have this, or they all don't for a given type:
	// public Dictionary<Type, float> distanceToNearestBuildingOfType = new Dictionary<Type, float>();


	private int inRailIndex = 0; // So each belt is taken from evenly
	private readonly List<Entity> inRails = new List<Entity>();
	private readonly Dictionary<PFNode, Entity> inRailDictionary = new Dictionary<PFNode, Entity>();
	private int outRailIndex = 0; // So each belt is given to evenly
	private readonly List<Entity> outRails = new List<Entity>();
	private readonly Dictionary<PFNode, Entity> outRailDictionary = new Dictionary<PFNode, Entity>();

	// Updated using 
	private const float RATE_MULT = 0.5f;
	private float[] importRates = new float[ResourceExt.NumResources()];
	private float exportRate = 0;

	private float[] storedResources = new float[ResourceExt.NumResources()];
	private float[] maxResources = new float[ResourceExt.NumResources()];

	private byte height = 4;
	private GameObject buildingHud;

	// This only really needs to happen once every like 2-3 seconds or something
	public void BotControlUpdate()
	{
		for (int i = 0; i < importRates.Length; i++)
		{
			importRates[i] *= RATE_MULT;
		}
		exportRate *= RATE_MULT;
	}

	public int GetOwnerID()
	{
		return ownerID;
	}

	// Returns how much was added
	public float AddResource(ResourceInfo resInfo)
	{
		if (storedResources[resInfo.ResId] + resInfo.amount <= maxResources[resInfo.ResId])
		{
			storedResources[resInfo.ResId] += resInfo.amount;
			return resInfo.amount;
		}
		float amountAdded = maxResources[resInfo.ResId] - storedResources[resInfo.ResId];
		storedResources[resInfo.ResId] = maxResources[resInfo.ResId];
		return amountAdded;
	}

	protected float GetResource(Resource resource)
	{
		return storedResources[(int)resource];
	}

	protected void SetResourceAmountUnchecked(Resource resource, float amount)
	{
		storedResources[(int)resource] = amount;
	}

	protected void SetMaxResources(Resource resource, float amount)
	{
		maxResources[(int)resource] = amount;
		storedResources[(int)resource] = Math.Min(storedResources[(int)resource], maxResources[(int)resource]);
	}

	public List<PFNode> GetPathFindingBorder(PFR pfr, bool outFacing)
	{
		return GetPathFindingBorder(tilesTaken, (tile) => Game.map.IsEntity(tile, id), pfr, outFacing);
	}

	public static List<PFNode> GetPathFindingBorder(IEnumerable<PFTile> tilesTaken, Func<PFTile, bool> isTileEntity, PFR pfr, bool outFacing)
	{
		List<PFNode> border = new List<PFNode>();
		foreach (PFTile check in tilesTaken)
		{
			for (byte dir = 0; dir < pfr.MaxDir(); dir++)
			{
				PFTile tile = Game.map.GetToTile(check, dir);

				// Border could include the invalid tiles?
				if (tile.IsValid() && !isTileEntity(tile))
				{
					if (outFacing)
					{
						border.Add(new PFNode(check, dir, (byte)pfr));
					}
					else
					{
						// Assumes symmetry in the PFR rule...
						for (byte inDir = 0; inDir < pfr.MaxDir(); inDir++)
						{
							if (Game.map.GetToTile(tile, inDir).Equals(check))
							{
								border.Add(new PFNode(tile, inDir, (byte)pfr));
								break;
							}
						}
					}
				}
			}
		}
		return border;
	}

	public override void OnAddEntity()
	{
		Game.buildingHandler.OnAddBuilding(this);
		SetMaxResources(Resource.ELECTRICITY, 10000f);
	}

	public sealed override void InitRender(bool preview)
	{
		CreateGameRep(resource);
		gameRep.name = "Building_" + this + " " + id;
		
		gameRep.GetComponent<MeshRenderer>().material.color = GetDefaultColor();
		EntireBorderInfo entireBorder = preview ? new EntireBorderInfo(GetCenterTile(), (tile) => tile.IsValid() && tilesTaken.Contains(tile), (tile) => tile.IsValid()) :
												  new EntireBorderInfo(GetCenterTile(), (tile) => tile.IsValid() && Game.map.IsEntity(tile, id), (tile) => tile.IsValid());

		buildingHud = gameRep.transform.Find("BuildingHud").gameObject;

		Mesh mesh = entireBorder.CreateBorderedMesh(MapInfo.TILE_HEIGHT * height, -MapInfo.TILE_LENGTH * 0.3f, false);

		gameRep.GetComponent<MeshFilter>().sharedMesh = mesh;
		if (!preview)
		{
			buildingHud = buildingHud.transform.Find("Canvas").gameObject;
			gameRep.GetComponent<MeshCollider>().sharedMesh = mesh;
		}
		SetColor();
	}

	private const float MAX_NCO = 5f;

	protected abstract Color GetDefaultColor();

	public void SetOwner(int ownerID)
	{
		if (ownerID != -1)
		{
			Bot.bots[ownerID].controlled.Remove(this);
		}
		this.ownerID = ownerID;
		Bot.bots[ownerID].controlled.Add(this);
		if (IsVisible())
		{
			SetColor();
		}
	}

	public void SetColor()
	{
		Mesh mesh = gameRep.GetComponent<MeshFilter>().sharedMesh;
		int vertexCount = mesh.vertexCount;
		List<Color> colors = new List<Color>(); // Color32 might be more efficient?
		for (int i = 0; i < vertexCount; i++)
		{
			colors.Add(Bot.GetControlColor(ownerID));
		}
		mesh.SetColors(colors);
	}

	private void AttemptImportResource(Entity rail)
	{
		if (Game.STOP_HEAVY_DUTY_SIMULATION)
			return;
		if (World.Active.GetExistingSystem<BeltMoveSystem>().AttemptTakeResource(rail, out ResourceInfo resInfo))
		{
			if (resInfo.ownerID == ownerID)
			{
				ownerNCO = Mathf.Min(MAX_NCO, ownerNCO + resInfo.amount);
			}
			else if (resInfo.ownerID != -1)
			{
				ownerNCO -= resInfo.amount;
				if (ownerNCO < 0)
				{
					SetOwner(resInfo.ownerID);
					ownerNCO = -ownerNCO;
				}
			}
			AddResource(resInfo);
			importRates[resInfo.ResId] += resInfo.amount;
		}
	}

	private void AttemptExportResource(Entity rail, ResourceInfo resInfo)
	{
		if (Game.STOP_HEAVY_DUTY_SIMULATION)
			return;
		if (World.Active.GetExistingSystem<BeltMoveSystem>().AttemptCreateBeltResource(rail, resInfo))
		{
			exportRate += resInfo.amount;
			UseResourceUnchecked(resInfo);
		}
	}

	public void AddChild(OEntity entity)
	{
		childEntities.Add(entity);
	}

	public override void VisibilityChanged()
	{
		base.VisibilityChanged();
		foreach (OEntity entity in childEntities)
		{
			entity.VisibilityChanged();
		}
	}

	/*public ConveyorBelt AddInRail(PFNode obi)
	{
		if (!inRailDictionary.TryGetValue(obi, out ConveyorBelt rail))
		{
			PFTile to = Game.map.GetToTile(obi.tile, obi.dir);
			rail = new ConveyorBelt(obi, new PFNode(new PFTile(0, 0, 0, -1), 0, 0), Game.map.ConnectionPoint(obi), Game.map.ConnectionPoint(obi) + Game.map.ConnectionNormal(obi) * 0.2f * MapInfo.TILE_LENGTH,
				Game.map.WorldPosition(to, PFR.BeltNormal));
			rail.Add(this);

			inRailDictionary.Add(obi, rail);
			inRails.Add(rail);
		}
		return rail;
	}

	public ConveyorBelt AddOutRail(PFNode obi)
	{
		if (!outRailDictionary.TryGetValue(obi, out ConveyorBelt rail))
		{
			rail = new ConveyorBelt(new PFNode(new PFTile(0, 0, 0, -1), 0, 0), obi, Game.map.WorldPosition(obi.tile, PFR.BeltNormal),
				Game.map.ConnectionPoint(obi) - Game.map.ConnectionNormal(obi) * 0.2f * MapInfo.TILE_LENGTH, Game.map.ConnectionPoint(obi));
			rail.Add(this);

			outRailDictionary.Add(obi, rail);
			outRails.Add(rail);
		}
		return rail;
	}*/

/*	public void AddInput(Entity belt)
	{
		inRails.Add(belt);
	}

	public void AddOutput(Entity belt)
	{
		outRails.Add(belt);
	}

	public abstract ResourceInfo GetResourceExported(bool includeMinimum);

	public virtual void Tick(float tickTime)
	{
		if (IsVisible())
		{
			/*buildingHud.transform.Find("Panel").GetComponent<Image>().color = Bot.GetControlColor(ownerID);
			buildingHud.transform.Find("Text").GetComponent<Text>().text = this + "\n" +
				"Coal = " + GetResource(Resource.COAL) + "/" + maxResources[(int)Resource.COAL] + "\n" +
				("Owner = " + ownerID + " (" + ownerNCO + ")\n");*/
		/*}

		if (inRails.Count > 0)
		{
			inRailIndex = (inRailIndex + 1) % inRails.Count;
			for (int i = 0; i < inRails.Count; i++)
			{
				AttemptImportResource(inRails[(i + inRailIndex) % inRails.Count]);
			}
		}


		// Keep trying production:
		while (ProductionTick() == Produced.PRODUCED) {}

		if (outRails.Count > 0)
		{
			// Attempt export:
			if (GetResourceExported(false).resType != Resource.INVALID)
			{

				outRailIndex = (outRailIndex + 1) % outRails.Count;
				for (int i = 0; i < outRails.Count; i++)
				{
					if (!HasResource(GetResourceExported(true)))
						break;

					AttemptExportResource(outRails[(i + outRailIndex) % outRails.Count], GetResourceExported(false));
				}
			}
		}
	}

	protected abstract Produced ProductionTick();

	protected Produced StandardProductionTick(ResourceInfo use, ResourceInfo create)
	{
		if (use.resType == Resource.INVALID || HasResource(use))
		{
			if (AttemptAddResource(create))
			{
				if (use.resType != Resource.INVALID)
					UseResourceUnchecked(use);
				return Produced.PRODUCED;
			}
			return Produced.COULD_HAVE_PRODUCED;
		}
		return Produced.NOT_PRODUCED;
	}

	private bool HasResource(ResourceInfo resInfo)
	{
		return storedResources[resInfo.ResId] >= resInfo.amount;
	}

	private void UseResourceUnchecked(ResourceInfo resInfo)
	{
		storedResources[resInfo.ResId] -= resInfo.amount;
	}

	private bool AttemptAddResource(ResourceInfo resInfo)
	{
		if (storedResources[resInfo.ResId] + resInfo.amount <= maxResources[resInfo.ResId])
		{
			storedResources[resInfo.ResId] += resInfo.amount;
			return true;
		}
		return false;
	}

	/*public Tile GetClosestTile(Building other)
	{
		float shortestDistSquared = float.PositiveInfinity;
		Tile closest = null;
		foreach (Tile tile in tilesTaken)
		{
			float distSquared = Vector3.SqrMagnitude(tile.GetWorldPosition() - other.GetCenterTile().GetWorldPosition());
			if (distSquared < shortestDistSquared)
			{
				shortestDistSquared = distSquared;
				closest = tile;
			}
		}
		return closest;
	}*/

	/*public List<PathFindingNode> GetAllOutBorderInfo(PFR pfr)
	{
		List<PathFindingNode> entireBorderInfo = new List<PathFindingNode>();
		foreach (Tile check in tilesTaken)
		{
			// Only 2 states for now...
			foreach (int index in pfr == PFR.CONVEYOR_BELT_NORMAL ? check.GetParent().GetAllHorizontalAdjacentIndices() : check.GetParent().GetAllAdjacentIndices())
			{
				Tile tile = check.GetParent().GetAdjacentTile(index, check.pos);

				if (tile == null || tile.entity != this)
				{
					entireBorderInfo.Add(new PathFindingNode(pfr, check, index));
				}
			}
		}
		return entireBorderInfo;
	}*/

	/*public bool IsValid(Tile center, out List<Tile> tiles)
	{
		int radius = 4;

		tiles = null;
		if (center == null)
			return true;

		HashSet<Tile> tilesTaken = new HashSet<Tile>();

		// So obviously there is at least one tile
		if (!AttemptAddToSet(tilesTaken, center))
			return false;

		List<Tile> tilesTakenSoFar;
		for (int i = 1; i < radius; i++)
		{
			tilesTakenSoFar = new List<Tile>(tilesTaken);
			foreach (Tile tile in tilesTakenSoFar)
			{
				foreach (int index in tile.GetParent().GetAllHorizontalAdjacentIndices())
				{
					if (!AttemptAddToSet(tilesTaken, tile.GetParent().GetAdjacentTile(index, tile.pos)))
						return false;
				}
			}
		}

		for (int i = 1; i < radius; i++)
		{
			tilesTakenSoFar = new List<Tile>(tilesTaken);
			foreach (Tile tile in tilesTakenSoFar)
			{
				if (tile != center)
				{
					foreach (int index1 in tile.GetParent().GetAllHorizontalAdjacentIndices())
					{
						Tile adjTile = tile.GetParent().GetAdjacentTile(index1, tile.pos);
						if (adjTile != null)
						{
							int numAdj = 0;
							foreach (int index2 in adjTile.GetParent().GetAllHorizontalAdjacentIndices())
							{
								if (tilesTaken.Contains(adjTile.GetParent().GetAdjacentTile(index2, adjTile.pos)))
								{
									numAdj++;
								}
							}
							if (numAdj == 2)
							{
								if (!AttemptAddToSet(tilesTaken, adjTile))
									return false;
							}
						}
					}
				}
			}
		}


		for (int i = 1; i < height; i++)
		{
			tilesTakenSoFar = new List<Tile>(tilesTaken);
			foreach (Tile tile in tilesTakenSoFar)
			{
				if (!AttemptAddToSet(tilesTaken, tile.GetParent().GetAdjacentTile(tile.GetParent().GetAboveIndex(), tile.pos)))
					return false;
			}
		}

		tiles = tilesTaken.ToList();
		return true;
	}*/
/* }

public enum Produced
{
	NOT_PRODUCED,
	COULD_HAVE_PRODUCED,
	PRODUCED
}*/