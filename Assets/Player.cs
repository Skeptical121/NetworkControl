using System.Collections;
using System.Collections.Generic;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using Unity.Physics;
using Ray = Unity.Physics.Ray;
using RaycastHit = Unity.Physics.RaycastHit;
using Unity.Physics.Systems;

public class Player : MonoBehaviour
{
	CharacterController controller;
	Transform cameraTransform;
	Bot thisBot;
	Section nodeIn = null;
	// Building startBuilding = null;

	float upDownRot = 0f;

	bool init = false;

	private void Start()
	{
		controller = GetComponent<CharacterController>();
		cameraTransform = transform.Find("Camera");
	}

	private void Update()
	{
		if (!init)
		{
			init = true;
			thisBot = Bot.bots[0];
			PlayerUI.botPlayerLookingAt = Bot.bots[0];
			controller.enabled = false;
			transform.position = Game.mapObject.sections[0].GetCenter();
			controller.enabled = true;
			// ((Building)Game.buildingHandler.[1]).AddBotControl(thisBot, 100f);
			// ((Building)Game.activeEntities[1]).SetBotNCOProduction(thisBot, 0.5f);
		}
		VisibilityCheck();

		Ray r = new Ray { Origin = cameraTransform.position, Displacement = (float3)cameraTransform.forward * 200 };

		PhysicsWorld world = World.Active.GetOrCreateSystem<BuildPhysicsWorld>().PhysicsWorld;
		if (world.CollisionWorld.CastRay(new RaycastInput { Start = r.Origin, End = r.Origin + r.Displacement, Filter = CollisionFilter.Default }, out RaycastHit hit))
		{
			// Debug.Log(hit.RigidBodyIndex);
			GetComponent<PlayerUI>().displayInfo = world.CollisionWorld.Bodies[hit.RigidBodyIndex].Entity;
			
			//if (World.Active.EntityManager.HasComponent<ESection>(entity))
			//{
				/*ConstructPusher builder = new ConstructPusher();
				PFNode node = section.GetClosestNodeWhereYis0(hitInfo.point, PFR.BeltNormal);
				PFNode other = node;
				other.tile.x += 1;
				builder.Init(node, other, 0);
				Entity entity = builder.ConstructIfValid(World.Active.EntityManager);*/
			/*}
			else if (World.Active.EntityManager.HasComponent<BuildingInfo>(entity))
			{

				// Debug.Log(World.Active.EntityManager.GetComponentData<BuildingInfo>(entity).ownerID);
			}*/
		}
		else
		{
			GetComponent<PlayerUI>().displayInfo = Entity.Null;
		}



		controller.Move(Input.GetAxisRaw("Horizontal") * transform.right * 5f * Time.deltaTime + Input.GetAxisRaw("Vertical") * transform.forward * 5f * Time.deltaTime);

		/*if (Input.GetMouseButtonDown(0) && Physics.Raycast(cameraTransform.position, cameraTransform.forward, out RaycastHit hitInfo, 50f, 1 << 0))
		{
			if (hitInfo.transform.name.StartsWith("Section "))
			{
				Section section = Game.mapObject.sections[int.Parse(hitInfo.transform.name.Split(' ')[1])];
				ConstructPusher builder = new ConstructPusher();
				PFNode node = section.GetClosestNodeWhereYis0(hitInfo.point, PFR.BeltNormal);
				PFNode other = node;
				other.tile.x += 1;
				builder.Init(node, other, 0);
				Entity entity = builder.ConstructIfValid(World.Active.EntityManager);
			}
		}*/
			/*if (startBuilding == null)
			{
				if (Physics.Raycast(cameraTransform.position, cameraTransform.forward, out RaycastHit hitInfo, 50f, 1 << 0))
				{
					GameRep gameRep;
					if (gameRep = hitInfo.transform.GetComponent<GameRep>())
					{
						if (gameRep.entity is Building building)
						{
							if (Input.GetMouseButtonDown(0) && building.GetOwnerID() == Bot.bots[0].id)
							{
								startBuilding = building;
							}
						}
						else if (gameRep.entity is CoalSpawn coalSpawn)
						{
							new BuildBuilding<MiningDrill>(!Input.GetMouseButtonDown(0)).Build(coalSpawn.tile);
						}
					}
				}
			}
			else if (Physics.Raycast(cameraTransform.position, cameraTransform.forward, out RaycastHit hitInfo, 50f, 1 << 0))
			{
				//if (hitInfo.transform.name.StartsWith("Section "))
				//{
					// Section section = Game.mapObject.sections[int.Parse(hitInfo.transform.name.Split(' ')[1])];
					// builder.Schedule((Building)Game.mapObject.entities[0], section.GetClosestTileWhereYis0(hitInfo.point));
				//}
				GameRep gameRep;
				if (gameRep = hitInfo.transform.GetComponent<GameRep>())
				{
					if (gameRep.entity is Building building)
					{*/
			/*PathFindScheduler builder = Input.GetMouseButtonDown(0) ? (PathFindScheduler)new ConnectConveyorBelts() : new PreviewBuilder(ConnectConveyorBelts.CreateConveyorBelt);
			builder.Schedule(PFR.BeltNormal, startBuilding, building);
			if (Input.GetMouseButtonDown(0))
			{
				startBuilding = null;
			}*/
			//}
			/*else if (gameRep.entity is CoalSpawn coalSpawn)
			{
				HashSet<PFTile> tilesTaken = EntireBorderInfo.GetTilesIfValid(coalSpawn.tile, 2, 4);
				if (tilesTaken != null)
				{
					MiningDrill md = new MiningDrill();
					IBuilder builder = Input.GetMouseButtonDown(0) ? (IBuilder)new ConnectConveyorBeltToNewBuilding(md, tilesTaken) : new PreviewBuilder(ConnectConveyorBelts.CreateConveyorBelt);
					builder.Schedule(PFR.BeltNormal, startBuilding, 
						Building.GetPathFindingBorder(tilesTaken, (tile) => tilesTaken.Contains(tile), PFR.BeltNormal, false), 
						Game.map.WorldPosition(coalSpawn.tile));
				}
			}*/
			//}
			//}

		if (Cursor.lockState == CursorLockMode.Locked)
		{
			upDownRot = Mathf.Clamp(upDownRot - 5f * Input.GetAxis("Mouse Y"), -87f, 87f);
			cameraTransform.localEulerAngles = new Vector3(upDownRot, 0, 0);
			transform.eulerAngles = new Vector3(transform.eulerAngles.x, transform.eulerAngles.y + 5f * Input.GetAxis("Mouse X"), transform.eulerAngles.z);
		}

		if (Input.GetMouseButtonDown(1) || Input.GetKeyDown(KeyCode.Escape))
		{
			if (Cursor.lockState == CursorLockMode.None)
			{
				Cursor.lockState = CursorLockMode.Locked;
			}
			else
			{
				Cursor.lockState = CursorLockMode.None;
			}
		}
	}

	// Visaul check is run every frame
	private void VisibilityCheck()
	{
		// Since 99.9%+ of the time you remain in the same node, check that first:
		if (nodeIn != null && nodeIn.InBounds(cameraTransform.position))
			return;

		// TODO_EFFICIENCY find a better way than a for loop to iterate through the nodes to find the one that the camera is in..
		// Although maybe it's fine
		foreach (Section node in Game.mapObject.sections)
		{
			if (node != nodeIn && node.InBounds(cameraTransform.position))
			{
				// Debug.Log("Section in = " + Game.mapObject.sections.IndexOf(node));
				VisibilityUpdate(node);
				return;
			}
		}
	}
	
	// Ideally this would happen over multiple frames and use some sort of predictive meausures (essentially which nodes are adjacent the one you are in) to add to which nodes are visible
	private void VisibilityUpdate(Section newNode)
	{
		/*HashSet<Section> nodesSetToVisible = new HashSet<Section>(newNode.visibleNodesFromThisNode);
		if (nodeIn != null)
		{
			nodesSetToVisible.ExceptWith(nodeIn.visibleNodesFromThisNode);
			HashSet<Section> nodesSetToNotVisible = new HashSet<Section>(nodeIn.visibleNodesFromThisNode);
			nodesSetToNotVisible.ExceptWith(newNode.visibleNodesFromThisNode);
			foreach (Section node in nodesSetToNotVisible)
			{
				node.SetVisible(false);
			}
		}
		foreach (Section node in nodesSetToVisible)
		{
			node.SetVisible(true);
		}
		nodeIn = newNode;*/
	}
}
