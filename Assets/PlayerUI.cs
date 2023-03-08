using System.Collections;
using System.Collections.Generic;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.UI;

public class PlayerUI : MonoBehaviour
{
	public Entity displayInfo = Entity.Null;
	private GameObject buildingInfo;
	private GameObject playerInfo;
	public static Bot botPlayerLookingAt = null;

	// Start is called before the first frame update
	void Start()
    {
		Transform canvas = GameObject.Find("Canvas").transform;
		buildingInfo = canvas.Find("BuildingInfo").gameObject;
		buildingInfo.SetActive(false);
		playerInfo = canvas.Find("PlayerInfo").gameObject;
	}

    // Update is called once per frame
    void Update()
    {
        
    }

	private void OnGUI()
	{
		if (World.Active.EntityManager.HasComponent<BuildingInfo>(displayInfo))
		{
			/*Vector3 display = Camera.main.WorldToScreenPoint(World.Active.EntityManager.GetComponentData<CenterTile>(displayInfo).pos + new float3(0, 5, 0));

			float width = 100;
			float height = 100;
			float startX = display.x - width * 0.5f;
			float startY = Screen.height - display.y - height;

			GUI.Box(new Rect(startX, startY, height, 60), "");
			GUI.Label(new Rect());*/
			BuildingInfo bI = World.Active.EntityManager.GetComponentData<BuildingInfo>(displayInfo);
			DynamicBuffer<NCOElement> ncoContainer = World.Active.EntityManager.GetBuffer<NCOElement>(displayInfo);

			string text = "Building: " + displayInfo + "\n" +
					"Owner: " + bI.owner.id + "\n";

			for (int i = 0; i < ncoContainer.Length; i++)
			{
				text += "NCO of " + ncoContainer[i].owner.id + ": " + ncoContainer[i].nco + "\n";
			}

			buildingInfo.SetActive(true);
			buildingInfo.transform.Find("Text").GetComponent<Text>().text = text;
		}
		else
		{
			buildingInfo.SetActive(false);
		}

		if (botPlayerLookingAt != null)
		{
			playerInfo.transform.Find("Text").GetComponent<Text>().text = "Credit: " + botPlayerLookingAt.Credit;
		}
	}
}
