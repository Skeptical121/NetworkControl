using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Mathematics;
using System;
using Unity.Entities;
using Unity.Transforms;
using Unity.Rendering;
using Unity.Physics;
using Unity.Collections;

[Serializable]
public class MapObject
{

	public List<Section> sections = new List<Section>();


	// private readonly List<float3> untiledCoalPositions = new List<float3>();
	// public List<PFTile> untakenCoalTiles = new List<PFTile>();

	public MapObject()
	{
	}

	public void Add()
	{
		int numRectangles = 100;
		float radius = 100f;
		int connectionChecks = 500;

		/*for (int i = 0; i < 50; i++)
		{
			untiledCoalPositions.Add(new float3(Variance.Range(-radius, radius), 0f, Variance.Range(-radius, radius)));
		}*/

		List<RectSection> rectangles = new List<RectSection>();
		for (int i = 0; i < numRectangles; i++)
		{
			RectSection r = AttemptAddSection(new RectSection(new float3(Variance.Range(-radius, radius), 0, Variance.Range(-radius, radius)),
				Quaternion.Euler(0, Variance.Range(0f, 360f), 0) * Vector3.forward, new int3(16, 6, 32)));
			if (r != null)
			{
				rectangles.Add(r);
			}
		}

		long start = Game.NanoTime();
		for (int k = 0; k < connectionChecks; k++)
		{
			int a = Variance.NextInt(rectangles.Count);
			int b = Variance.NextInt(rectangles.Count);
			if (a != b && Vector3.Distance(rectangles[a].GetCenter(), rectangles[b].GetCenter()) < 60f)
			{
				ConnectionHandler.ConnectWithPath(24, 8, 6, rectangles[a], rectangles[b]);
			}
		}
		Debug.Log("Connected paths in time = " + (Game.NanoTime() - start) / 1000000f + "ms");
	}

	/*public void OnAddTileWhereYIs0(PFTile tile)
	{
		if (Variance.Chance(0.05f))
		{
			CoalSpawn coal = new CoalSpawn(tile, 50f);
			sections[tile.section].coalSpawns.Add(coal);
		}*/


		/*Assert.IsTrue(tile.y == 0, "Y must be 0");
		float3 tilePos = Game.map.WorldPosition(tile);
		for (int i = 0; i < untiledCoalPositions.Count; i++)
		{
			// Be absolutely sure that the tile is selected somewhere:
			if (Vector3.Distance(untiledCoalPositions[i], tilePos) <= (MapInfo.TILE_LENGTH + MapInfo.MAX_TILE_LENGTH_DIFFERENCE))
			{
				untakenCoalTiles.Add(tile);
				untiledCoalPositions.RemoveAt(i);

				// Add a "marker" entity here, so no buildings get placed here?
				// Or is that okay?
				// A marker entity that can be completly ignored?
				return;
			}
		}*/
	//}

	// Returns id
	public int OnAddSection(Section section)
	{
		sections.Add(section);
		return sections.Count - 1;
	}

	public void InitSection(Section section)
	{
		Game.map.sectionSize.Add(section.size);
		Game.map.sectionStartIndex.Add(Game.map.sectionStartIndex[Game.map.sectionStartIndex.Length - 1] + section.size.x * section.size.y * section.size.z); // This list is 1 larger than sectionSize
		Entity entity = World.Active.EntityManager.CreateEntity(
			typeof(VisibleSection),
			typeof(RenderEntityInSection),
			typeof(ESection),
			typeof(SubMeshRenderer));
		World.Active.EntityManager.GetBuffer<VisibleSection>(entity).Add(new VisibleSection { Value = entity });
		World.Active.EntityManager.SetComponentData(entity, new ESection { visible = false });


		Entity renderer1 = World.Active.EntityManager.CreateEntity(
			typeof(Translation),
			typeof(RenderMesh),
			typeof(LocalToWorld),
			typeof(PhysicsCollider));
		/*Entity renderer2 = World.Active.EntityManager.CreateEntity(
			typeof(Translation),
			typeof(RenderMesh),
			typeof(LocalToWorld));*/

		// RenderMesh rm = section.CreateMesh();

		// BlobAssetReference<Unity.Physics.Collider> collider = Unity.Physics.MeshCollider.Create(new NativeArray<float3>(rm.mesh.vertices, Allocator.Temp));
		// World.Active.EntityManager.SetComponentData(renderer1, new PhysicsCollider { Value = collider });

		World.Active.EntityManager.SetSharedComponentData(renderer1, section.CreateMesh());
		// World.Active.EntityManager.SetSharedComponentData(renderer2, section.UpdateWallMesh());

		World.Active.EntityManager.GetBuffer<SubMeshRenderer>(entity).Add(new SubMeshRenderer { renderer = renderer1 });
		// World.Active.EntityManager.GetBuffer<SubMeshRenderer>(entity).Add(new SubMeshRenderer { renderer = renderer2 });

		Game.map.sectionRender.Add(entity);
		Game.map.InitSection(section);
	}

	public T AttemptAddSection<T>(T section) where T : Section
	{
		if (section.CanAdd())
		{
			section.Add();
			return section;
		}
		return null;
	}

	public CurveSection AttemptConnect(short pathWidth, short pathHeight, Side a, int aOffset, Side b, int bOffset)
	{
		// So... it's just possible:
		float3 aPos = a.Get(aOffset + pathWidth / 2f);
		float3 bPos = b.Get(bOffset + pathWidth / 2f);

		float dist = Vector3.Distance(aPos, bPos);
		CurveSection path = new CurveSection(pathWidth, pathHeight, new float3[] { aPos, aPos + a.sideInfo.normal * dist * 0.4f, bPos + b.sideInfo.normal * dist * 0.4f, bPos });
		return AttemptAddSection(path);
	}

	// Obviously this should only update nodes that are actually effected..
	public void UpdateKnownVisibilities()
	{

		// Assumes static map for now..
		/*long start = Game.NanoTime();
		for (int i = 0; i < sections.Count; i++)
		{
			sections[i].gameRep.name = "Section " + i;
			for (int j = i + 1; j < sections.Count; j++)
			{
				if (sections[i].ShouldBeVisible(sections[j]))
				{
					sections[i].visibleNodesFromThisNode.Add(sections[j]);
					sections[j].visibleNodesFromThisNode.Add(sections[i]);
				}
				//Debug.Log("Sections: " + Game.map.nodes.IndexOf(node) + " at " + node.GetCenter() + ", " + Game.map.nodes.IndexOf(other) + " at " + other.GetCenter() + 
				//	". Visible = " + node.ShouldBeVisible(other));
			}
		}
		Debug.Log("Updated visiblities in " + (Game.NanoTime() - start) / 1000000f + "ms");*/
	}

	/*public void UpdateTex(int x, int y)
	{
		tex.SetPixel(x, y, new Color(resources[x, y] / (float)max, 0, 0));
		tex.Apply();
	}*/
}