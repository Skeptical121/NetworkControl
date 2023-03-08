using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Mathematics;
using Unity.Rendering;
using UnityEngine;


[Serializable]
public class CurveSection : Section
{
	public const float EXTRA_COLLIDER_WIDTH = 0.9f * MapInfo.TILE_LENGTH;

	private readonly float3[] curve;
	private List<float3> positions; // Size will be resolution * 2 + 1, since it stores every half point from 0 to resolution
	private List<float3> directions; // Size will be resolution, since + 1, since it stores every full point from 0 to resolution
	private List<float3> tangents; // Size will be resolution * 2 + 1, since it stores every half point from 0 to resolution

	[NonSerialized] private Bounds bounds;

	// So... this is simple enough...

	// While technically the path is represented by a bezier curve, in order to normalize the distances
	public CurveSection(int width, int height, float3[] curve)
		: base(new int3(width, height, 0))
	{
		this.curve = curve;
	}


	public override float3 GetWorldPosition(int edgeX, int edgeY, int edgeZ)
	{
		return GetPos(edgeZ, edgeX * 0.5f, edgeY * 0.5f);
	}

	public override float3 GetWorldNormal(byte dir, byte x, byte y, byte z)
	{
		switch (dir)
		{
			case 0: return -tangents[z * 2 + 1];
			case 1: return tangents[z * 2 + 1];
			case 2: return -directions[z];
			case 3: return directions[z + 1];
			case 4: return new float3(0, -1, 0);
			case 5: return new float3(0, 1, 0);
			default: Assert.Fail("Invalid: " + dir); return new float3(0, 0, 0);
		}
	}

	public override bool InBounds(float3 worldPosition)
	{
		if (bounds.Contains(worldPosition))
		{
			Vector2 worldPos = new Vector2(worldPosition.x, worldPosition.z);
			for (int i = 0; i < size.z; i++)
			{
				Vector3 a = GetPos(i * 2, 0, 0);
				Vector3 b = GetPos(i * 2, size.x, 0);
				Vector3 c = GetPos(i * 2 + 2, 0, 0);
				Vector3 d = GetPos(i * 2 + 2, size.x, 0);
				if (PointInTriangle(worldPos, new Vector2(a.x, a.z), new Vector2(b.x, b.z), new Vector2(c.x, c.z)) || 
					PointInTriangle(worldPos, new Vector2(b.x, b.z), new Vector2(c.x, c.z), new Vector2(d.x, d.z)))
					return true;
			}
			return false;
		}
		return false;
	}

	private static bool PointInTriangle(Vector2 point, Vector2 a, Vector2 b, Vector2 c)
	{
		return SameSide(point, a, b, c) && SameSide(point, b, a, c) && SameSide(point, c, a, b);
	}

	private static bool SameSide(Vector2 p1, Vector2 p2, Vector2 a, Vector2 b)
	{
		return CrossProduct(b - a, p1 - a) * CrossProduct(b - a, p2 - a) >= 0;
	}

	private static float CrossProduct(Vector2 a, Vector2 b)
	{
		return a.x * b.y - a.y * b.x;
	}

	public override bool CanAdd()
	{

		BezierCurve.GetRep(curve, 100, out NativeArray<BezierCurveNetDistance> netDistance);
		float totalDist = netDistance[netDistance.Length - 1].dist;

		size.z = Mathf.Max(1, Mathf.RoundToInt(totalDist / MapInfo.TILE_LENGTH));
   
		positions = new List<float3>();
		directions = new List<float3>();
		tangents = new List<float3>();
		float boxSize = MapInfo.TILE_LENGTH - MapInfo.MAX_TILE_LENGTH_DIFFERENCE;
		float3 prevPos0 = float3.zero;
		float3 prevPos1 = float3.zero;
		for (byte z = 0; z <= size.z; z++)
		{
			PosRot prevPosRot = netDistance.GetMapped(totalDist * z / (float)size.z);

			positions.Add(prevPosRot.pos);
			directions.Add(prevPosRot.Dir);
			tangents.Add(prevPosRot.Tan);

			if (z < size.z)
			{
				PosRot posRot = netDistance.GetMapped(totalDist * (z + 0.5f) / size.z);
				positions.Add(posRot.pos);
				tangents.Add(posRot.Tan);
			}

			if (z == 0)
			{
				prevPos0 = GetPos(0, 0, 0);
				prevPos1 = GetPos(0, size.x, 0);
			}
			else
			{
				float3 nextPos0 = GetPos(z * 2, 0, 0);
				float3 nextPos1 = GetPos(z * 2, size.x, 0);

				float estimatedDist0 = math.dot(directions[z - 1], nextPos0 - prevPos0);
				float estimatedDist1 = math.dot(directions[z - 1], nextPos1 - prevPos1);

				// EstimatedDist is a really good approximation, so we can use that
				if (Mathf.Abs(MapInfo.TILE_LENGTH - estimatedDist0) >= MapInfo.MAX_TILE_LENGTH_DIFFERENCE || Mathf.Abs(MapInfo.TILE_LENGTH - estimatedDist1) >= MapInfo.MAX_TILE_LENGTH_DIFFERENCE)
				{
					return false;
				}

				prevPos0 = nextPos0;
				prevPos1 = nextPos1;
			}
		}

		for (int z = 0; z < size.z; z++)
		{
			PosRot posRot = netDistance.GetMapped(totalDist * (z + 0.5f) / size.z);
			if (Physics.CheckBox(posRot.pos + new float3(0, 1, 0) * size.y / 2f * MapInfo.TILE_HEIGHT,
				new Vector3(EXTRA_COLLIDER_WIDTH + size.x / 2f * MapInfo.TILE_LENGTH, size.y / 2f * MapInfo.TILE_HEIGHT, boxSize / 2f), posRot.rot, NODE_LAYER))
			{
				return false;
			}
		}
		return true;
	}

	public override void AddSides()
	{
		AddSide(0, (byte)size.x, GetPos(0, 0, 0), -directions[0], tangents[0], 2);
		AddSide((byte)(size.z - 1), (byte)size.x, GetPos(size.z * 2, 0, 0), directions[size.z], tangents[tangents.Count - 1], 3);
	}

	public override RenderMesh CreateMesh()
	{
		Mesh renderMesh = CreateFlatRenderMesh(0, true);
		Mesh renderMeshCeiling = CreateFlatRenderMesh(WALL_HEIGHT, false);
		Mesh bothMeshes = MeshCreator.CombineMeshes(true, renderMesh, renderMeshCeiling);
		gameRep.GetComponent<MeshCollider>().sharedMesh = bothMeshes;
		// gameRep.GetComponent<MeshFilter>().sharedMesh = bothMeshes; // This will mean that changes to this mesh will change this
		bounds = bothMeshes.bounds; // Even though this isn't an actual collider for the node itself (like it's not closed), it should be accurate for the bounds

		return new RenderMesh { mesh = bothMeshes, material = sectionMat };
	}


	public override Mesh CreateColliderMesh()
	{
		Vector3[] vertices = new Vector3[4 * (size.z + 1)];
		int[] triangles = new int[3 * (size.z * 8 + 4)];
		for (int z = 0; z <= size.z; z++)
		{
			vertices[z * 4] = GetPos(z * 2, -EXTRA_COLLIDER_WIDTH / MapInfo.TILE_LENGTH, 0);
			vertices[z * 4 + 1] = GetPos(z * 2, size.x + EXTRA_COLLIDER_WIDTH / MapInfo.TILE_LENGTH, 0);
			vertices[z * 4 + 2] = GetPos(z * 2, -EXTRA_COLLIDER_WIDTH / MapInfo.TILE_LENGTH, WALL_HEIGHT);
			vertices[z * 4 + 3] = GetPos(z * 2, size.x + EXTRA_COLLIDER_WIDTH / MapInfo.TILE_LENGTH, WALL_HEIGHT);
			if (z < size.z)
			{
				int[] vI = { 0, 4, 1, 1, 4, 5, 0, 2, 4, 4, 2, 6, 1, 5, 3, 3, 5, 7, 2, 3, 6, 6, 3, 7 };
				for (int t = 0; t < 24; t++)
				{
					triangles[z * 24 + t] = z * 4 + vI[t];
				}
			}
		}
		int[] vILast = { 0, 1, 2, 2, 1, 3, size.z * 4 + 0, size.z * 4 + 2, size.z * 4 + 1, size.z * 4 + 1, size.z * 4 + 2, size.z * 4 + 3 };
		for (int t = 0; t < 12; t++)
		{
			triangles[3 * (size.z * 8) + t] = vILast[t];
		}

		Mesh colliderMesh = new Mesh
		{
			vertices = vertices,
			triangles = triangles
		};
		return colliderMesh;
	}

	public Mesh CreateFlatRenderMesh(byte y, bool facingUp)
	{
		Vector3[] meshVertices = new Vector3[2 * (size.z + 1)];
		Vector3[] meshNormals = new Vector3[2 * (size.z + 1)];
		Vector2[] meshUV = new Vector2[2 * (size.z + 1)];
		// List<Vector3> meshUV = new List<Vector3>(2 * (resolution + 1));
		int[] meshTriangles = new int[3 * size.z * 2];
		for (int z = 0; z <= size.z; z++)
		{
			meshVertices[z * 2] = GetPos(z * 2, size.x, y);
			meshVertices[z * 2 + 1] = GetPos(z * 2, 0, y);

			meshUV[z * 2] = new Vector2(0, z);
			meshUV[z * 2 + 1] = new Vector2(size.x, z);
			
			meshNormals[z * 2] = facingUp ? Vector3.up : Vector3.down;
			meshNormals[z * 2 + 1] = facingUp ? Vector3.up : Vector3.down;
			if (z < size.z)
			{

				meshTriangles[z * 6 + 0] = z * 2 + 0;
				meshTriangles[z * 6 + 1] = z * 2 + (facingUp ? 1 : 2);
				meshTriangles[z * 6 + 2] = z * 2 + (facingUp ? 2 : 1);
				meshTriangles[z * 6 + 3] = z * 2 + (facingUp ? 2 : 1);
				meshTriangles[z * 6 + 4] = z * 2 + (facingUp ? 1 : 2);
				meshTriangles[z * 6 + 5] = z * 2 + 3;
			}
		}

		Mesh renderMesh = new Mesh
		{
			vertices = meshVertices,
			triangles = meshTriangles,
			normals = meshNormals,
			uv = meshUV
		};
		return renderMesh;
	}

	private float3 GetPos(int zIndex, float x, float y)
	{
		return positions[zIndex] + tangents[zIndex] * (x - size.x / 2f) * MapInfo.TILE_LENGTH + new float3(0, y * MapInfo.TILE_HEIGHT, 0);
	}

	public override Vector3 GetCenter()
	{
		return (positions[0] + positions[positions.Count - 1]) / 2;
	}
}
