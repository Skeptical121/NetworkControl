using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/*
public abstract class OEntity
{
	// An entity is defined as an object that exists on one or multiple nodes, and will be rendered if that node is visible


	protected GameObject gameRep = null;
	public int id;
	protected object parents; // Parent is List<Node> OR a building
	private int numNodesVisible = 0; // -1 goes to parent to check, used in the case where parent is List<Node>.. this even works if buildings can expand into other nodes

	protected List<PFTile> tilesTaken; // Its entire purpose is setting entity refs to -1 on removal... hmm
	float constructionProgress = 0f; // 0f = purely a facade, 1f = fully built..

	// The entity should be entirely connected to the "centerTile" that it is on...

	public abstract void OnAddEntity();
	public abstract void InitRender(bool preview);

	// Assumes IsVisible = true, or is being used as a preview
	protected void CreateGameRep(GameObject resource)
	{
		Assert.IsNull(gameRep);
		gameRep = Object.Instantiate(resource);
		gameRep.GetComponent<GameRep>().entity = this;
	}
	
	// Fully unload the render
	public void DestroyRender()
	{
		Object.Destroy(gameRep);
	}

	// So there is an assumption that the first tile is the "center" tile, as well as tilesTaken != null
	public PFTile GetCenterTile()
	{
		return tilesTaken[0];
	}

	public void InstantConstruct()
	{
		constructionProgress = 1f;
	}

	protected void Construct(ResourceInfo resInfo)
	{
		// Construct building first
		if (parents is Building building && !building.IsConstructed())
		{
			building.Construct(resInfo);
		}
		else
		{
			constructionProgress = Mathf.Min(1f, constructionProgress + resInfo.amount / GetConstructionCost());
		}
	}

	protected virtual float GetConstructionCost()
	{
		return 10f;
	}

	protected bool IsConstructed()
	{
		return constructionProgress == 1f;
	}

	public bool IsVisible()
	{
		return (numNodesVisible == -1 && ((Building)parents).IsVisible()) || numNodesVisible > 0;
	}

	// Note that tiles are not copied here, so don't modify them after
	// Parents should be known already..



	public void VisibilityChange(int change)
	{
		bool wasVisible = IsVisible();
		numNodesVisible += change;
		Assert.IsTrue(numNodesVisible >= 0 && numNodesVisible <= ((List<Section>)parents).Count, "" + this + ", " + numNodesVisible + ", " + ((List<Section>)parents).Count);
		if (IsVisible() != wasVisible)
		{
			VisibilityChanged();
		}
	}

	public virtual void VisibilityChanged()
	{
		if (IsVisible())
		{
			if (gameRep == null)
			{
				InitRender(false);
			}
			else
			{
				gameRep.SetActive(true);
			}
			VisibilityIsNow(true);
		}
		else
		{
			gameRep.SetActive(false);
			VisibilityIsNow(false);
		}
	}

	protected virtual void VisibilityIsNow(bool visible)
	{
	}

	public OEntity()
	{
		id = Game.mapObject.entities.Count;
		Game.mapObject.entities.Add(this);
	}

	// Doesn't have to be called if preview = true, only if needed
	public void Add(List<PFTile> tiles, bool preview)
	{
		tilesTaken = tiles;
		if (!preview)
		{
			HashSet<Section> parents = new HashSet<Section>();
			foreach (PFTile tile in tiles)
			{
				if (!Game.map.IsEmpty(tile))
				{
					Debug.LogError("Entity was not null when setting entity! Setting " + this + " onto existing tile that had entity");
				}
				MapUpdater.SetEntity(tile, id);
				parents.Add(Game.mapObject.sections[tile.section]);
			}
			Add(parents.ToList());
		}
	}

	public void Add(Building parent)
	{
		this.parents = parent;
		parent.AddChild(this);
		numNodesVisible = -1;
		Add();
	}

	public void Add(List<Section> parents)
	{
		Assert.IsTrue(parents.Count == parents.Distinct().Count(), "Parent nodes should not have duplicates");
		this.parents = parents;
		numNodesVisible = 0;
		foreach (Section node in parents)
		{
			node.AddRenderEntity(this);
			if (node.IsVisible())
			{
				numNodesVisible++;
			}
		}
		Add();
	}

	private void Add()
	{
		OnAddEntity(); // It's important that this comes before InitRender because this might contain information that InitRender uses

		if (IsVisible())
		{
			InitRender(false);
		}
	}
}*/