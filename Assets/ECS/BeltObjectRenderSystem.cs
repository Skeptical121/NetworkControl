using System.Collections;
using System.Collections.Generic;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Rendering;
using Unity.Transforms;
using UnityEngine;

public struct AdvanceInterp : IComponentData {}

public struct BeltObjectInterp : IComponentData
{
	public Entity parent;
	public float fromPos;
	public float toPos;
	public float forwardOffset;
}

[UpdateInGroup(typeof(RenderSystemGroup))]
public class BeltObjectRenderSystem : JobComponentSystem
{

	protected override void OnCreate()
	{
		base.OnCreate();
	}

	// This stuff can be done in parallel..
	[BurstCompile]
	struct BeltObjectRenderJob : IJobForEach<BeltObjectInterp, Translation, Rotation>
	{
		[ReadOnly] public BufferFromEntity<BezierCurveNetDistance> curve;
		public float t; // Interpolation value (already pruned to be from 0-1)

		public void Execute([ReadOnly] ref BeltObjectInterp interp,
			ref Translation translation, ref Rotation rotation)
		{
			float interpVal = math.lerp(interp.fromPos, interp.toPos, t);
			interpVal += interp.forwardOffset;
			PosRot posRot = curve[interp.parent].AsNativeArray().GetMapped(interpVal);
			translation.Value = posRot.pos;
			rotation.Value = posRot.rot;
		}
	}

	// [BurstCompile]
	// struct BeltRenderJob : IJobForEach_BCCC<BezierCurveNetDistance, >

	protected override JobHandle OnUpdate(JobHandle inputDeps)
	{
		BeltObjectRenderJob job = new BeltObjectRenderJob
		{
			curve = GetBufferFromEntity<BezierCurveNetDistance>(true),
			t = ECSHandler.interp
		};

		return job.Schedule(this, inputDeps);
	}


	public static void SetParent(ComponentDataFromEntity<BeltObjectInterp> beltInterp, Entity renderEntity, Entity parentEntity, float pos, float forwardOffset)
	{
		if (renderEntity != Entity.Null)
		{
			beltInterp[renderEntity] = new BeltObjectInterp { fromPos = pos, toPos = pos, parent = parentEntity, forwardOffset = forwardOffset };
		}
	}

	public static void SetInterp(ComponentDataFromEntity<BeltObjectInterp> beltInterp, float pos, ResourceInfo resInfo)
	{
		if (resInfo.renderEntity != Entity.Null)
		{
			BeltObjectInterp interp = beltInterp[resInfo.renderEntity];
			interp.fromPos = interp.toPos;
			interp.toPos = pos; // Center it
			beltInterp[resInfo.renderEntity] = interp;
		}
	}
}

[UpdateInGroup(typeof(SimSystemGroup))]
[UpdateAfter(typeof(MainSimSystemGroup))]
[UpdateBefore(typeof(EndMainSimEntityCommandBufferSystem))] // hmm
public class InitRenderBeltObjectSystem : JobComponentSystem
{
	private EntityArchetype beltObjectArch;
	private NativeList<RenderObjectAdd> add;

	protected override void OnCreate()
	{
		add = new NativeList<RenderObjectAdd>(Allocator.Persistent);
		beltObjectArch = World.EntityManager.CreateArchetype(
			typeof(Translation),
			typeof(Rotation),
			typeof(RenderMesh),
			typeof(LocalToWorld),
			typeof(BeltObjectInterp));
		base.OnCreate();
	}

	[BurstCompile]
	struct GetInitRenderObjectInfoJob : IJobForEachWithEntity_EB<BeltObject>
	{
		public NativeList<RenderObjectAdd> add;
		public void Execute(Entity belt, int index, DynamicBuffer<BeltObject> beltObjects)
		{
			// If is visible:
			for (int i = beltObjects.Length - 1; i >= 0; i--)
			{
				if (beltObjects[i].state == BeltObject.State.BELT_OBJECT && beltObjects[i].resInfo.renderEntity == Entity.Null)
				{
					add.Add(new RenderObjectAdd { addIndex = i, beltObject = beltObjects[i], belt = belt });
				}
				else
				{
					break;
				}
			}
		}
	}

	struct RenderObjectAdd
	{
		public Entity belt;
		public BeltObject beltObject;
		public int addIndex;
	}

	protected override JobHandle OnUpdate(JobHandle inputDeps)
	{
		add.Clear();
		GetInitRenderObjectInfoJob job = new GetInitRenderObjectInfoJob
		{
			add = add
		};

		JobHandle handle = job.ScheduleSingle(this, inputDeps);
		handle.Complete(); // So, it is important that nothing else ticks, (or nothing related, anyways), since it has data
		// Hopefully that is faster than just running it on the main thread..
		int num = add.Length;
		if (num > 0)
		{
			// NativeArray<Entity> entities = new NativeArray<Entity>(num, Allocator.Temp);
			// EntityManager.CreateEntity(beltObjectArch, entities);
			for (int i = 0; i < num; i++)
			{
				Entity entity = EntityManager.CreateEntity(beltObjectArch);
				EntityManager.SetComponentData(entity, new BeltObjectInterp { fromPos = -add[i].beltObject.resInfo.length, toPos = -add[i].beltObject.resInfo.length, parent = add[i].belt, forwardOffset = add[i].beltObject.resInfo.length * 0.5f });
				EntityManager.SetSharedComponentData(entity, new RenderMesh
				{
					mesh = RenderInfo.self.beltObject,
					material = RenderInfo.BeltObject[add[i].beltObject.resInfo.owner.id]
				});
				BeltObject beltObject = add[i].beltObject;
				beltObject.resInfo.renderEntity = entity;
				DynamicBuffer<BeltObject> beltObjects = EntityManager.GetBuffer<BeltObject>(add[i].belt);
				beltObjects[add[i].addIndex] = beltObject;
			}
		}

		return handle;
	}

	protected override void OnDestroy()
	{
		base.OnDestroy();
		add.Dispose();
	}
}
