using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

[InternalBufferCapacity(0)]
public struct BezierCurveNetDistance : IBufferElementData
{
	public float dist;
	public PosRot posRot;
}

public struct PosRot
{
	public float3 pos;
	public quaternion rot;

	public float3 Dir { get => math.mul(rot, new float3(0, 0, 1)); }
	public float3 Tan { get => math.mul(rot, new float3(1, 0, 0)); }
}

public static class BezierCurve
{
	// So... get tangent is assuming the normal is up...
	// Which often is true but hmm

	public static PosRot GetMapped(this NativeArray<BezierCurveNetDistance> netDistance, float target)
	{
		int low = 0;
		int high = netDistance.Length;
		int index = 0;
		while (low < high)
		{
			index = low + ((high - low) / 2);
			if (netDistance[index].dist < target)
				low = index + 1;
			else
				high = index;
		}
		if (netDistance[index].dist > target)
			index--;

		// It's just going to extrapolate anyways:
		if (index == netDistance.Length - 1)
			index--;

		// If index is -1, something got pushed back too far... hmm
		if (index == -1)
		{
			index = 0;
		}
		float t = (target - netDistance[index].dist) / (netDistance[index + 1].dist - netDistance[index].dist); // Extrapolate
		return new PosRot
		{
			pos = math.lerp(netDistance[index].posRot.pos, netDistance[index + 1].posRot.pos, t),
			rot = math.slerp(netDistance[index].posRot.rot, netDistance[index + 1].posRot.rot, t)
		};
	}

	public static void GetRep(float3[] points, int distancePrecision, out NativeArray<BezierCurveNetDistance> netDistance)
	{
		netDistance = new NativeArray<BezierCurveNetDistance>(distancePrecision + 1, Allocator.Temp);
		netDistance[0] = new BezierCurveNetDistance { dist = 0, posRot = new PosRot { pos = GetPos(points, 0), rot = GetRot(points, 0) } };
		for (int i = 0; i < distancePrecision; i++)
		{
			float3 thisPos = GetPos(points, (i + 1) / (float)distancePrecision);
			netDistance[i + 1] = new BezierCurveNetDistance
			{
				dist = netDistance[i].dist + math.distance(netDistance[i].posRot.pos, thisPos),
				posRot = new PosRot { pos = thisPos, rot = GetRot(points, (i + 1) / (float)distancePrecision) }
			};
		}
	}

	private static float3 GetPos(float3[] p, float t)
	{
		if (p.Length == 2)
			return (1 - t) * p[0] + t * p[1];
		else if (p.Length == 3)
			return (1 - t) * (1 - t) * p[0] + (1 - t) * t * p[1] * 2 + t * t * p[2];
		else
			return (1 - t) * (1 - t) * (1 - t) * p[0] + 3 * (1 - t) * (1 - t) * t * p[1] + 3 * (1 - t) * t * t * p[2] + t * t * t * p[3];
	}

	private static quaternion GetRot(float3[] p, float t)
	{
		if (p.Length == 2)
			return quaternion.LookRotationSafe(math.normalize(p[1] - p[0]), new float3(0, 1, 0));
		else if (p.Length == 3)
			return quaternion.LookRotationSafe(math.normalize(2 * (1 - t) * (p[1] - p[0]) + 2 * t * (p[2] - p[1])), new float3(0, 1, 0));
		else
			return quaternion.LookRotationSafe(math.normalize(3 * (1 - t) * (1 - t) * (p[1] - p[0]) + 6 * (1 - t) * t * (p[2] - p[1]) + 3 * t * t * (p[3] - p[2])), new float3(0, 1, 0));
	}

	// normalOffsets are needed when hardEdges == true
	public static Mesh CreateRenderMesh(NativeArray<BezierCurveNetDistance> posList, Vector2[] offsetList, float[] uvList, float3 curveTangent, bool loop, bool hardEdges, Vector2[] normalOffsets = null, float netDistanceOffset = 0)
	{
		int numOffsetVertices = offsetList.Length;
		if (hardEdges)
		{
			numOffsetVertices = loop ? offsetList.Length * 2 : (offsetList.Length - 2) * 2 + 2;
		}

		Vector3[] meshVertices = new Vector3[numOffsetVertices * posList.Length];
		Vector3[] meshNormals = new Vector3[numOffsetVertices * posList.Length];
		Vector2[] meshUV = new Vector2[numOffsetVertices * posList.Length];

		// List<Vector3> meshUV = new List<Vector3>(2 * (resolution + 1));
		int[] meshTriangles = new int[loop ? 6 * (posList.Length - 1) * offsetList.Length : 6 * (posList.Length - 1) * (offsetList.Length - 1)];
		for (int i = 0; i < posList.Length; i++)
		{
			float3 tangent = math.normalize(math.cross(math.mul(posList[i].posRot.rot, new float3(0, 0, 1)), curveTangent));

			for (int r = 0; r < numOffsetVertices; r++)
			{
				int rIndex = hardEdges ? (r + 1) / 2 : r;
				float3 offset = offsetList[rIndex].x * tangent + offsetList[rIndex].y * curveTangent;
				meshVertices[i * numOffsetVertices + r] = posList[i].posRot.pos + offset;
				if (hardEdges)
				{
					offset = normalOffsets[r / 2].x * tangent + normalOffsets[r / 2].y * curveTangent;
				}
				meshNormals[i * numOffsetVertices + r] = Vector3.Normalize(offset);
				meshUV[i * numOffsetVertices + r] = new Vector2(uvList[rIndex], netDistanceOffset + posList[i].dist);

				if ((!hardEdges || r % 2 == 0) && i < posList.Length - 1 && (loop || r < numOffsetVertices - 1))
				{
					int triangleMult = hardEdges ? numOffsetVertices / 2 : (loop ? numOffsetVertices : numOffsetVertices - 1);
					int rPlusOne = (r + 1) % numOffsetVertices;

					int rMult = hardEdges ? 3 : 6;
					meshTriangles[i * 6 * triangleMult + r * rMult + 0] = i * numOffsetVertices + r;
					meshTriangles[i * 6 * triangleMult + r * rMult + 1] = i * numOffsetVertices + rPlusOne;
					meshTriangles[i * 6 * triangleMult + r * rMult + 2] = (i + 1) * numOffsetVertices + r;
					meshTriangles[i * 6 * triangleMult + r * rMult + 3] = (i + 1) * numOffsetVertices + r;
					meshTriangles[i * 6 * triangleMult + r * rMult + 4] = i * numOffsetVertices + rPlusOne;
					meshTriangles[i * 6 * triangleMult + r * rMult + 5] = (i + 1) * numOffsetVertices + rPlusOne;
				}
			}
		}


		Mesh renderMesh = new Mesh
		{
			vertices = meshVertices,
			triangles = meshTriangles,
			normals = meshNormals,
			uv = meshUV
		};
		return renderMesh;
	}

	public static Mesh CreateCylinderRenderMesh(NativeArray<BezierCurveNetDistance> posList, float radius, int renderVertices)
	{
		float3 l1 = math.mul(posList[0].posRot.rot, new float3(0, 0, 1));
		float3 l2 = math.mul(posList[posList.Length - 1].posRot.rot, new float3(0, 0, 1));
		float3 planeNormal = math.cross(l1, l2);
		if (math.lengthsq(planeNormal) <= 0.0001f)
		{
			// resolution = 1; // I guess?
			if (Vector3.Cross(l1, Vector3.up).sqrMagnitude <= 0.0001f)
				planeNormal = Vector3.right;
			else
				planeNormal = Vector3.up;
		}
		else
		{
			planeNormal = Vector3.Normalize(planeNormal);
		}
		Vector2[] offsets = new Vector2[renderVertices];
		float[] uv = new float[renderVertices];
		for (int r = 0; r < renderVertices; r++)
		{
			float angle = 360 * (r + 0.5f) / renderVertices * Mathf.Deg2Rad;
			offsets[r] = new Vector2(Mathf.Sin(angle), Mathf.Cos(angle)) * radius; // Starts going up
			uv[r] = r / (float)renderVertices;
		}
		return CreateRenderMesh(posList, offsets, uv, planeNormal, true, false);
	}
}
