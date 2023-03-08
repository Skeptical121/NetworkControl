using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MeshCreator
{

	public static int[] RectTri(int a, int b, int c, int d)
	{
		return new int[] { a, b, c, c, b, d };
	}

	public static int[] MergeTriangles(params int[][] triangles)
	{
		int total = 0;
		for (int i = 0; i < triangles.Length; i++)
			total += triangles[i].Length;

		int[] returnTriangles = new int[total];
		total = 0;
		for (int i = 0; i < triangles.Length; i++)
		{
			for (int k = 0; k < triangles[i].Length; k++)
			{
				returnTriangles[total++] = triangles[i][k];
			}
		}
		return returnTriangles;
	}

	public static Mesh CombineMeshes(bool mergeSubMeshes, params Mesh[] meshes)
	{
		CombineInstance[] combine = new CombineInstance[meshes.Length];

		for (int i = 0; i < meshes.Length; i++)
		{
			combine[i].mesh = meshes[i];
			combine[i].transform = Matrix4x4.identity;
		}

		Mesh finalMesh = new Mesh();
		finalMesh.CombineMeshes(combine, mergeSubMeshes);
		return finalMesh;
	}
}
