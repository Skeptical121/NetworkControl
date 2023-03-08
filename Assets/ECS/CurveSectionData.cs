using System.Collections;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Rendering;
using Unity.Transforms;
using UnityEngine;

/*
public struct CurveSectionData : IBufferElementData
{
	public float3 pos;
	public float3 dir;
	public float3 tan;
}

public static class CurveSectionExt
{
	public static float3 GetPos(this DynamicBuffer<CurveSectionData> curve, ESection section, int zIndex, float x, float y)
	{
		return curve[zIndex].pos + curve[zIndex].tan * (x - section.size.x / 2f) * MapInfo.TILE_LENGTH + new float3(0, y * MapInfo.TILE_HEIGHT, 0);
	}

	public static float3 GetPos(this NativeArray<CurveSectionData> curve, int3 size, int zIndex, float x, float y)
	{
		return curve[zIndex].pos + curve[zIndex].tan * (x - size.x / 2f) * MapInfo.TILE_LENGTH + new float3(0, y * MapInfo.TILE_HEIGHT, 0);
	}

	public static float3 GetWorldNormal(this DynamicBuffer<CurveSectionData> curve, byte dir, byte x, byte y, byte z)
	{
		switch (dir)
		{
			case 0: return -curve[z * 2 + 1].tan;
			case 1: return curve[z * 2 + 1].tan;
			case 2: return -curve[z * 2].dir;
			case 3: return curve[z * 2 + 2].dir;
			case 4: return new float3(0, -1, 0);
			case 5: return new float3(0, 1, 0);
			default: Assert.Fail("Invalid: " + dir); return new float3(0, 0, 0);
		}
	}
}

public class CurveSectionConstruct : ISectionConstructable
{
	// BezierCurve is not saved
	private int3 size;
	private BezierCurve curve;

	public CurveSectionConstruct(int width, int height, BezierCurve curve)
	{
		size = new int3(width, height, math.max(1, Mathf.RoundToInt(curve.GetTotalDistance() / MapInfo.TILE_LENGTH)));
		this.curve = curve;
	}

	public override Entity AttemptAdd(EntityManager EntityManager)
	{
		NativeArray<CurveSectionData> curveSectionData = new NativeArray<CurveSectionData>(size.z * 2 + 1, Allocator.Temp);

		float boxSize = MapInfo.TILE_LENGTH - MapInfo.MAX_TILE_LENGTH_DIFFERENCE;
		Vector3 prevPos0 = Vector3.zero;
		Vector3 prevPos1 = Vector3.zero;
		for (byte z = 0; z <= size.z; z++)
		{
			float t = curve.GetMapped(z / (float)size.z);
			Vector3 prevPos = curve.GetPos(t);
			Vector3 prevDir = curve.GetDir(t);
			Vector3 prevTan = BezierCurve.GetTan(prevDir);

			curveSectionData[z * 2] = new CurveSectionData { pos = prevPos, dir = prevDir, tan = prevTan };

			if (z < size.z)
			{
				t = curve.GetMapped((z + 0.5f) / size.z);
				Vector3 curvePos = curve.GetPos(t);
				Vector3 dir = curve.GetDir(t);
				Vector3 tan = BezierCurve.GetTan(dir);
				curveSectionData[z * 2 + 1] = new CurveSectionData { pos = curvePos, dir = dir, tan = tan };

				if (Physics.CheckBox(curvePos + Vector3.up * size.y / 2f * MapInfo.TILE_HEIGHT,
					new Vector3(CurveSection.EXTRA_COLLIDER_WIDTH + size.x / 2f * MapInfo.TILE_LENGTH, size.y / 2f * MapInfo.TILE_HEIGHT, boxSize / 2f), Quaternion.LookRotation(dir), Section.NODE_LAYER))
				{
					return Entity.Null;
				}


			}

			if (z == 0)
			{
				prevPos0 = curveSectionData.GetPos(size, 0, 0, 0);
				prevPos1 = curveSectionData.GetPos(size, 0, size.x, 0);
			}
			else
			{
				Vector3 nextPos0 = curveSectionData.GetPos(size, z * 2, 0, 0);
				Vector3 nextPos1 = curveSectionData.GetPos(size, z * 2, size.x, 0);

				float estimatedDist0 = Vector3.Dot(curveSectionData[z * 2 - 2].dir, nextPos0 - prevPos0);
				float estimatedDist1 = Vector3.Dot(curveSectionData[z * 2 - 2].dir, nextPos1 - prevPos1);

				// EstimatedDist is a really good approximation, so we can use that
				if (Mathf.Abs(MapInfo.TILE_LENGTH - estimatedDist0) >= MapInfo.MAX_TILE_LENGTH_DIFFERENCE || Mathf.Abs(MapInfo.TILE_LENGTH - estimatedDist1) >= MapInfo.MAX_TILE_LENGTH_DIFFERENCE)
				{
					return Entity.Null;
				}

				prevPos0 = nextPos0;
				prevPos1 = nextPos1;
			}
		}

		Entity entity = EntityManager.CreateEntity(
			typeof(CurveSectionData),
			typeof(LinkedSide),
			typeof(UnlinkedSide),
			typeof(VisibleSection),
			typeof(RenderEntityInSection),
			typeof(ESection),
			typeof(Translation),
			typeof(RenderMesh),
			typeof(LocalToWorld));

		EntityManager.SetComponentData(entity, new ESection { size = size, visible = false });
		
		AddSide(EntityManager.GetBuffer<UnlinkedSide>(entity), 0, (byte)size.x, curveSectionData.GetPos(size, 0, 0, 0), -curveSectionData[0].dir, curveSectionData[0].tan, 2);
		AddSide(EntityManager.GetBuffer<UnlinkedSide>(entity), (byte)(size.z - 1), (byte)size.x, curveSectionData.GetPos(size, size.z * 2, 0, 0), curveSectionData[size.z].dir, curveSectionData[curveSectionData.Length - 1].tan, 3);
		EntityManager.GetBuffer<CurveSectionData>(entity).AddRange(curveSectionData);
		return entity;
	}

	public override Mesh CreateRenderMesh(EntityManager EntityManager, Entity entity)
	{
		Mesh renderMesh = CreateFlatRenderMesh(EntityManager, entity, 0, true);
		Mesh renderMeshCeiling = CreateFlatRenderMesh(EntityManager, entity, Section.WALL_HEIGHT, false);
		return MeshCreator.CombineMeshes(true, renderMesh, renderMeshCeiling);
	}


	public override Mesh CreateColliderMesh(EntityManager EntityManager, Entity entity)
	{
		DynamicBuffer<CurveSectionData> curveSectionData = EntityManager.GetBuffer<CurveSectionData>(entity);
		ESection section = EntityManager.GetComponentData<ESection>(entity);
		Vector3[] vertices = new Vector3[4 * (size.z + 1)];
		int[] triangles = new int[3 * (size.z * 8 + 4)];
		for (int z = 0; z <= size.z; z++)
		{
			vertices[z * 4] = curveSectionData.GetPos(section, z * 2, -CurveSection.EXTRA_COLLIDER_WIDTH / MapInfo.TILE_LENGTH, 0);
			vertices[z * 4 + 1] = curveSectionData.GetPos(section, z * 2, size.x + CurveSection.EXTRA_COLLIDER_WIDTH / MapInfo.TILE_LENGTH, 0);
			vertices[z * 4 + 2] = curveSectionData.GetPos(section, z * 2, -CurveSection.EXTRA_COLLIDER_WIDTH / MapInfo.TILE_LENGTH, Section.WALL_HEIGHT);
			vertices[z * 4 + 3] = curveSectionData.GetPos(section, z * 2, size.x + CurveSection.EXTRA_COLLIDER_WIDTH / MapInfo.TILE_LENGTH, Section.WALL_HEIGHT);
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

	private Mesh CreateFlatRenderMesh(EntityManager EntityManager, Entity entity, byte y, bool facingUp)
	{
		DynamicBuffer<CurveSectionData> curveSectionData = EntityManager.GetBuffer<CurveSectionData>(entity);
		ESection section = EntityManager.GetComponentData<ESection>(entity);
		Vector3[] meshVertices = new Vector3[2 * (size.z + 1)];
		Vector3[] meshNormals = new Vector3[2 * (size.z + 1)];
		Vector2[] meshUV = new Vector2[2 * (size.z + 1)];
		// List<Vector3> meshUV = new List<Vector3>(2 * (resolution + 1));
		int[] meshTriangles = new int[3 * size.z * 2];
		for (int z = 0; z <= size.z; z++)
		{
			meshVertices[z * 2] = curveSectionData.GetPos(section, z * 2, size.x, y);
			meshVertices[z * 2 + 1] = curveSectionData.GetPos(section, z * 2, 0, y);

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
}*/