using System;
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


public struct BeltSection : IComponentData
{
	public Entity parentBelt;
	public PFNode from;
	public PFNode to;
}

public struct BeltParent : IComponentData
{
	public float dist;
	public PFNode input;
	public PFNode output;
}


// Information to recreate the belt when it is divided:
[InternalBufferCapacity(0)]
public struct BeltPath : IBufferElementData
{
	public Entity beltSection;
	public float dist;
	public int curveIndexStart;
}

[InternalBufferCapacity(2)]
public struct SubMeshRenderer : IBufferElementData
{
	public Entity renderer;
}

[InternalBufferCapacity(0)]
public struct BeltObject : IBufferElementData
{
	public enum State : byte
	{
		BELT_OBJECT,
		MARKER_PUSHING,
		PUSHING,
		MARKER_PULLING,
		PULLING
	}

	public State state; // Does it exist, essentially
	public Entity markerRef;
	public float pos; // pos refers to the back
	public ResourceInfo resInfo;
}

[UpdateInGroup(typeof(MainSimSystemGroup))]
public class BeltSystem : JobComponentSystem
{
	private EndMainSimEntityCommandBufferSystem barrier;

	protected override void OnCreate()
	{
		barrier = World.GetOrCreateSystem<EndMainSimEntityCommandBufferSystem>();
		base.OnCreate();
	}

	[BurstCompile]
	struct BeltMoveJob : IJobForEachWithEntity_EBC<BeltObject, BeltParent>
	{
		// public EntityManager
		// public ComponentDataFromEntity<BeltObject> beltObjectFromEntity;
		// [NativeDisableParallelForRestriction] public ComponentDataFromEntity<Marker> markerFromEntity;

		[NativeDisableParallelForRestriction] public ComponentDataFromEntity<BeltObjectInterp> beltInterp;
		[NativeDisableParallelForRestriction] public ComponentDataFromEntity<Pusher> pusherFromEntity;
		public float tickTime;

		public void Execute(Entity beltEntity, int index, DynamicBuffer<BeltObject> beltObjects, [ReadOnly] ref BeltParent belt)
		{
			if (beltObjects.Length > 0)
			{
				float next = belt.dist + beltObjects[0].resInfo.length;
				for (int i = 0; i < beltObjects.Length; i++)
				{
					BeltObject beltObject = beltObjects[i];
					switch (beltObject.state)
					{
						case BeltObject.State.BELT_OBJECT:
							beltObject.pos += GetBeltSpeed() * tickTime;

							if (beltObject.pos + beltObject.resInfo.length > next)
							{
								beltObject.pos = next - beltObject.resInfo.length;
							}

							beltObjects[i] = beltObject;
							BeltObjectRenderSystem.SetInterp(beltInterp, beltObject.pos, beltObject.resInfo);

							if (i > 0 && (beltObjects[i - 1].state == BeltObject.State.MARKER_PUSHING || beltObjects[i - 1].state == BeltObject.State.MARKER_PULLING) && beltObject.pos > beltObjects[i - 1].pos)
							{
								// Test if can be pushed..
								if (beltObjects[i - 1].state == BeltObject.State.MARKER_PUSHING && pusherFromEntity[beltObjects[i - 1].markerRef].IsWaitingAtFrom())
								{
									BeltObject markerBeltObject = beltObjects[i - 1];

									markerBeltObject.resInfo = beltObject.resInfo;
									markerBeltObject.state = BeltObject.State.PUSHING;

									Pusher pusher = pusherFromEntity[markerBeltObject.markerRef];
									pusher.resInfo = beltObject.resInfo;
									pusher.maxPushDistance = 0; // Wait for puller to say how far it can push..
									pusherFromEntity[markerBeltObject.markerRef] = pusher;
									BeltObjectRenderSystem.SetParent(beltInterp, beltObject.resInfo.renderEntity, markerBeltObject.markerRef, 0, 0);

									beltObjects[i - 1] = markerBeltObject;

									beltObjects.RemoveAt(i);
									i--;
								}
								else
								{
									// Go ahead of marker:
									beltObjects[i] = beltObjects[i - 1];
									beltObjects[i - 1] = beltObject;
								}
							}

							next = beltObjects[i].pos;
							break;
						case BeltObject.State.MARKER_PUSHING:
							break;
						case BeltObject.State.PUSHING:
							if (!pusherFromEntity[beltObject.markerRef].IsBlockingFrom())
							{
								beltObject.resInfo = ResourceInfo.Invalid; // Not needed
								beltObject.state = BeltObject.State.MARKER_PUSHING;
								beltObjects[i] = beltObject;
							}
							else
							{
								next = beltObject.pos;
							}
							break;
						case BeltObject.State.MARKER_PULLING:
						{
							Pusher pusher = pusherFromEntity[beltObjects[i].markerRef];
							if (!pusher.resInfo.IsInvalid())
							{
								if (pusher.IsBlockingTo())
								{
									// Switch to PULLING
									beltObject.resInfo = pusher.resInfo;
									beltObject.state = BeltObject.State.PULLING;
									beltObjects[i] = beltObject;
									pusher.maxPushDistance = 1;
									break;
								}
								
								if (CanPushInto(beltObjects, next, pusher.resInfo.length, i))
								{
									pusher.maxPushDistance = 1;
								}
								else
								{
									pusher.maxPushDistance = pusher.GetBlocking() - Pusher.epsilon; // Depends on the type of pusher for this...
								}
								pusherFromEntity[beltObjects[i].markerRef] = pusher;
							}
							break;
						}
						case BeltObject.State.PULLING:
						{
							Pusher pusher = pusherFromEntity[beltObjects[i].markerRef];
							if (pusher.progress == 1)
							{
								pusher.resInfo = ResourceInfo.Invalid; // Needed!
								pusherFromEntity[beltObjects[i].markerRef] = pusher;

								beltObjects.Insert(i, new BeltObject
								{
									markerRef = Entity.Null,
									pos = beltObject.pos,
									resInfo = beltObject.resInfo,
									state = BeltObject.State.BELT_OBJECT
								});
								BeltObjectRenderSystem.SetParent(beltInterp, beltObject.resInfo.renderEntity, beltEntity, beltObject.pos, beltObject.resInfo.length * 0.5f);

								beltObject.resInfo = ResourceInfo.Invalid; // Not needed
								beltObject.state = BeltObject.State.MARKER_PULLING;
								beltObjects[i + 1] = beltObject;
							}
							next = beltObject.pos;
							break;
						}
					}
				}
			}
		}

		private bool CanPushInto(DynamicBuffer<BeltObject> beltObjects, float next, float pusherLength, int i)
		{
			if (next >= beltObjects[i].pos + pusherLength)
			{
				// Go backwards:
				int n = i + 1;
				while (n < beltObjects.Length && CanPushInto(beltObjects[n].state))
				{
					n++;
				} 
				if (n >= beltObjects.Length)
					return true;
				else
					return beltObjects[n].pos + beltObjects[n].resInfo.length - Pusher.epsilon < beltObjects[i].pos;
			}
			return false;
		}

		private bool CanPushInto(BeltObject.State state)
		{
			switch (state)
			{
				case BeltObject.State.MARKER_PULLING: return true;
				case BeltObject.State.MARKER_PUSHING: return true;
				case BeltObject.State.PULLING: return false;
				case BeltObject.State.PUSHING: return false;
				case BeltObject.State.BELT_OBJECT: return false;
				default: return false;
			}
		}

		private float GetBeltSpeed()
		{
			return 1f * MapInfo.TILE_LENGTH;
		}
	}

	[BurstCompile]
	struct OutputTick : IJobForEach_BC<BeltObject, BeltParent>
	{
		public MapInfo map;
		public ComponentDataFromEntity<Constructing> constructingFromEntity;

		// Multiple conveyor belts can feed into the same resourceInput...
		public BufferFromEntity<NCOElement> ncoElementsFromEntity;
		public ComponentDataFromEntity<BuildingInfo> ncoInfoFromEntity;
		public ComponentDataFromEntity<CenterTile> centerTileFromEntity;
		// public NativeList<Entity> entitiesToRemove;
		public EntityCommandBuffer CommandBuffer;
		public float worldTime;

		public void Execute(DynamicBuffer<BeltObject> beltObjects, [ReadOnly] ref BeltParent belt)
		{
			Entity beltOutput = map.GetEntity(map.GetToTile(belt.output));
			if (beltOutput != Entity.Null)
			{
				if (beltObjects.Length > 0 && beltObjects[0].state == BeltObject.State.BELT_OBJECT && beltObjects[0].pos >= belt.dist - 0.0001f) // It should be exactly equal if it is waiting
				{
					BeltObject beltObject = beltObjects[0];
					

					if (ncoInfoFromEntity.HasComponent(beltOutput))
					{
						DestroyEndBeltObject(beltObjects);
						BuildingInfo ncoInfo = ncoInfoFromEntity[beltOutput];
						DynamicBuffer<NCOElement> ncoElements = ncoElementsFromEntity[beltOutput];

						ncoInfoFromEntity[beltOutput] = ncoInfo;
						for (int i = 0; i < ncoElements.Length; i++)
						{
							if (ncoElements[i].owner.id == beltObject.resInfo.owner.id)
							{
								NCOElement ncoElement = ncoElements[i];
								ncoElement.nco += beltObject.resInfo.amount;
								ncoElements[i] = ncoElement;
								return;
							}
						}
						ncoElements.Add(new NCOElement { owner = beltObject.resInfo.owner, nco = beltObject.resInfo.amount });
					}
					else if (constructingFromEntity.HasComponent(beltOutput))
					{
						DestroyEndBeltObject(beltObjects);
						Constructing c = constructingFromEntity[beltOutput];
						c.facade = false;
						c.progress += beltObject.resInfo.amount;
						constructingFromEntity[beltOutput] = c;
					}
				}
			}

			Entity beltInput = map.GetEntity(belt.input.tile);
			if (beltInput != Entity.Null)
			{
				if (beltObjects.Length == 0 || beltObjects[beltObjects.Length - 1].pos >= 0)
				{
					if (ncoInfoFromEntity.HasComponent(beltInput))
					{
						BuildingInfo buildingInfo = ncoInfoFromEntity[beltInput];
						if (buildingInfo.owner.id != Bot.INVALID)
						{
							beltObjects.Add(new BeltObject
							{
								markerRef = Entity.Null,
								pos = -ResourceInfo.STANDARD_LENGTH,
								resInfo = new ResourceInfo(buildingInfo.owner, /*worldTime, beltInput, */Entity.Null, ResourceInfo.STANDARD_LENGTH)
							});
						}
					}
				}
			}
		}

		private void DestroyEndBeltObject(DynamicBuffer<BeltObject> beltObjects)
		{
			Entity renderEntity = beltObjects[0].resInfo.renderEntity;
			beltObjects.RemoveAt(0);
			if (renderEntity != Entity.Null)
			{
				CommandBuffer.DestroyEntity(renderEntity); // So, the other option is to create a list so it batch removes?
			}
		}
	}

	protected override JobHandle OnUpdate(JobHandle inputDeps)
	{
		JobHandle job = new OutputTick
		{
			map = Game.map,
			constructingFromEntity = GetComponentDataFromEntity<Constructing>(),
			ncoElementsFromEntity = GetBufferFromEntity<NCOElement>(),
			ncoInfoFromEntity = GetComponentDataFromEntity<BuildingInfo>(),
			worldTime = Game.GetGameTime(),
			centerTileFromEntity = GetComponentDataFromEntity<CenterTile>(),
			CommandBuffer = barrier.CreateCommandBuffer()
		}.ScheduleSingle(this, inputDeps);

		job = new BeltMoveJob
		{
			tickTime = Game.GetTickTime(),
			pusherFromEntity = GetComponentDataFromEntity<Pusher>(),
			beltInterp = GetComponentDataFromEntity<BeltObjectInterp>()
		}.Schedule(this, job);

		Vector2 offset = new Vector2(0, 0 - Game.GetGameTime());
		RenderInfo.self.conveyorBeltMat.SetTextureOffset("_MainTex", offset);

		barrier.AddJobHandleForProducer(job);
		return job;
	}
}