using Unity.Entities;
using Unity.Mathematics;
/*
public class ConstructPusher : Constructable
{
	private PFNode from;
	private Entity fromEntity;
	private float fromMarker;
	private PFNode to;
	private Entity toEntity;
	private float toMarker;
	private BotEntity builder;

	public void Init(PFNode from, PFNode to, BotEntity builder)
	{
		this.from = from;
		fromEntity = Game.map.GetEntity(Game.map.GetToTile(from));
		this.to = to;
		toEntity = Game.map.GetEntity(Game.map.GetToTile(to));
		this.builder = builder;
	}

	protected override ConstructableType GetConstructableType()
	{
		return ConstructableType.Pusher;
	}

	protected override void Construct(EntityManager EntityManager, Entity entity)
	{
		EntityManager.SetComponentData(entity, new TransportationOwner { builder = builder, tax = 0.01f });
		EntityManager.SetComponentData(entity, new Pusher { maxPushDistance = 0, progress = 0, resInfo = ResourceInfo.Invalid });

		SetMarker(EntityManager, entity, fromEntity, BeltObject.State.MARKER_PUSHING, fromMarker);
		SetMarker(EntityManager, entity, toEntity, BeltObject.State.MARKER_PULLING, toMarker);


		UnityEngine.Debug.Log("From: " + fromEntity);
		UnityEngine.Debug.Log("To: " + toEntity);

		DynamicBuffer<BezierCurveNetDistance> netDistance = EntityManager.GetBuffer<BezierCurveNetDistance>(entity);

		float3 fromPos = Game.map.ConnectionPoint(from) + Game.map.ConnectionNormal(from) * ResourceInfo.STANDARD_LENGTH * 0.5f;
		float3 toPos = Game.map.ConnectionPoint(to) + Game.map.ConnectionNormal(to) * ResourceInfo.STANDARD_LENGTH * 0.5f;

		quaternion rot = quaternion.LookRotationSafe(toPos - fromPos, new float3(0, 1, 0));
		netDistance.Add(new BezierCurveNetDistance
		{
			dist = 0,
			posRot = new PosRot { pos = fromPos, rot = rot }
		});
		netDistance.Add(new BezierCurveNetDistance
		{
			dist = 1,
			posRot = new PosRot { pos = toPos, rot = rot }
		});
	}

	private void SetMarker(EntityManager EntityManager, Entity entity, Entity belt, BeltObject.State markerState, float pos)
	{
		DynamicBuffer<BeltObject> beltObjects = EntityManager.GetBuffer<BeltObject>(belt);
		for (int i = 0; i <= beltObjects.Length; i++)
		{
			if (i == beltObjects.Length || pos > beltObjects[i].pos)
			{
				beltObjects.Insert(i, new BeltObject
				{
					state = markerState,
					pos = pos,
					markerRef = entity,
					resInfo = ResourceInfo.Invalid
				});
				break;
			}
		}
	}

	private bool SetMarkerPos(EntityManager EntityManager, Entity belt, PFNode node, ref float pos)
	{
		DynamicBuffer<BeltPath> beltPath = EntityManager.GetBuffer<BeltPath>(EntityManager.GetComponentData<BeltSection>(belt).parentBelt);
		for (int i = 0; i < beltPath.Length; i++)
		{
			if (EntityManager.GetComponentData<BeltSection>(beltPath[i].beltSection).from.Equals(node))
			{
				pos = beltPath[i].dist;
				return true;
			}
		}
		return false;
	}

	protected override bool IsValid(EntityManager EntityManager)
	{
		return fromEntity != toEntity && EntityManager.HasComponent<BeltSection>(fromEntity) && EntityManager.HasComponent<BeltSection>(toEntity) &&
			SetMarkerPos(EntityManager, fromEntity, from, ref fromMarker) &&
			SetMarkerPos(EntityManager, toEntity, to, ref toMarker);
	}

	protected override void InitRender(EntityManager EntityManager, Entity entity)
	{

	}
}*/