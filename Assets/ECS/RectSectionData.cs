using System.Collections;
using System.Collections.Generic;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Rendering;
using Unity.Transforms;
using UnityEngine;

/*
public struct RectSectionData : IComponentData
{
	public float3 corner;
	public float3 xDir;
	public float3 zDir;

	public float3 GetPos(float x, float y, float z)
	{
		return corner + x * xDir * MapInfo.TILE_LENGTH + z * zDir * MapInfo.TILE_LENGTH + new float3(0, y * MapInfo.TILE_HEIGHT, 0);
	}

	public float3 GetWorldNormal(byte dir)
	{
		switch (dir)
		{
			case 0: return -xDir;
			case 1: return xDir;
			case 2: return -zDir;
			case 3: return zDir;
			case 4: return new float3(0, -1, 0);
			case 5: return new float3(0, 1, 0);
			default: return new float3(0, 0, 0);
		}
	}
}


public class RectSectionConstruct : ISectionConstructable
{
	private RectSectionData data;
	private int3 size;

	public RectSectionConstruct(float3 corner, float3 zDir, int3 size)
	{
		this.size = size;
		data = new RectSectionData
		{
			corner = corner,
			xDir = Rotate90Across(zDir),
			zDir = zDir
		};
	}

	private static float3 Rotate90Across(float3 dir)
	{
		return new float3(dir.z, dir.y, -dir.x);
	}

	public override Entity AttemptAdd(EntityManager EntityManager)
	{


		float3 halfExtents = new float3(size.x * MapInfo.TILE_LENGTH / 2f, size.y * MapInfo.TILE_HEIGHT / 2f, size.z * MapInfo.TILE_LENGTH / 2f);
		// So this is a useful case, since we can just check a box...
		if (Physics.CheckBox(data.GetPos(size.x / 2f, size.y / 2f, size.z / 2f),
			halfExtents, Quaternion.LookRotation(data.zDir), Section.NODE_LAYER))
		{
			return Entity.Null;
		}

		Entity entity = EntityManager.CreateEntity(
			typeof(RectSectionData),
			typeof(LinkedSide),
			typeof(UnlinkedSide),
			typeof(VisibleSection),
			typeof(RenderEntityInSection),
			typeof(ESection),
			typeof(Translation),
			typeof(RenderMesh),
			typeof(LocalToWorld));

		EntityManager.SetComponentData(entity, new ESection { size = size, visible = false });

		AddSide(EntityManager.GetBuffer<UnlinkedSide>(entity), 0, (byte)size.z, data.GetPos(0, 0, 0), -data.xDir, data.zDir, 0);
		AddSide(EntityManager.GetBuffer<UnlinkedSide>(entity), (byte)(size.x - 1), (byte)size.z, data.GetPos(size.x, 0, 0), data.xDir, data.zDir, 1);
		AddSide(EntityManager.GetBuffer<UnlinkedSide>(entity), 0, (byte)size.x, data.GetPos(0, 0, 0), -data.zDir, data.xDir, 2);
		AddSide(EntityManager.GetBuffer<UnlinkedSide>(entity), (byte)(size.z - 1), (byte)size.x, data.GetPos(0, 0, size.z), data.zDir, data.xDir, 3);
		EntityManager.SetComponentData(entity, data);
		return entity;
	}

	public override Mesh CreateRenderMesh(EntityManager EntityManager, Entity entity)
	{
		Vector3[] meshVertices = { data.GetPos(0, 0, 0), data.GetPos(0, 0, size.z), data.GetPos(size.x, 0, 0), data.GetPos(size.x, 0, size.z),
				data.GetPos(0, Section.WALL_HEIGHT, 0), data.GetPos(0, Section.WALL_HEIGHT, size.z), data.GetPos(size.x, Section.WALL_HEIGHT, 0), data.GetPos(size.x, Section.WALL_HEIGHT, size.z)};
		Vector3[] meshNormals = { Vector3.up, Vector3.up, Vector3.up, Vector3.up,
								  Vector3.down, Vector3.down, Vector3.down, Vector3.down };
		int[] meshTriangles = { 0, 1, 2, 2, 1, 3,
								4, 6, 5, 5, 6, 7 };
		Vector2[] meshUV = { new Vector2(0, 0), new Vector2(0, size.z), new Vector2(size.x, 0), new Vector2(size.x, size.z),
							 new Vector2(0, 0), new Vector2(0, size.z), new Vector2(size.x, 0), new Vector2(size.x, size.z) }; // Use same texture for top for now

		return new Mesh
		{
			vertices = meshVertices,
			triangles = meshTriangles,
			normals = meshNormals,
			uv = meshUV
		};
	}

	public override Mesh CreateColliderMesh(EntityManager EntityManager, Entity entity)
	{
		RectSectionData data = EntityManager.GetComponentData<RectSectionData>(entity);
		Vector3[] colliderVertices = { data.GetPos(0, 0, 0), data.GetPos(0, 0, size.z), data.GetPos(size.x, 0, size.z), data.GetPos(size.x, 0, 0),
					data.GetPos(0, Section.WALL_HEIGHT, 0), data.GetPos(0, Section.WALL_HEIGHT, size.z), data.GetPos(size.x, Section.WALL_HEIGHT, size.z), data.GetPos(size.x, Section.WALL_HEIGHT, 0) };
		int[] colliderTriangles = MeshCreator.MergeTriangles(MeshCreator.RectTri(0, 1, 3, 2), MeshCreator.RectTri(4, 5, 7, 6), MeshCreator.RectTri(0, 1, 4, 5),
			 MeshCreator.RectTri(1, 2, 5, 6), MeshCreator.RectTri(2, 3, 6, 7), MeshCreator.RectTri(3, 0, 7, 4));
		Mesh colliderMesh = new Mesh
		{
			vertices = colliderVertices,
			triangles = colliderTriangles
		};
		return colliderMesh;
	}
}*/