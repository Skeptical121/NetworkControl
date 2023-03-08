using System.Collections;
using System.Collections.Generic;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using UnityEngine;
/*
[UpdateInGroup(typeof(MainSimSystemGroup))]
public class TaxSystem : JobComponentSystem
{
	// Obviously this shouldn't run every tick..
	[BurstCompile]
	struct TaxTick : IJobForEach_BC<BeltObject, TransportationOwner>
	{
		public ComponentDataFromEntity<BotCredit> botCreditFromEntity;
		public float tickTime;
		public void Execute([ReadOnly] DynamicBuffer<BeltObject> beltObjects, [ReadOnly] ref TransportationOwner owner)
		{
			for (int i = 0; i < beltObjects.Length; i++)
			{
				// Only collect taxes from those you don't own:
				if (beltObjects[i].resInfo.owner.id != owner.builder.id)
				{
					BotCredit bc = botCreditFromEntity[owner.builder.entity];
					bc.credit += owner.GetTax(tickTime);
					botCreditFromEntity[owner.builder.entity] = bc;
				}
			}
		}
	}

	[BurstCompile]
	struct BuildingTax : IJobForEach_BC<NCOElement, BuildingInfo>
	{
		public ComponentDataFromEntity<BotCredit> botCreditFromEntity;
		public float tickTime;
		public void Execute([ReadOnly] DynamicBuffer<NCOElement> ncoContainer, [ReadOnly] ref BuildingInfo buildingInfo)
		{
			float taxGive = buildingInfo.GetTax(tickTime);
			// Proportional tax give:
			float total = 0;
			for (int i = 0; i < ncoContainer.Length; i++)
			{
				total += ncoContainer[i].nco;
			}
			for (int i = 0; i < ncoContainer.Length; i++)
			{
				BotCredit bc = botCreditFromEntity[ncoContainer[i].owner.entity];
				bc.credit += taxGive * ncoContainer[i].nco / total;
				botCreditFromEntity[ncoContainer[i].owner.entity] = bc;
			}
		}
	}

	protected override JobHandle OnUpdate(JobHandle inputDeps)
	{
		// Doesn't have to run every tick in theory:
		JobHandle job = new TaxTick
		{
			botCreditFromEntity = GetComponentDataFromEntity<BotCredit>(),
			tickTime = Game.GetTickTime()
		}.ScheduleSingle(this, inputDeps);
		job = new BuildingTax
		{
			botCreditFromEntity = GetComponentDataFromEntity<BotCredit>(),
			tickTime = Game.GetTickTime()
		}.ScheduleSingle(this, job);
		return job;
	}
}*/

public struct BotEntity
{
	public Entity entity;
	public int id;

	public static BotEntity Invalid { get => new BotEntity { entity = Entity.Null, id = Bot.INVALID }; }
}

public struct BotCredit : IComponentData
{
	public float credit;
}
