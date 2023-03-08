using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Mathematics;
using Unity.Rendering;
using UnityEngine;


[Serializable]
public class RectSection : Section
{

	private float3 corner;
	private float3 xDir;
	private float3 zDir;

	// Everything is indoors for now...

	public RectSection(float3 corner, float3 zDir, int3 size)
		: base(size)
	{
		this.corner = corner;
		xDir = Rotate90Across(zDir);
		this.zDir = zDir;
	}

	private static float3 Rotate90Across(float3 dir)
	{
		return new float3(dir.z, dir.y, -dir.x);
	}

	private float3 GetPos(float x, float y, float z)
	{
		return corner + x * xDir * MapInfo.TILE_LENGTH + z * zDir * MapInfo.TILE_LENGTH + new float3(0, y * MapInfo.TILE_HEIGHT, 0);
	}

	public override float3 GetWorldPosition(int edgeX, int edgeY, int edgeZ)
	{
		return GetPos(edgeX * 0.5f, edgeY * 0.5f, edgeZ * 0.5f);
	}

	public override float3 GetWorldNormal(byte dir, byte x, byte y, byte z)
	{
		switch (dir)
		{
			case 0: return -xDir;
			case 1: return xDir;
			case 2: return -zDir;
			case 3: return zDir;
			case 4: return new float3(0, -1, 0);
			case 5: return new float3(0, 1, 0);
			default: Assert.Fail("Invalid: " + dir); return new float3(0, 0, 0);
		}
	}

	public override bool InBounds(float3 worldPosition)
	{
		float3 offset = worldPosition - corner;
		float x = math.dot(offset, xDir);
		float z = math.dot(offset, zDir);
		return x >= 0 && x <= size.x * MapInfo.TILE_LENGTH &&
			   offset.y >= 0 && offset.y <= size.y * MapInfo.TILE_HEIGHT &&
			   z >= 0 && z <= size.z * MapInfo.TILE_LENGTH;
	}

	public override bool CanAdd()
	{
		float3 halfExtents = new float3(size.x * MapInfo.TILE_LENGTH / 2f, size.y * MapInfo.TILE_HEIGHT / 2f, size.z * MapInfo.TILE_LENGTH / 2f);
		// So this is a useful case, since we can just check a box...
		return !Physics.CheckBox(GetPos(size.x / 2f, size.y / 2f, size.z / 2f),
			halfExtents, Quaternion.LookRotation(zDir), NODE_LAYER);
	}

	public override void AddSides()
	{
		AddSide(0, (byte)size.z, GetPos(0, 0, 0), -xDir, zDir, 0);
		AddSide((byte)(size.x - 1), (byte)size.z, GetPos(size.x, 0, 0), xDir, zDir, 1);
		AddSide(0, (byte)size.x, GetPos(0, 0, 0), -zDir, xDir, 2);
		AddSide((byte)(size.z - 1), (byte)size.x, GetPos(0, 0, size.z), zDir, xDir, 3);
	}

	public override Mesh CreateColliderMesh()
	{
		Vector3[] colliderVertices = { GetPos(0, 0, 0), GetPos(0, 0, size.z), GetPos(size.x, 0, size.z), GetPos(size.x, 0, 0),
									GetPos(0, WALL_HEIGHT, 0), GetPos(0, WALL_HEIGHT, size.z), GetPos(size.x, WALL_HEIGHT, size.z), GetPos(size.x, WALL_HEIGHT, 0) };
		int[] colliderTriangles = MeshCreator.MergeTriangles(MeshCreator.RectTri(0, 1, 3, 2), MeshCreator.RectTri(4, 5, 7, 6), MeshCreator.RectTri(0, 1, 4, 5),
			 MeshCreator.RectTri(1, 2, 5, 6), MeshCreator.RectTri(2, 3, 6, 7), MeshCreator.RectTri(3, 0, 7, 4));
		Mesh colliderMesh = new Mesh
		{
			vertices = colliderVertices,
			triangles = colliderTriangles
		};
		return colliderMesh;
	}

	public override RenderMesh CreateMesh()
	{

		Vector3[] meshVertices = { GetPos(0, 0, 0), GetPos(0, 0, size.z), GetPos(size.x, 0, 0), GetPos(size.x, 0, size.z),
								   GetPos(0, WALL_HEIGHT, 0), GetPos(0, WALL_HEIGHT, size.z), GetPos(size.x, WALL_HEIGHT, 0), GetPos(size.x, WALL_HEIGHT, size.z)};
		Vector3[] meshNormals = { Vector3.up, Vector3.up, Vector3.up, Vector3.up, 
								  Vector3.down, Vector3.down, Vector3.down, Vector3.down };
		int[] meshTriangles = { 0, 1, 2, 2, 1, 3,
								4, 6, 5, 5, 6, 7 };
		Vector2[] meshUV = { new Vector2(0, 0), new Vector2(0, size.z), new Vector2(size.x, 0), new Vector2(size.x, size.z),
							 new Vector2(0, 0), new Vector2(0, size.z), new Vector2(size.x, 0), new Vector2(size.x, size.z) }; // Use same texture for top for now

		Mesh renderMesh = new Mesh
		{
			vertices = meshVertices,
			triangles = meshTriangles,
			normals = meshNormals,
			uv = meshUV
		};

		gameRep.GetComponent<MeshCollider>().sharedMesh = renderMesh;
		// gameRep.GetComponent<MeshFilter>().sharedMesh = renderMesh;
		return new RenderMesh { mesh = renderMesh, material = sectionMat };
	}

	public override Vector3 GetCenter()
	{
		return GetPos(size.x * 0.5f, 0f, size.z * 0.5f);
	}
}