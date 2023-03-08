using System;
using System.Reflection;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class RenderInfo : MonoBehaviour
{
	public static RenderInfo self = null;

	// Name the materials the same thing as the type, with postfix "Mat"
	public static Material[] BeltObject;
	public static Material[] Building;
	public static Material[] Facade;

	public static Mesh tileObject;
	public Mesh beltObject;
	public Material conveyorBeltMat;

	public void Init()
	{
		self = this;
		FieldInfo[] fields = typeof(RenderInfo).GetFields(BindingFlags.Static | BindingFlags.Public);
		foreach (FieldInfo field in fields)
		{
			if (field.FieldType.Equals(typeof(Material[])))
			{
				string str = field.Name;
				Material mat = (Material)Resources.Load(str + "Mat");
				Material[] all = new Material[Bot.MAX_BOTS + 1];
				all[Bot.MAX_BOTS] = mat;
				for (int i = 0; i < Bot.MAX_BOTS; i++)
				{
					Material m = new Material(mat);
					Color color = Bot.GetControlColor(i);
					color.a = m.color.a;
					m.color = color;
					all[i] = m;
				}
				field.SetValue(null, all);
			}
		}



		// CUSTOM MESHES:
		tileObject = new Mesh
		{
			vertices = new Vector3[] { new Vector3(-0.5f, 0, -0.5f), new Vector3(-0.5f, 0, 0.5f), new Vector3(0.5f, 0, -0.5f), new Vector3(0.5f, 0, 0.5f) },
			triangles = MeshCreator.RectTri(0, 1, 2, 3),
			normals = new Vector3[] { Vector3.up, Vector3.up, Vector3.up, Vector3.up },
			uv = new Vector2[] { new Vector2(0, 0), new Vector2(0, 1), new Vector2(1, 1), new Vector2(1, 0) }
		};
	}
}
