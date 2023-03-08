using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class DebugDraw : MonoBehaviour
{
	public static string[] toDisplay = new string[500];
	public static List<float> tickTime = new List<float>();

	private GUIStyle data = null;

	// Start is called before the first frame update
	void Start()
    {
    }

    // Update is called once per frame
    void OnGUI()
    {
		if (data == null)
		{
			data = new GUIStyle(GUI.skin.box);
			data.normal.background = new Texture2D(1, 1);
		}

		for (int i = 0; i < toDisplay.Length; i++)
		{
			if (toDisplay[i] != null)
			{
				GUI.Label(new Rect(20, i * 20, 300, 20), toDisplay[i]);
			}
		}

		float width = 5f;
		GUI.backgroundColor = new Color(255f, 255f, 0f, 0.5f);
		GUI.Box(new Rect(600, 0, width * tickTime.Count, 0.01f * 5000f), "", data);
		for (int x = 0; x < tickTime.Count; x++)
		{
			GUI.backgroundColor = new Color(0f, 255f, 0f, 0.5f);
			GUI.Box(new Rect(600 + x * width, 0, width, tickTime[x] * 5000f), "", data);
		}
	}

	private static int index = 0;

	public static void ResetIndex()
	{
		index = 0;
	}

	public static void DisplayMessage(string text)
	{
		if (index < toDisplay.Length)
		{
			toDisplay[index] = text;
			index++;
		}
		else
		{
			Debug.LogError("Index for diplay message too high (" + index + ")");
		}
	}
}
