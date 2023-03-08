using System.Collections;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;

public static class VectorMath
{
	// lineVec1 and lineVec2 should be normalized unless you know what you're doing...
	public static float3 GetIntersection(float3 p1, float3 p2, float3 lineVec1, float3 lineVec2)
	{
		float3 lineVec3 = p2 - p1;
		float3 crossVec1and2 = math.cross(lineVec1, lineVec2);
		float3 crossVec3and2 = math.cross(lineVec3, lineVec2);

		// float planarFactor = Vector3.Dot(lineVec3, crossVec1and2); <- this will always be 0

		if (/*Mathf.Abs(planarFactor) < 0.0001f && */math.lengthsq(crossVec1and2) > 0.0001f)
		{
			float s = math.dot(crossVec3and2, crossVec1and2) / math.lengthsq(crossVec1and2); // lengthsq is SqrMagnitude
			return p1 + lineVec1 * s;
		}
		else
		{
			Assert.Fail("Invalid: " + p1 + ", " + p2 + ", " + lineVec1 + ", " + lineVec2 + "... make / use a different method if we want this to be valid behaviour");
			return (p1 + p2) / 2;
		}
	}

	// return value is from 0 - 2, 0 being straight, 2 being directly backwards
	public static float GetAngleMultiplier(float3 dir1, float3 dir2)
	{
		return 1 - math.dot(dir1, dir2);
	}
}
