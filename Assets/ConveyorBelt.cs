using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

/*
public class ConveyorBelt : OEntity
{
	public static GameObject resource;

	readonly BezierCurve curve;
	public int beltIndex = -1;

	// float LENGTH;
	// private int orderIndex = 0;
	// private float distanceBefore = 0; // Animation purposes only

	/*int front = 0;
	int back = -1; // No items
	ConveyorBelt backResourceFrom = null;
	BeltResource[] resources;
	List<ConveyorBelt> allNext = new List<ConveyorBelt>();*/
/*
	PFNode from;
	PFNode to;

	// int nextOffset = 0; // Evenly distributes among the next choices

	public ConveyorBelt(PFNode from, PFNode to, params float3[] points)
	{
		this.from = from;
		this.to = to;
		// curve = new BezierCurve(points, GetCurvePrecision(math.normalize(points[1] - points[0]), math.normalize(points[points.Length - 1] - points[points.Length - 2])));
	}

	public override void InitRender(bool preview)
	{
		CreateGameRep(resource);

		// So length is useful for the actual resolution?
		Vector2[] offsets = { new Vector2(-MapInfo.TILE_LENGTH * 0.3f, 0), new Vector2(MapInfo.TILE_LENGTH * 0.3f, 0) };
		float[] uv = { 0f, 1f };
		Vector2[] normals = { new Vector2(0, 1) };
		Mesh renderMesh = curve.CreateRenderMesh(/*GetCurvePrecision(curve.GetDir(0), curve.GetDir(1)), *//*offsets, uv, Vector3.up, false, true, normals); //  CURVE_PRECISION, Tile.TILE_LENGTH * 0.6f, 1f);

		offsets = new Vector2[] { 
			new Vector2(MapInfo.TILE_LENGTH * 0.3f, 0), 
			new Vector2(MapInfo.TILE_LENGTH * 0.3f, MapInfo.TILE_LENGTH * 0.1f), 
			new Vector2(MapInfo.TILE_LENGTH * 0.35f, MapInfo.TILE_LENGTH * 0.1f),
			new Vector2(MapInfo.TILE_LENGTH * 0.35f, -MapInfo.TILE_LENGTH * 0.1f),
			new Vector2(-MapInfo.TILE_LENGTH * 0.35f, -MapInfo.TILE_LENGTH * 0.1f),
			new Vector2(-MapInfo.TILE_LENGTH * 0.35f, MapInfo.TILE_LENGTH * 0.1f),
			new Vector2(-MapInfo.TILE_LENGTH * 0.3f, MapInfo.TILE_LENGTH * 0.1f),
			new Vector2(-MapInfo.TILE_LENGTH * 0.3f, 0) };
		uv = new float[] { 0f, 0.1f, 0.15f, 0.35f, 1.05f, 1.25f, 1.3f, 1.4f };
		normals = new Vector2[]{ Vector2.left, Vector2.up, Vector2.right, Vector2.down, Vector2.left, Vector2.up, Vector2.right };

		Mesh renderMesh2 = curve.CreateRenderMesh(/*GetCurvePrecision(curve.GetDir(0), curve.GetDir(1)), *//*offsets, uv, Vector3.up, false, true, normals);

		gameRep.GetComponent<MeshFilter>().sharedMesh = MeshCreator.CombineMeshes(false, renderMesh, renderMesh2);
	}

	public override void OnAddEntity()
	{
		// BeltMover.Queue(() => beltIndex = BeltMover.beltInfo.AddBelt(id, curve.GetTotalDistance()));

		/*if (from.tile.IsValid())
		{
			OEntity startEntity = Game.map.GetEntity(from.tile);
			if (startEntity is ConveyorBelt startC)
				BeltMover.Queue(() => BeltMover.beltInfo.ConnectBelts(startC.beltIndex, beltIndex));
			else if (startEntity is Building startB)
				startB.AddOutRail(from); // Connection will be done by the added belt
		}
		if (to.tile.IsValid() && Game.map.GetToTile(to.tile, to.dir).IsValid())
		{
			OEntity endEntity = Game.map.GetEntity(Game.map.GetToTile(to.tile, to.dir));
			if (endEntity is ConveyorBelt endC)
				BeltMover.Queue(() => BeltMover.beltInfo.ConnectBelts(beltIndex, endC.beltIndex));
			else if (endEntity is Building endB)
				endB.AddInRail(to); // Connection will be done by the added belt
		}*/
	/*}

	/*protected override void VisibilityIsNow(bool visible)
	{
		BeltMover.Queue(() => BeltMover.beltInfo.SetVisible(beltIndex, visible));
	}

	Vector2 offset = Vector2.zero;

	// Note how renderTick could be run at any framerate and produce the same results
	public void RenderTick()
	{
		Belt belt = BeltMover.beltInfo.belts[beltIndex];
		offset.y = belt.distanceBefore - Game.GetGameTime();

		int front = BeltMover.beltInfo.beltFronts[beltIndex];
		int back = BeltMover.beltInfo.beltBacks[beltIndex];

		// This operation here is kinda slow, so that's another point for trying to combine conveyor belts..
		// It should be noted that this only needs to be rendered very rarely compared to how many conveyor belts there are...
		gameRep.GetComponent<MeshRenderer>().material.SetTextureOffset("_MainTex", offset);

		//for (int i = front; !BeltMover.beltInfo.IsEmpty(front, back, belt.numBeltData) && ((back >= front && i >= front && i <= back) || (back < front && (i >= front || i <= back))); i = (i + 1) % belt.numBeltData)
		//{
		//	resources[i].resInfo.UpdateGameObject(curve, curve.GetMapped(resources[i].pos / LENGTH));
		//}
	}*/

	/*public int NumNext()
	{
		return allNext.Count;
	}

	public void CombineNext(ConveyorBelt next)
	{
		Assert.IsTrue(allNext.Count == 0 && next.allNext.Count == 0 && IsEmpty() && next.IsEmpty());
		LENGTH += next.LENGTH;
		Game.resourceRails[next.orderIndex].Remove(next);
		resources = new BeltResource[(int)(LENGTH / OBJ_LENGTH) + RESOURCES_EXTRA_BUFFER];
	}

	public void ConnectNext(ConveyorBelt next)
	{
		allNext.Add(next);
		next.Propegate(distanceBefore + LENGTH, orderIndex + 1);
	}*/
	
	// Loops will cause an infinite loop here right now... could store the belts in a set...
	/*void Propegate(float distance, int setOrderIndex)
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
				Game.resourceRails.Add(new HashSet<ConveyorBelt>());
			Game.resourceRails[orderIndex].Add(this);
		}
		foreach (ConveyorBelt next in allNext)
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

	// ..
	// Assumes CanTakeResource has been called!!
	public void TakeResourceUnchecked()
	{
		// resources[front].resInfo.DestroyGameObject();
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

	public void Tick(float tickTime)
	{
		if (IsEmpty())
			return; // Nothing to tick...

		int n = 0;
		// Front to back...
		for (int i = front; !IsEmpty() && ((back >= front && i >= front && i <= back) || (back < front && (i >= front || i <= back))); i = (i + 1) % resources.Length)
		{
			if (!IsConstructed())
			{
				// Consume everything?
				Construct(resources[i].resInfo);
				TakeResourceUnchecked();
				continue;
			}

			if (n++ >= resources.Length)
			{
				Debug.LogError("Infinite loop???? " + i + " .. " + front + ", " + back + " " + resources.Length);
				return;
			}

			resources[i].pos += GetBeltSpeed() * tickTime;

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
						ConveyorBelt next = allNext[nextOffset];

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
	}*/
// }