using System.Collections;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Rendering;
using Unity.Transforms;
using UnityEngine;

/*
	// METHODS FOR MAIN THREAD ONLY:
	public static float3 GetWorldPosition(Entity section, int edgeX, int edgeY, int edgeZ)
	{
		if (World.Active.EntityManager.HasComponent<RectSectionData>(section))
			return World.Active.EntityManager.GetComponentData<RectSectionData>(section).GetPos(edgeX * 0.5f, edgeY * 0.5f, edgeZ * 0.5f);
		else
			return World.Active.EntityManager.GetBuffer<CurveSectionData>(section).GetPos(
				World.Active.EntityManager.GetComponentData<ESection>(section), edgeZ, edgeX * 0.5f, edgeY * 0.5f);
	}

	public static float3 GetWorldNormal(Entity section, byte dir, byte x, byte y, byte z)
	{
		if (World.Active.EntityManager.HasComponent<RectSectionData>(section))
			return World.Active.EntityManager.GetComponentData<RectSectionData>(section).GetWorldNormal(dir);
		else
			return World.Active.EntityManager.GetBuffer<CurveSectionData>(section).GetWorldNormal(dir, x, y, z);
	}





	public static float3 GetCenter(EntityManager EntityManager, Entity section)
	{
		ESection sectionData = World.Active.EntityManager.GetComponentData<ESection>(section);
		if (World.Active.EntityManager.HasComponent<RectSectionData>(section))
		{
			return World.Active.EntityManager.GetComponentData<RectSectionData>(section).GetPos(sectionData.size.x * 0.5f, 0f, sectionData.size.z * 0.5f);
		}
		else
		{
			DynamicBuffer<CurveSectionData> curveSectionData = World.Active.EntityManager.GetBuffer<CurveSectionData>(section);
			return (curveSectionData[0].pos + curveSectionData[curveSectionData.Length - 1].pos) / 2;
		}
	}

	public static bool InBounds(EntityManager EntityManager, Entity section, float3 worldPosition)
	{
		ESection sectionData = World.Active.EntityManager.GetComponentData<ESection>(section);
		if (World.Active.EntityManager.HasComponent<RectSectionData>(section))
		{
			RectSectionData rectSectionData = World.Active.EntityManager.GetComponentData<RectSectionData>(section);
			float3 offset = worldPosition - rectSectionData.corner;
			float x = math.dot(offset, rectSectionData.xDir);
			float z = math.dot(offset, rectSectionData.zDir);
			return x >= 0 && x <= sectionData.size.x * MapInfo.TILE_LENGTH &&
				   offset.y >= 0 && offset.y <= sectionData.size.y * MapInfo.TILE_HEIGHT &&
				   z >= 0 && z <= sectionData.size.z * MapInfo.TILE_LENGTH;
		}
		else
		{
			DynamicBuffer<CurveSectionData> curveSectionData = World.Active.EntityManager.GetBuffer<CurveSectionData>(section);
			// Should do initial bounds check...
			Vector2 worldPos = new Vector2(worldPosition.x, worldPosition.z);
			for (int i = 0; i < sectionData.size.z; i++)
			{
				Vector3 a = curveSectionData.GetPos(sectionData, i * 2, 0, 0);
				Vector3 b = curveSectionData.GetPos(sectionData, i * 2, sectionData.size.x, 0);
				Vector3 c = curveSectionData.GetPos(sectionData, i * 2 + 2, 0, 0);
				Vector3 d = curveSectionData.GetPos(sectionData, i * 2 + 2, sectionData.size.x, 0);
				if (PointInTriangle(worldPos, new Vector2(a.x, a.z), new Vector2(b.x, b.z), new Vector2(c.x, c.z)) ||
					PointInTriangle(worldPos, new Vector2(b.x, b.z), new Vector2(c.x, c.z), new Vector2(d.x, d.z)))
					return true;
			}
			return false;
		}
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
}

public abstract class ISectionConstructable
{
	public abstract Entity AttemptAdd(EntityManager EntityManager); // Also creates the entity...
	public abstract Mesh CreateRenderMesh(EntityManager EntityManager, Entity entity);
	public abstract Mesh CreateColliderMesh(EntityManager EntityManager, Entity entity);

	public void Init(EntityManager EntityManager, Entity entity)
	{


		// Init Render:
		Mesh renderMesh = CreateRenderMesh(EntityManager, entity);
		EntityManager.SetSharedComponentData(entity, new RenderMesh { mesh = renderMesh });
	}

	public void AddSide(DynamicBuffer<UnlinkedSide> unlinked, byte sideIndex, byte length, Vector3 start, Vector3 normal, Vector3 tangent, byte dir)
	{
		unlinked.Add(new UnlinkedSide { Value = new Side(length, start, 0, new Side.ConstantSideInfo(normal, tangent, sideIndex, dir)) });
	}
}*/

public struct ESection : IComponentData
{
	public bool visible;
	// public int3 size;
}

public struct VisibleSection : IBufferElementData
{
	public Entity Value;
}

// NOT used for simulation
public struct RenderEntityInSection : IBufferElementData
{
	public Entity Value;
}

public class SectionVisibleSystem : JobComponentSystem
{


	protected override JobHandle OnUpdate(JobHandle inputDeps)
	{
		return inputDeps;
	}

	public void AddSection()
	{

	}
}
