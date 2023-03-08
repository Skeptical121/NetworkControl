using System;
using System.Collections.Generic;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

public enum Resource : int
{
	INVALID,
	NCO,
}
/*
public static class ResourceExt
{
	private static Mesh[] beltObjectMesh;
	private static Material[,] beltObjectMat;
	public static void Init()
	{
		beltObjectMesh = new Mesh[NumResources()];
		beltObjectMat = new Material[NumResources(), Bot.MAX_BOTS + 1];
	}

	public static void LoadResource(this Resource resource, Mesh mesh, string mat)
	{
		beltObjectMesh[(int)resource] = mesh;
		Material material = (Material)Resources.Load(mat);
		for (int i = 0; i < Bot.MAX_BOTS; i++)
		{
			Material m = new Material(material);
			m.color = Bot.GetControlColor(i);
			beltObjectMat[(int)resource, i] = m;
		}
		Material mLast = new Material(material);
		mLast.color = Bot.GetControlColor(Bot.INVALID);
		beltObjectMat[(int)resource, Bot.MAX_BOTS] = mLast;
	}

	public static Mesh GetMesh(this Resource resource)
	{
		Assert.IsTrue(resource != Resource.INVALID);
		return beltObjectMesh[(int)resource];
	}

	public static Material GetMat(this Resource resource, int botIndex)
	{
		Assert.IsTrue(resource != Resource.INVALID);
		return beltObjectMat[(int)resource, botIndex];
	}

	// Includes INVALID
	public static int NumResources()
	{
		return 2;
	}
}*/

// Resources do not contain pathfinding information.
public struct ResourceInfo
{
	// public readonly Resource resType;
	public BotEntity owner;
	// public float spawnTime;
	// public Entity spawnLocation;
	public float amount;
	public Entity renderEntity;
	public float length; // More of a diameter?

	// public int ResId { get => (int)resType; }

	public const float STANDARD_LENGTH = MapInfo.TILE_LENGTH * 0.4f;

	public static ResourceInfo Invalid { get => new ResourceInfo(BotEntity.Invalid, Entity.Null, 0f); }

	public ResourceInfo(BotEntity owner, Entity renderEntity, float length)
	{
		this.owner = owner;
		amount = 1f;
		// this.spawnTime = spawnTime;
		// this.spawnLocation = spawnLocation;
		this.renderEntity = renderEntity;
		this.length = length;
	}

	/*public float GetAmount(float endTime, Entity endLocation, ComponentDataFromEntity<CenterTile> centerTileFromEntity)
	{
		if (spawnLocation == endLocation || !centerTileFromEntity.HasComponent(spawnLocation) || !centerTileFromEntity.HasComponent(endLocation))
			return 0;
		else
			return (endTime - spawnTime) * math.distance(centerTileFromEntity[spawnLocation].pos, centerTileFromEntity[endLocation].pos); // Time * absolute distance
	}*/

	public bool IsInvalid()
	{
		return owner.id == Bot.INVALID;
	}

	/*public void DestroyGameObject()
	{
		if (gameRep != null)
		{
			gameRep.SetActive(false);
			Game.unusedResourceRailObjects[ResId].Add(gameRep);
		}
	}

	public void UpdateGameObject(BezierCurve curve, float t)
	{
		if (gameRep == null)
		{
			if (Game.unusedResourceRailObjects[ResId].Count > 0)
			{
				gameRep = Game.unusedResourceRailObjects[ResId][Game.unusedResourceRailObjects[ResId].Count - 1];
				Game.unusedResourceRailObjects[ResId].RemoveAt(Game.unusedResourceRailObjects[ResId].Count - 1);
				gameRep.SetActive(true);
			}
			else
			{
				gameRep = UnityEngine.Object.Instantiate(ResourceExt.resourceObjectResources[ResId]);
			}
			ParticleSystem.MainModule main = gameRep.transform.Find("ConveyorBeltObject").GetComponent<ParticleSystem>().main;
			main.startColor = Bot.GetControlColor(ownerID);
		}
		gameRep.transform.rotation = Quaternion.LookRotation(curve.GetDir(t));
		gameRep.transform.position = curve.GetPos(t);
	}*/
}

/*public class ResourceRail : IComparable<ResourceRail>
{
	public const int RESOURCES_EXTRA_BUFFER = 3;

	readonly float OBJ_LENGTH;
	float LENGTH;
	readonly float SPEED;

	public Node parentNode;

	int orderIndex = 0;
	readonly BezierCurve curve; // Render purposes only...
	public float distanceBefore = 0; // Animation purposes only

	int front = 0;
	int back = -1; // No items
	ResourceRail backResourceFrom = null;
	BeltResource[] resources;
	List<ResourceRail> allNext = new List<ResourceRail>();

	ResourceRailContainer parent;

	int nextOffset = 0; // Evenly distributes among the next choices

	public ResourceRail(Node parentNode, ResourceRailContainer parent, float objLength, float length, float speed, BezierCurve curve)
	{
		this.parentNode = parentNode;
		this.parent = parent;

		OBJ_LENGTH = objLength;
		LENGTH = length;
		SPEED = speed;
		this.curve = curve;
		resources = new BeltResource[(int)(LENGTH / OBJ_LENGTH) + RESOURCES_EXTRA_BUFFER];
		Game.resourceRails[orderIndex].Add(this); // orderIndex just starts off at 0...
	}

	public int NumNext()
	{
		return allNext.Count;
	}

	public void CombineNext(ResourceRail next)
	{
		Assert.IsTrue(allNext.Count == 0 && next.allNext.Count == 0 && IsEmpty() && next.IsEmpty());
		LENGTH += next.LENGTH;
		Game.resourceRails[next.orderIndex].Remove(next);
		resources = new BeltResource[(int)(LENGTH / OBJ_LENGTH) + RESOURCES_EXTRA_BUFFER];
	}

	public void ConnectNext(ResourceRail next)
	{
		allNext.Add(next);
		next.Propegate(distanceBefore + LENGTH, orderIndex + 1);
	}

	// Loops will cause an infinite loop here right now... could store the belts in a set...
	void Propegate(float distance, int setOrderIndex)
	{
		distanceBefore = distance; // Hmm
								   // Debug.Log("Offset: " + offset.y);
		if (setOrderIndex > orderIndex)
		{
			if (orderIndex != -1)
			{
				Game.resourceRails[orderIndex].Remove(this);
			}
			orderIndex = setOrderIndex;
			if (orderIndex >= Game.resourceRails.Count)
				Game.resourceRails.Add(new HashSet<ResourceRail>());
			Game.resourceRails[orderIndex].Add(this);
		}
		foreach (ResourceRail next in allNext)
		{
			next.Propegate(distanceBefore + LENGTH, orderIndex + 1);
		}
	}

	public bool IsEmpty()
	{
		return back + 1 == front || (back == resources.Length - 1 && front == 0);
	}

	public bool CanAddResource()
	{
		return IsEmpty() || resources[back].pos >= OBJ_LENGTH;
	}

	public bool AddResource(ResourceInfo resInfo)
	{
		if (CanAddResource())
		{
			back = (back + 1) % resources.Length;
			// Assert.IsFalse(IsEmpty(), "Too many resources on belt?");
			resources[back].pos = 0;
			resources[back].resInfo = resInfo;
			backResourceFrom = null;
			return true;
		}
		return false;
	}

	// Resource must have reached as far as it can go...
	// Assumes CanTakeResource has been called!!
	public void TakeResourceUnchecked()
	{
		resources[front].resInfo.DestroyGameObject();
		front = (front + 1) % resources.Length;
	}

	public bool CanTakeResource(out ResourceInfo res)
	{
		if (!IsEmpty() && resources[front].pos >= LENGTH)
		{
			res = resources[front].resInfo;
			return true;
		}
		res = new ResourceInfo();
		return false;
	}

	// RenderTick is only called if the rail is visible
	public void RenderTick()
	{
		parent.RenderTick();
		for (int i = front; !IsEmpty() && ((back >= front && i >= front && i <= back) || (back < front && (i >= front || i <= back))); i = (i + 1) % resources.Length)
		{
			resources[i].resInfo.UpdateGameObject(curve, curve.GetMapped(resources[i].pos / LENGTH));
		}
	}

	public void Tick()
	{
		if (IsEmpty())
			return; // Nothing to tick...


		int n = 0;
		// Front to back...
		for (int i = front; !IsEmpty() && ((back >= front && i >= front && i <= back) || (back < front && (i >= front || i <= back))); i = (i + 1) % resources.Length)
		{
			if (n++ >= resources.Length)
			{
				Debug.LogError("Infinite loop???? " + i + " .. " + front + ", " + back + " " + resources.Length);
				return;
			}

			resources[i].pos += SPEED;

			if (i == front)
			{
				if (resources[i].pos >= LENGTH - OBJ_LENGTH)
				{
					if (allNext.Count == 0)
					{
						if (resources[i].pos >= LENGTH)
							resources[i].pos = LENGTH;
					}
					else
					{
						ResourceRail next = allNext[nextOffset];

						float posWas = resources[i].pos;
						if (!next.IsEmpty() && next.resources[next.back].pos - OBJ_LENGTH < resources[i].pos - LENGTH)
						{
							if (next.backResourceFrom == this)
								resources[i].pos = next.resources[next.back].pos - OBJ_LENGTH + LENGTH;
							else
								resources[i].pos = Mathf.Min(resources[i].pos, Mathf.Max(LENGTH, next.resources[next.back].pos - OBJ_LENGTH + LENGTH));
						}

						if (resources[i].pos < 0)
						{
							Debug.LogError("Why is pos < 0 " + posWas);
						}
						// Assert.IsTrue(resources[i].pos >= 0, "pos can not be less than 0!");
						

						// Very important that this uses greater than, because intersections cause resources to wait at exactly LENGTH
						if (resources[i].pos > LENGTH)
						{
							next.back = (next.back + 1) % next.resources.Length;
							// Assert.IsFalse(next.IsEmpty(), "Too many resources on belt? " + next.back + ", " + next.resources.Length + ", " + next.front);
							next.resources[next.back].pos = resources[i].pos - LENGTH; // This is one reason that it is important that the order of updates is done backwards...
							next.resources[next.back].resInfo = resources[i].resInfo;
							next.backResourceFrom = this;

							// Assert.IsTrue(next.resources[next.back].pos >= 0, "pos can not be less than 0!");

							front = (front + 1) % resources.Length;
							// Also update next offset:
							nextOffset = (nextOffset + 1) % allNext.Count;
						}
					}

				}
			}
			else
			{
				// We know that i - 1 exists here, since otherwise this would be the front
				int iMinusOne = i - 1;
				if (iMinusOne == -1)
					iMinusOne = resources.Length - 1;
				if (resources[iMinusOne].pos - OBJ_LENGTH < resources[i].pos)
					resources[i].pos = resources[iMinusOne].pos - OBJ_LENGTH;

				// This assert statement makes it take like 5x as long:

				// Assert.IsTrue(resources[i].pos >= 0, "pos can not be less than 0! " + front + ", " + back + ", " + resources[i].resInfo.type + ", " + resources[iMinusOne].resInfo.type + ", " + i + " " + iMinusOne + ", " + LENGTH + ", " + allNext.Count);
			}

		}
	}

	public int CompareTo(ResourceRail other)
	{
		return orderIndex - other.orderIndex;
	}
}*/