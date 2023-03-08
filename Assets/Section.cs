using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;
using System;
using Unity.Mathematics;
using Unity.Entities;
using Unity.Rendering;

/*public class CoalSpawn
{
	public static GameObject resource;
	public GameObject gameRep;

	public PFTile tile;
	public float amount;
	public CoalSpawn(PFTile tile, float amount)
	{
		this.tile = tile;
		this.amount = amount;
		Assert.IsNull(gameRep);
		gameRep = UnityEngine.Object.Instantiate(resource);
		gameRep.transform.position = Game.map.WorldPosition(tile);
	}*/

	/*public override void InitRender(bool preview)
	{
		CreateGameRep(resource);
		gameRep.transform.position = Game.map.WorldPosition(tile);
	}

	public override void OnAddEntity()
	{
		foreach (Section parent in (List<Section>)parents)
		{
			parent.coalSpawns.Add(this);
		}
	}*/
//}

[Serializable]
public abstract class Section
{
	public const int NODE_LAYER = 1 << 8;
	public const int WALL_LAYER = 1 << 10;

	public const byte WALL_HEIGHT = 20;
	public static GameObject resource;
	public static Material sectionMat;

	// [NonSerialized] public List<CoalSpawn> coalSpawns;

	[NonSerialized] public GameObject gameRep = null;

	public List<Side> unlinked = new List<Side>();
	public List<Side> linked = new List<Side>();

	// [NonSerialized] private bool visible = false;
	// [NonSerialized] public HashSet<Section> visibleNodesFromThisNode;
	// [NonSerialized] private List<OEntity> renderEntities; // This should NOT be used for simulation

	public int3 size;
	private int id;

	public Section(int3 size)
	{
		this.size = size;
	}

	public int GetID()
	{
		return id;
	}

	public abstract bool CanAdd();
	public abstract void AddSides();
	public abstract RenderMesh CreateMesh();

	public void Add()
	{
		// If a node is added during the game, obviously a visibility check needs to be done against the added node and every other node.
		// However, the only additional visibilities are from what is visible to the added node... so the amount of checks there is pretty trivial

		id = Game.mapObject.OnAddSection(this);
		AddSides();
		Init();
	}

	public void Init()
	{
		// coalSpawns = new List<CoalSpawn>();
		Assert.IsNull(gameRep);
		gameRep = UnityEngine.Object.Instantiate(resource);
		gameRep.name = "Section " + id;
		gameRep.transform.Find("SectionCollider").GetComponent<MeshCollider>().sharedMesh = CreateColliderMesh();
		// AddMesh();
		// SetVisible(false);
		Game.mapObject.InitSection(this);
		// renderEntities = new List<OEntity>();
		// visibleNodesFromThisNode = new HashSet<Section>();
		// visibleNodesFromThisNode.Add(this);
	}

	public float3 GetConnectionPoint(byte dir, byte x, byte y, byte z)
	{
		switch (dir)
		{
			case 0: return GetWorldPosition(x * 2, y * 2 + 1, z * 2 + 1);
			case 1: return GetWorldPosition(x * 2 + 2, y * 2 + 1, z * 2 + 1);
			case 2: return GetWorldPosition(x * 2 + 1, y * 2 + 1, z * 2);
			case 3: return GetWorldPosition(x * 2 + 1, y * 2 + 1, z * 2 + 2);
			case 4: return GetWorldPosition(x * 2 + 1, y * 2, z * 2 + 1);
			case 5: return GetWorldPosition(x * 2 + 1, y * 2 + 2, z * 2 + 1);
			default: Assert.Fail("Invalid: " + dir); return new float3(0, 0, 0);
		}
	}
	public abstract float3 GetWorldPosition(int edgeX, int edgeY, int edgeZ);
	public abstract float3 GetWorldNormal(byte dir, byte x, byte y, byte z);



	// It is assumed that renderEntity has just been added...
	//public void AddRenderEntity(OEntity renderEntity)
	//{
	//	renderEntities.Add(renderEntity);
	//}


	public abstract bool InBounds(float3 worldPosition);
	public abstract Mesh CreateColliderMesh();

	protected void AddSide(byte sideIndex, byte length, Vector3 start, Vector3 normal, Vector3 tangent, byte dir)
	{
		unlinked.Add(new Side(length, start, 0, new Side.ConstantSideInfo(normal, tangent, sideIndex, dir)));
	}

	public Side LinkSide(Side side, byte start, byte width)
	{
		unlinked.Remove(side);
		Side newSide = new Side(width, side.Get(start), (byte)(side.startIndex + start), side.sideInfo);
		linked.Add(newSide);
		if (start == 0 && start + width == side.numTiles)
			return newSide;

		if (start == 0)
		{
			unlinked.Add(new Side((byte)(side.numTiles - width), side.Get(width), (byte)(side.startIndex + width), side.sideInfo));
		}
		else if (start + width == side.numTiles)
		{
			unlinked.Add(new Side(start, side.Get(0), side.startIndex, side.sideInfo));
		}
		else
		{
			unlinked.Add(new Side(start, side.Get(0), side.startIndex, side.sideInfo));
			unlinked.Add(new Side((byte)(side.numTiles - (start + width)), side.Get(start + width), (byte)(side.startIndex + start + width), side.sideInfo));
		}
		return newSide;
	}

	public static void LinkSides(Section a, Side aSide, byte aOffset, Section b, Side bSide, byte bOffset, byte width)
	{
		aSide = a.LinkSide(aSide, aOffset, width);
		bSide = b.LinkSide(bSide, bOffset, width);
		LinkSides(a, aSide, b, bSide);
	}

	public static void LinkSides(Section a, Side aSide, Section b, Side bSide)
	{
		Assert.IsEqual(aSide.numTiles, bSide.numTiles);
		byte minHeight = (byte)Mathf.Min(a.size.y, b.size.y);

		bool sameOrder = Quaternion.Angle(Quaternion.LookRotation(aSide.sideInfo.tangent), Quaternion.LookRotation(bSide.sideInfo.tangent)) < Side.ANGLE_TOLERANCE;
		for (byte aIndex = 0; aIndex < aSide.numTiles; aIndex++)
		{
			int bIndex = sameOrder ? aIndex : bSide.numTiles - aIndex - 1;
			for (byte y = 0; y < minHeight; y++)
			{
				Game.map.linkedTiles.TryAdd(
					Game.map.Index(new PFNode(a.GetTile(aSide.sideInfo.sideIndex, y, (byte)(aSide.startIndex + aIndex), aSide.sideInfo.dir == 2 || aSide.sideInfo.dir == 3), aSide.sideInfo.dir, 0)),
					b.GetTile(bSide.sideInfo.sideIndex, y, (byte)(bSide.startIndex + bIndex), bSide.sideInfo.dir == 2 || bSide.sideInfo.dir == 3));

				Game.map.linkedTiles.TryAdd(
					Game.map.Index(new PFNode(b.GetTile(bSide.sideInfo.sideIndex, y, (byte)(bSide.startIndex + bIndex), bSide.sideInfo.dir == 2 || bSide.sideInfo.dir == 3), bSide.sideInfo.dir, 0)), 
					a.GetTile(aSide.sideInfo.sideIndex, y, (byte)(aSide.startIndex + aIndex), aSide.sideInfo.dir == 2 || aSide.sideInfo.dir == 3));
			}
		}
		a.UpdateWallMesh();
		b.UpdateWallMesh();
	}

	private PFTile GetTile(byte x, byte y, byte z, bool reverseXZ)
	{
		if (!reverseXZ)
			return new PFTile(x, y, z, id);
		else
			return new PFTile(z, y, x, id);
	}

	public abstract Vector3 GetCenter();

	public void UpdateWallMesh()
	{
		EntireBorderInfo ebi = new EntireBorderInfo(new PFTile(0, 0, 0, id), 
			(tile) => tile.IsValid() && tile.section == id && tile.y == 0,
			(tile) => !tile.IsValid() && tile.y == 0);

		// A little bit of padding here helps immensely with the visibility check
		Mesh mesh = ebi.CreateBorderedMesh(MapInfo.TILE_HEIGHT * WALL_HEIGHT, 0.0f, true);
		// Mesh mesh2 = CreatedBorderedMesh(ebi, 0f, false);
		gameRep.transform.Find("InnerWall").GetComponent<MeshCollider>().sharedMesh = mesh;
		gameRep.transform.Find("InnerWall").GetComponent<MeshFilter>().sharedMesh = mesh;

		// A little bit of padding here helps immensely with the visibility check
		mesh = ebi.CreateBorderedMesh(MapInfo.TILE_HEIGHT * WALL_HEIGHT, 0.0f, false);
		// Just have them seperate for now...
		
		gameRep.transform.Find("OuterWall").GetComponent<MeshCollider>().sharedMesh = mesh;
		gameRep.transform.Find("OuterWall").GetComponent<MeshFilter>().sharedMesh = mesh;
	}

	/*
	public bool IsVisible()
	{
		return visible;
	}

	public void SetVisible(bool visible)
	{
		this.visible = visible;
		gameRep.GetComponent<MeshRenderer>().enabled = visible;
		gameRep.transform.Find("InnerWall").GetComponent<MeshRenderer>().enabled = visible;
		gameRep.transform.Find("OuterWall").GetComponent<MeshRenderer>().enabled = visible;
		foreach (OEntity renderEntity in renderEntities)
		{
			renderEntity.VisibilityChange(visible ? 1 : -1);
		}
	}*/

	public PFTile GetClosestTileWhereYis0(float3 pos)
	{
		float closestDistSq = float.PositiveInfinity;
		PFTile closestTile = PFTile.Invalid;
		for (byte x = 0; x < size.x; x++)
		{
			for (byte z = 0; z < size.z; z++)
			{
				PFTile tile = new PFTile(x, 0, z, id);
				float distsq = math.distancesq(pos, Game.map.WorldPosition(tile));
				if (distsq < closestDistSq)
				{
					closestDistSq = distsq;
					closestTile = tile;
				}
			}
		}
		return closestTile;
	}

	public PFNode GetClosestNodeWhereYis0(float3 pos, PFR pfr)
	{
		PFTile tile = GetClosestTileWhereYis0(pos);

		float closestDistSq = float.PositiveInfinity;
		PFNode closestNode = PFNode.Invalid;
		foreach (PFNode node in tile.GetAllAdjacentPFRs(PFR.Horizontal))
		{
			float distsq = math.distancesq(pos, Game.map.ConnectionPoint(node));
			if (distsq < closestDistSq)
			{
				closestDistSq = distsq;
				closestNode = node;
			}
		}
		closestNode.pfr = (byte)pfr;
		return closestNode;
	}

	// Create a list of corners... (with normals saved)

	// This method could be more efficient... but hmm

	// Then from there it's actually quite easy to determine what's going on...
	// Corner or edge tile will work
	/*public static EntireBorderInfo GetEntireBorder(Tile startTile, Func<Tile, bool> insideCondition, Func<BorderInfo, bool> validBorderCondition = null)
	{

		EntireBorderInfo entireBorderInfo = new EntireBorderInfo();

		// Border Info does not risk getting created more than once I believe
		HashSet<Tile> tilesToCheck = new HashSet<Tile>();
		HashSet<Tile> tilesChecked = new HashSet<Tile>();
		tilesToCheck.Add(startTile);

		while (tilesToCheck.Count > 0)
		{
			Tile check = tilesToCheck.First(); // So this should be a pretty fast way to get any value from the set.. a KeyedCollection could work better here?
											   // But using a list could probably still be the most efficient... since its only the Contains check we get a speedup here..
			tilesToCheck.Remove(check);
			tilesChecked.Add(check);
			foreach (BorderInfo bI in check.GetParent().GetBorderInfo(check.pos))
			{
				if (!insideCondition(bI.tile))
				{
					if (validBorderCondition == null || validBorderCondition(bI))
					{
						// There are 2 corners, find them (create them if they don't exist), and add this border to them..
						for (int i = 0; i < bI.corners.Length; i++)
						{
							bool found = false;
							foreach (CornerInfo cI in entireBorderInfo.cornerInfo)
							{
								if (cI.CloseEnough(bI.corners[i]))
								{
									bI.cornerRefs[i] = cI;
									cI.borderInfoAdjacent.Add(bI);
									found = true;
									break;
								}
							}

							if (!found)
							{
								CornerInfo newCorner = new CornerInfo(bI.corners[i], bI);
								entireBorderInfo.cornerInfo.Add(newCorner);
								bI.cornerRefs[i] = newCorner;
							}
						}
						entireBorderInfo.borderInfo.Add(bI);
					} // I don't think it needs to add the tile to tilesChecked here
				}
				else
				{
					if (!tilesToCheck.Contains(bI.tile) && !tilesChecked.Contains(bI.tile))
					{
						tilesToCheck.Add(bI.tile);
					}
				}
			}
		}
		return entireBorderInfo;
	}

	public static Mesh CreatedBorderedMesh(EntireBorderInfo entireBorder, float height, float padding, bool inFacing)
	{
		foreach (CornerInfo cI in entireBorder.cornerInfo)
		{
			foreach (BorderInfo bI in cI.borderInfoAdjacent)
			{
				cI.AddToOffset(-bI.connectionNormal);
			}
		}

		Vector3[] vertices = new Vector3[entireBorder.borderInfo.Count * 4];
		Vector3[] normals = new Vector3[entireBorder.borderInfo.Count * 4];
		int[] triangles = new int[entireBorder.borderInfo.Count * 6];


		for (int i = 0; i < entireBorder.borderInfo.Count; i++)
		{
			BorderInfo bI = entireBorder.borderInfo[i];
			for (int c = 0; c < 2; c++)
			{
				vertices[i * 4 + c] = bI.cornerRefs[c].GetOffsetCorner(padding);
				normals[i * 4 + c] = inFacing ? -bI.connectionNormal : bI.connectionNormal;
			}
			for (int c = 2; c < 4; c++)
			{
				vertices[i * 4 + c] = vertices[i * 4 + c - 2] + Vector3.up * height;
				normals[i * 4 + c] = normals[i * 4 + c - 2];
			}

			triangles[i * 6 + 0] = i * 4;
			triangles[i * 6 + 1] = i * 4 + (inFacing ? 2 : 1);
			triangles[i * 6 + 2] = i * 4 + (inFacing ? 1 : 2);
			triangles[i * 6 + 3] = i * 4 + (inFacing ? 1 : 2);
			triangles[i * 6 + 4] = i * 4 + (inFacing ? 2 : 1);
			triangles[i * 6 + 5] = i * 4 + 3;
		}
		Mesh mesh = new Mesh
		{
			vertices = vertices,
			normals = normals,
			triangles = triangles
		};
		return mesh;
	}*/

	public bool ShouldBeVisible(Section other)
	{
		// All visibility is done through linked sides...
		foreach (Side a in linked)
		{
			foreach (Side b in other.linked)
			{
				// Squared magnitude means that short distances get even shorter here:
				if (math.distancesq(a.Get(0), b.Get(0)) <= 0.001f || math.distancesq(a.Get(a.numTiles), b.Get(0)) <= 0.001f ||
					math.distancesq(a.Get(0), b.Get(b.numTiles)) <= 0.001f || math.distancesq(a.Get(a.numTiles), b.Get(b.numTiles)) <= 0.001f)
					return true;

				

				float offseta0 = Vector3.Dot(b.Get(0) - a.GetCenter(), a.sideInfo.normal);
				float offseta1 = Vector3.Dot(b.Get(b.numTiles) - a.GetCenter(), a.sideInfo.normal);
				float offsetb0 = Vector3.Dot(a.Get(0) - b.GetCenter(), b.sideInfo.normal);
				float offsetb1 = Vector3.Dot(a.Get(a.numTiles) - b.GetCenter(), b.sideInfo.normal);

				// Cull completely behind:
				if ((offseta0 < 0 && offseta1 < 0) || (offsetb0 < 0 && offsetb1 < 0))
					continue;

				Vector2Double[,] s = { { new Vector2Double(a.Get(0).x, a.Get(0).z), new Vector2Double(a.Get(a.numTiles).x, a.Get(a.numTiles).z) },
									 { new Vector2Double(b.Get(0).x, b.Get(0).z), new Vector2Double(b.Get(b.numTiles).x, b.Get(b.numTiles).z) } };
				// Firstly, we need to cutoff the parts of the side that are behind
				// If we do, we might have to switch s because one way will not work for the next part


				// Debug.Log("BEFORE_PRE: " + s[0, 0] + ", " + s[0, 1] + " / " + s[1, 0] + ", " + s[1, 1]);
				// GameObject line;
				if (Math.Abs((s[0, 1] - s[0, 0]).normalized.Cross((s[1, 1] - s[1, 0]).normalized)) >= 0.0000001) // if not close to parallel
				{
					
					// Only 0 or 1 sides need to be trimmed
					if (offseta0 < 0 || offseta1 < 0)
					{
						Vector2Double linesIntersect = s[0, 0] + (s[0, 1] - s[0, 0]).normalized * GetIntersection(s[0, 0], s[1, 0], (s[0, 1] - s[0, 0]).normalized, (s[1, 1] - s[1, 0]).normalized);
						// Debug.Log("Intersection = " + linesIntersect);
						s[1, offseta0 < 0 ? 0 : 1] = linesIntersect;
						offseta0 = Mathf.Max(0, offseta0);
						offseta1 = Mathf.Max(0, offseta1);
					}
					else if (offsetb0 < 0 || offsetb1 < 0)
					{
						Vector2Double linesIntersect = s[0, 0] + (s[0, 1] - s[0, 0]).normalized * GetIntersection(s[0, 0], s[1, 0], (s[0, 1] - s[0, 0]).normalized, (s[1, 1] - s[1, 0]).normalized);
						// Debug.Log("Intersection = " + linesIntersect);
						s[0, offsetb0 < 0 ? 0 : 1] = linesIntersect;
						offsetb0 = Mathf.Max(0, offsetb0);
						offsetb1 = Mathf.Max(0, offsetb1);
					}

					if (Mathf.Min(offseta0, offseta1) < Mathf.Min(offsetb0, offsetb1))
					{
						Vector2Double[] save = { s[0, 0], s[0, 1] };
						s[0, 0] = s[1, 0];
						s[0, 1] = s[1, 1];
						s[1, 0] = save[0];
						s[1, 1] = save[1];
					}

					// The order might have to be changed..
					double connectionIntersection = GetIntersection(s[0, 0], s[0, 1], (s[1, 0] - s[0, 0]).normalized, (s[1, 1] - s[0, 1]).normalized);
					if (connectionIntersection > 0 && connectionIntersection * connectionIntersection < (s[1, 0] - s[0, 0]).sqrMagnitude)
					{
						Vector2Double save = s[1, 0];
						s[1, 0] = s[1, 1];
						s[1, 1] = save;
					}

					// Debug.Log("PRE: " + s[0, 0] + ", " + s[0, 1] + " / " + s[1, 0] + ", " + s[1, 1]);

					Vector2Double[] sLine = { s[1, 0] - s[0, 0], s[1, 1] - s[0, 1] };
					// The longer one is the one that needs to be modified...
					if (sLine[0].sqrMagnitude > sLine[1].sqrMagnitude)
						s[1, 0] = s[0, 0] + sLine[0].normalized * GetIntersection(s[0, 0], s[1, 1], sLine[0].normalized, (s[0, 0] - s[0, 1]).normalized);
					else
						s[1, 1] = s[0, 1] + sLine[1].normalized * GetIntersection(s[0, 1], s[1, 0], sLine[1].normalized, (s[0, 1] - s[0, 0]).normalized);
					

					// Debug.Log("POST: " + s[0, 0] + ", " + s[0, 1] + " / " + s[1, 0] + ", " + s[1, 1]);
				}

				/*line = UnityEngine.Object.Instantiate(GameObject.Find("Line"));
				line.GetComponent<LineRenderer>().SetPosition(0, new Vector3((float)s[1, 0].x, 0, (float)s[1, 0].y));
				line.GetComponent<LineRenderer>().SetPosition(1, new Vector3((float)s[1, 1].x, 0, (float)s[1, 1].y));
				line.GetComponent<LineRenderer>().material.color = new Color(0, 0, 255);
				line.name = "Side " + Game.map.nodes.IndexOf(this) + " to " + Game.map.nodes.IndexOf(other);*/


				// Note that the modification to s are kept, which means that there is a chance there will be nodes that are considered visible that are impossible
				// But this ends up being an edge case

				// We should cutoff the portions of the sides that are behind, and then project them to be parallel, making it equal or closer to the side itself in terms of distance to the other side

				Vector2Double[] lineVec = { (s[0, 1] - s[0, 0]).normalized, (s[1, 1] - s[1, 0]).normalized };
				double[] lineDist = { Math.Sqrt((s[0, 1] - s[0, 0]).sqrMagnitude), Math.Sqrt((s[1, 1] - s[1, 0]).sqrMagnitude) };

				/*if (Game.map.nodes.IndexOf(this) == 11 && Game.map.nodes.IndexOf(other) == 19)
				{
					Debug.LogError(s[0, 0] + ", " + s[0, 1] + " / " + s[1, 0] + ", " + s[1, 1]);
				}*/
					// So we start with essentially a box, [0, 1] for x, and [0, 1] for y, that represent the 2 sides respectively

					// A convex polygon, cut by a straight line, produces 2 convex polygons (or 1)
					List<Polygon> potentialPolygons = new List<Polygon>();

				potentialPolygons.Add(new Polygon(new List<Vector2Double> { new Vector2Double(0, 0), new Vector2Double(0, 1), new Vector2Double(1, 1), new Vector2Double(1, 0) }));

				int stopInfinity = 0;
				while (potentialPolygons.Count > 0 && stopInfinity++ < 100)
				{

					int maxPolygonAreaIndex = 0;
					double maxArea = 0;
					// double areaCovered = 0;
					for (int i = 0; i < potentialPolygons.Count; i++)
					{
						double area = potentialPolygons[i].GetArea();
						// areaCovered += area;
						if (area > maxArea)
						{
							maxArea = area;
							maxPolygonAreaIndex = i;
						}
					}
					

					if (stopInfinity >= 99)
					{
						Debug.LogError("Stop infinity? " + potentialPolygons.Count);
					}
					Vector2Double polyMidPoint = potentialPolygons[maxPolygonAreaIndex].GetMidPoint();

					// The raycast is done from the original points...
					Vector2Double startRaycastVec2 = s[0, 0] + (s[0, 1] - s[0, 0]) * polyMidPoint.x;
					Vector2Double endRaycastVec2 = s[1, 0] + (s[1, 1] - s[1, 0]) * polyMidPoint.y;

					// Raycast is done the other way because of collider positioning
					Vector3 startRaycast = new Vector3((float)endRaycastVec2.x, MapInfo.TILE_HEIGHT * WALL_HEIGHT / 2f, (float)endRaycastVec2.y);
					Vector3 endRaycast = new Vector3((float)startRaycastVec2.x, MapInfo.TILE_HEIGHT * WALL_HEIGHT / 2f, (float)startRaycastVec2.y);
					float dist = Vector3.Distance(startRaycast, endRaycast);

					// Despite the raycast being 2 ways here, I believe that bi-directional walls are important
					if (!Physics.Raycast(startRaycast, (endRaycast - startRaycast) / dist, out RaycastHit hitInfo, dist, WALL_LAYER))
					{
						// hitInfo can be reused here
						if (!Physics.Raycast(endRaycast, (startRaycast - endRaycast) / dist, out hitInfo, dist, WALL_LAYER))
						{
							/*line = UnityEngine.Object.Instantiate(GameObject.Find("Line"));
							line.GetComponent<LineRenderer>().SetPosition(0, startRaycast);
							line.GetComponent<LineRenderer>().SetPosition(1, endRaycast);
							line.name = "Line " + Game.map.nodes.IndexOf(this) + " to " + Game.map.nodes.IndexOf(other);*/
							return true; // Visible!
						}
					}

					/*if (Game.map.nodes.IndexOf(this) == 11 && Game.map.nodes.IndexOf(other) == 19)
					{
						line = UnityEngine.Object.Instantiate(GameObject.Find("Line"));
						line.GetComponent<LineRenderer>().SetPosition(0, startRaycast);
						line.GetComponent<LineRenderer>().SetPosition(1, endRaycast);
						line.name = "Line " + Game.map.nodes.IndexOf(this) + " to " + Game.map.nodes.IndexOf(other);
					}*/


					Mesh hitMesh = ((MeshCollider)hitInfo.collider).sharedMesh;
					Vector3[] trianglePositions = {hitMesh.vertices[hitMesh.triangles[hitInfo.triangleIndex * 3 + 0]],
									hitMesh.vertices[hitMesh.triangles[hitInfo.triangleIndex * 3 + 1]],
									hitMesh.vertices[hitMesh.triangles[hitInfo.triangleIndex * 3 + 2]] };


					Vector2Double[] p;
					if (trianglePositions[0].y == trianglePositions[1].y)
						p = new Vector2Double[] { new Vector2Double(trianglePositions[0].x, trianglePositions[0].z), new Vector2Double(trianglePositions[1].x, trianglePositions[2].z) };
					else if (trianglePositions[0].y == trianglePositions[2].y)
						p = new Vector2Double[] { new Vector2Double(trianglePositions[0].x, trianglePositions[0].z), new Vector2Double(trianglePositions[2].x, trianglePositions[2].z) };
					else 
						p = new Vector2Double[] { new Vector2Double(trianglePositions[1].x, trianglePositions[1].z), new Vector2Double(trianglePositions[2].x, trianglePositions[2].z) };

					// Debug.Log("p = " + p[0] + ", " + p[1]);

					// Each point generates a line, these 2 lines define the 1 or 2 regions that should be takenout of the existing triangles
					Vector2Double[] lineStarts = new Vector2Double[2];
					Vector2Double[] lines = new Vector2Double[2];
					for (int i = 0; i < 2; i++)
					{
						lineStarts[i] = new Vector2Double(GetIntersection(s[0, 0], p[i], lineVec[0], (p[i] - s[1, 0]).normalized) / lineDist[0], 0);
						Vector2Double lineEnd = new Vector2Double(GetIntersection(s[0, 0], p[i], lineVec[0], (p[i] - s[1, 1]).normalized) / lineDist[0], 1);
						lines[i] = (lineEnd - lineStarts[i]).normalized;
					}
					// Debug.Log("line = " + lineStarts[0] + ", " + lines[0].x + ", " + lines[0].y + " / " + lineStarts[1] + ", " + lines[1].x + ", " + lines[1].y);

					// There's no point doing it the other way, since it'll generate the same lines, since they are parallel.. which they have to be in order for them to be straight lines

					List<Polygon> nextPolygons = new List<Polygon>();
					for (int i = 0; i < potentialPolygons.Count; i++)
					{
						// Slice the triangle along each line
						// Determine which of the sliced triangles are kept and not kept...

						// I believe we should purposely not normalize the first line vector

						// Vector2[][] newTriangles = SplitTriangle(potentialTriangles[i], lineStarts[0], lines[0], stopInfinity >= 16 && potentialTriangles.Count < 20);

						// List<Vector2[]> finalTriangles = new List<Vector2[]>();
						// for (int k = 0; k < newTriangles.Length; k++)
						// {
						// 	finalTriangles.AddRange(SplitTriangle(newTriangles[k], lineStarts[1], lines[1], stopInfinity >= 16 && potentialTriangles.Count < 20));
						// }
						// if (stopInfinity >= 16 && potentialTriangles.Count < 20)
						// {
						// Debug.Log("FC: " + finalTriangles.Count);
						// }

						Polygon[] newPolygons = potentialPolygons[i].SplitOnLine(lineStarts[0], lines[0]);
						List<Polygon> finalPolygons = new List<Polygon>();
						for (int k = 0; k < newPolygons.Length; k++)
						{
							finalPolygons.AddRange(newPolygons[k].SplitOnLine(lineStarts[1], lines[1]));
						}

						// Add all the new triangles that are NOT within the zone...
						foreach (Polygon polygon in finalPolygons)
						{
							Vector2Double midPoint = polygon.GetMidPoint();
							// if (stopInfinity >= 16 && potentialTriangles.Count < 20)
							// {
								// Debug.Log("HERE WE ARE: " + stopInfinity + " / " + triangle[0] + " " + triangle[1] + " " + triangle[2]);
							// }
							float sign0 = Math.Sign(lines[0].x * (midPoint.y - lineStarts[0].y) - lines[0].y * (midPoint.x - lineStarts[0].x));
							float sign1 = Math.Sign(lines[1].x * (midPoint.y - lineStarts[1].y) - lines[1].y * (midPoint.x - lineStarts[1].x));
							if (sign0 == sign1)
							{
								// NOT in the zone:
								nextPolygons.Add(polygon);
							}
						}
					}
					bool same = false;
					if (nextPolygons.Count == potentialPolygons.Count)
					{
						same = true;
						for (int i = 0; i < nextPolygons.Count; i++)
						{
							if (nextPolygons[i] != potentialPolygons[i])
							{
								same = false;
								break;
							}
						}
					}
					potentialPolygons = nextPolygons;
					if (same)
					{
						// Cut out the triangle that was just tested on... to prevent infinite loops this way..
						// Ideally we just change the priority of it..
						potentialPolygons.RemoveAt(maxPolygonAreaIndex);
					}
				}

				// No visibility was found = completely not visible
			}
		}
		return false;
	}

	/*private static Vector2[][] SplitTriangle(Vector2[] triangle, Vector2 lineStart, Vector2 line, bool verbose = false)
	{
		float inter01 = GetIntersection(triangle[0], lineStart, triangle[1] - triangle[0], line);
		float inter02 = GetIntersection(triangle[0], lineStart, triangle[2] - triangle[0], line);
		float inter12 = GetIntersection(triangle[1], lineStart, triangle[2] - triangle[1], line);
		if (verbose)
		{
			// Debug.Log("Inter: " + lineStart.x + ", " + lineStart.y + ", (" + line.x + ", " + line.y + ")... (" + triangle[0].x + ", " + triangle[0].y + "), " + triangle[1] + ", (" + triangle[2].x + ", " + triangle[2].y + "): " + inter01 + ", " + inter02 + ", " + inter12);
		}
		if (inter01 > 0 && inter01 < 1)
		{   // Note that we're always going to divide the triangle into a quadrilateral and a triangle
			if (inter02 > 0 && inter02 < 1)
				return SplitTriangleOnLine(triangle, inter01, inter02);
			else
				return SplitTriangleOnLine(new Vector2[] { triangle[1], triangle[0], triangle[2] }, 1f - inter01, inter12);
		}
		else if (inter02 > 0 && inter02 < 1)
		{
			return SplitTriangleOnLine(new Vector2[] { triangle[2], triangle[0], triangle[1] }, 1f - inter02, 1f - inter12);
		}
		return new Vector2[][] { triangle };
	}

	private static Vector2[][] SplitTriangleOnLine(Vector2[] triangle, float inter01, float inter02)
	{
		if (inter01 <= 0.0001 || inter02 <= 0.0001 || inter01 >= 0.9999 || inter02 >= 0.9999)
			return new Vector2[][] { triangle };

		Vector2 inter01Point = triangle[0] + (triangle[1] - triangle[0]) * inter01;
		Vector2 inter02Point = triangle[0] + (triangle[2] - triangle[0]) * inter02;
		Vector2[] a = { triangle[0], inter01Point, inter02Point };
		Vector2[] b = { triangle[1], inter01Point, inter02Point };
		Vector2[] c = { triangle[2], triangle[1], inter02Point };
		return new Vector2[][] { a, b, c };
	}*/

	// lineVec1 and lineVec2 MUST be normalized
	public static double GetIntersection(Vector2Double p1, Vector2Double p2, Vector2Double lineVec1, Vector2Double lineVec2)
	{
		double cross1and2 = lineVec1.Cross(lineVec2);
		if (Math.Abs(cross1and2) > 0.0000000001)
		{
			return (p2 - p1).Cross(lineVec2) / cross1and2; // In the other intersection method, sqrMagnitude is used.. but there's no need here
		}
		else
		{
			// Debug.LogError("Parallel Intersection.. " + p1 + ", " + p2 + ", " + lineVec1 + ", " + lineVec2 + " " + Cross(lineVec1, lineVec2) + ", " + lineVec1.x + ", " + lineVec1.y + " / " + lineVec2.x + ", " + lineVec2.y);
			return -1; //hmm
		}
	}
}

public class Polygon
{
	List<Vector2Double> points;
	public Polygon(List<Vector2Double> points)
	{
		this.points = points;
	}

	public Polygon[] SplitOnLine(Vector2Double lineStart, Vector2Double line)
	{
		// It will cross 0 or 2 lines..
		// List<Vector2> splits = new List<Vector2>();
		// List<float> splitNums = new List<float>();
		List<int> splitIndices = new List<int>();
		for (int i = 0; i < points.Count; i++)
		{
			Vector2Double prev = points[i];
			Vector2Double next = points[i == points.Count - 1 ? 0 : i + 1];
			double prevToNext = Math.Sqrt((next - prev).sqrMagnitude);
			double inter = Section.GetIntersection(prev, lineStart, (next - prev) / prevToNext, line) / prevToNext;
			// Debug.Log(splits.Count + ": " + inter + " at " + i);
			if (inter >= 0 && inter <= 1)
			{
				// splitNums.Add(inter);
				// splits.Add(prev + (next - prev) * inter);
				// splitIndices.Add(i);

				Vector2Double add = prev + (next - prev) * inter;

				if ((next - add).sqrMagnitude < 0.00000001)
				{
					int value = i == points.Count - 1 ? 0 : i + 1;
					if (splitIndices.Count == 0 || splitIndices[0] != value)
					{
						splitIndices.Add(value);
					}
				}
				else if ((prev - add).sqrMagnitude < 0.00000001)
				{
					if (splitIndices.Count == 0 || splitIndices[splitIndices.Count - 1] != i)
					{
						splitIndices.Add(i);
					}
				}
				else
				{
					i++; // Make sure to skip the next iteration otherwise infinite loops can happen
					points.Insert(i, add);
					splitIndices.Add(i);
				}
			}
		}

		// Verification only:
		for (int i = 0; i < points.Count; i++)
		{
			Vector2Double prev = points[i];
			Vector2Double next = points[i == points.Count - 1 ? 0 : i + 1];
			if ((next - prev).sqrMagnitude < 0.000000000000001)
			{
				Debug.LogError("TOO CLOSE!!! " + i + " of " + points.Count + ", (" + prev.x + ", " + prev.y + ") to (" + next.x + ", " + next.y + ")");
			}
		}

		if (splitIndices.Count == 0)
		{
			return new Polygon[] { this };
		}
		else if (splitIndices.Count == 2)
		{
			if ((splitIndices[0] + 1) % points.Count == splitIndices[1] || (splitIndices[1] + 1) % points.Count == splitIndices[0])
			{
				// Debug.LogError("Split indices ARE NEXT TO EACHOTHER! " + lineStart + ", " + line + ", " + points[splitIndices[0]] + " -> " + points[splitIndices[1]]);
				return new Polygon[] { this };
			}

			List<Vector2Double> newPoints = new List<Vector2Double>();
			if (splitIndices[0] > splitIndices[1])
			{
				int save = splitIndices[0];
				splitIndices[0] = splitIndices[1];
				splitIndices[1] = save;
			}
			else if (splitIndices[0] == splitIndices[1])
			{
				Debug.LogError("Split Indices the same!!!! " + splitIndices[0] + " of " + points.Count);
			}

			for (int i = splitIndices[0]; i <= splitIndices[1]; i++)
			{
				newPoints.Add(points[i]);
			}
			Polygon a = new Polygon(newPoints);
			newPoints = new List<Vector2Double>();
			for (int i = splitIndices[1]; i < points.Count; i++)
			{
				newPoints.Add(points[i]);
			}
			for (int i = 0; i <= splitIndices[0]; i++)
			{
				newPoints.Add(points[i]);
			}
			return new Polygon[] { a, new Polygon(newPoints) };
		}
		else
		{
			/*if (splitIndices.Count >= 3)
			{
				Debug.LogError("Splits = " + splitIndices.Count + " with line: " + lineStart + ", " + line);
				for (int i = 0; i < splitIndices.Count; i++)
				{
					Debug.Log("Split " + i + ": " + splitIndices[i] + ", " + points[splitIndices[i]] + ", " + points[(splitIndices[i] + 1) % points.Count]);
				}
			}*/
			return new Polygon[] { this };
		}
	}

	public double GetArea()
	{
		if (points.Count < 3)
		{
			Debug.LogError("Polygon count < 3, (" + points.Count + ")");
			return 0;
		}
		double area = points[points.Count - 1].x * points[0].y - points[0].x * points[points.Count - 1].y;
		for (int i = 1; i < points.Count; i++)
		{
			area += points[i - 1].x * points[i].y - points[i].x * points[i - 1].y;
		}
		return Math.Abs(area / 2);
	}

	public Vector2Double GetMidPoint()
	{
		Vector2Double total = new Vector2Double(0, 0);
		foreach (Vector2Double point in points)
		{
			total += point;
		}
		return total / points.Count;
	}
}

public struct Vector2Double
{
	public double x;
	public double y;

	public double sqrMagnitude
	{
		get { return x * x + y * y; }
	}

	public Vector2Double normalized
	{
		get { return new Vector2Double(x, y) / Math.Sqrt(x * x + y * y); }
	}

	public Vector2Double(double x, double y)
	{
		this.x = x;
		this.y = y;
	}

	public double Cross(Vector2Double b)
	{
		return x * b.y - y * b.x;
	}

	public static Vector2Double operator +(Vector2Double a, Vector2Double b)
	{
		return new Vector2Double(a.x + b.x, a.y + b.y);
	}

	public static Vector2Double operator -(Vector2Double a, Vector2Double b)
	{
		return new Vector2Double(a.x - b.x, a.y - b.y);
	}

	public static Vector2Double operator *(Vector2Double a, double b)
	{
		return new Vector2Double(a.x * b, a.y * b);
	}

	public static Vector2Double operator /(Vector2Double a, double b)
	{
		return new Vector2Double(a.x / b, a.y / b);
	}
}

/*public class EntireBorderInfo
{
	public HashSet<CornerInfo> cornerInfo = new HashSet<CornerInfo>(); // Hashed ONLY based on the object memory location
	public List<BorderInfo> borderInfo = new List<BorderInfo>();

	// Note that the order of the borderInfos can effect the result here, there's not much that can be done about that..
	public List<EntireBorderInfo> SplitOnDist(float maxOffset)
	{
		List<EntireBorderInfo> ebis = new List<EntireBorderInfo>();
		foreach (BorderInfo bI in borderInfo)
		{
			EntireBorderInfo ebi = new EntireBorderInfo();
			ebi.AddBorder(bI);
			ebis.Add(ebi);
		}

		// Merge them on the corners:
		bool changed = true;
		while (changed)
		{
			changed = false;
			for (int a = 0; a < ebis.Count; a++)
			{
				bool toBreak = false;
				for (int b = 0; b < ebis.Count; b++)
				{
					if (a != b && ebis[a].CanMerge(ebis[b], maxOffset))
					{
						changed = true;
						ebis[a].cornerInfo.UnionWith(ebis[b].cornerInfo);
						ebis[a].borderInfo.AddRange(ebis[b].borderInfo);
						ebis.RemoveAt(b);
						toBreak = true;
						break;
					}
				}
				if (toBreak)
					break;
			}
		}
		return ebis;*/

		/*foreach (BorderInfo bI in borderInfo)
		{
			bool found = false;
			foreach (EntireBorderInfo ebi in ebis)
			{
				foreach (CornerInfo cornerRef in bI.cornerRefs)
				{
					if (ebi.cornerInfo.Contains(cornerRef))
					{
						bool good = true;
						Vector3 tangent = Vector3.Cross(bI.connectionNormal, Vector3.up);
						foreach (BorderInfo otherBI in ebi.borderInfo)
						{
							float offset = Vector3.Distance(otherBI.connectionPoint, Vector3.Project(otherBI.connectionPoint - bI.connectionPoint, tangent));
							if (offset > Tile.TILE_LENGTH * 0.25f)
								good = false;
						}
						if (good)
						{
							ebi.AddBorder(bI);
							found = true;
							break;
						}
					}
				}
				if (found)
					break;
			}
			if (!found)
			{
				EntireBorderInfo ebi = new EntireBorderInfo();
				ebi.AddBorder(bI);
				ebis.Add(ebi);
			}
		}
		return ebis;*/
	//}

	/*public bool CanMerge(EntireBorderInfo other, float maxOffset)
	{
		BorderInfo low = borderInfo[0];
		BorderInfo high = borderInfo[borderInfo.Count - 1];
		BorderInfo otherLow = other.borderInfo[0];
		BorderInfo otherHigh = other.borderInfo[other.borderInfo.Count - 1];
		if (high.cornerRefs[1] == otherLow.cornerRefs[0] || high.cornerRefs[0] == otherLow.cornerRefs[1] || high.cornerRefs[0] == otherLow.cornerRefs[0] || high.cornerRefs[1] == otherLow.cornerRefs[1])
		{
			float offset1 = Vector3.Distance(otherHigh.connectionPoint - low.connectionPoint, Vector3.Project(otherHigh.connectionPoint - low.connectionPoint, Vector3.Cross(low.connectionNormal, Vector3.up)));
			float offset2 = Vector3.Distance(low.connectionPoint - otherHigh.connectionPoint, Vector3.Project(low.connectionPoint - otherHigh.connectionPoint, Vector3.Cross(otherHigh.connectionNormal, Vector3.up)));
			return offset1 <= maxOffset && offset2 <= maxOffset;
		}
		return false;
	}

	private void AddBorder(BorderInfo bI)
	{
		borderInfo.Add(bI);
		cornerInfo.UnionWith(bI.cornerRefs);
	}
}*/

[Serializable]
public class Side
{
	[Serializable]
	public struct ConstantSideInfo
	{
		public readonly float3 normal;
		public readonly float3 tangent;
		public readonly byte sideIndex;
		public readonly byte dir;

		public ConstantSideInfo(float3 normal, float3 tangent, byte sideIndex, byte dir)
		{
			this.normal = normal;
			this.tangent = tangent;
			this.sideIndex = sideIndex;
			this.dir = dir;
		}
	}

	public ConstantSideInfo sideInfo;

	public const float ANGLE_TOLERANCE = 1f; // this can be as high as like 180 degrees, just for testing which order to iterate the indecies...
	
	// A side is defined as a straight edge with no breaks

	public readonly byte numTiles;
	public readonly float3 start;

	public readonly byte startIndex;

	public Side(byte numTiles, float3 start, byte startIndex, ConstantSideInfo sideInfo)
	{
		this.numTiles = numTiles;
		this.start = start;
		this.sideInfo = sideInfo;
		this.startIndex = startIndex;
	}

	public float3 Get(float offset)
	{
		return start + sideInfo.tangent * offset * MapInfo.TILE_LENGTH;
	}
	public float3 GetCenter()
	{
		return Get(numTiles / 2f);
	}
}