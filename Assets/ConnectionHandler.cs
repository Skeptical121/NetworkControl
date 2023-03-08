using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ConnectionHandler
{


	// So, this is here to solve the problem of how do I create a path (or something else) to connect 2 different places together.

	public static CurveSection ConnectWithPath(byte maxPathWidth, byte minPathWidth, byte pathHeight, Section a, Section b)
	{

		for (byte pathWidth = maxPathWidth; pathWidth >= minPathWidth; pathWidth--)
		{
			Side closestForA = GetClosestSide(a, b, pathWidth);
			Side closestForB = GetClosestSide(b, a, pathWidth);
			/*foreach (Side closestForA in a.unlinked)
			{
				foreach (Side closestForB in b.unlinked)
				{
					if (Vector3.Angle(closestForA.normal, -closestForB.normal) < 90f)
					{*/
			//if (closestForA.numTiles >= pathWidth && closestForB.numTiles >= pathWidth)

			// Vector3.Angle normalizes the vectors first
			if (closestForA != null && closestForB != null &&
				Vector3.Angle(closestForA.sideInfo.normal, closestForB.GetCenter() - closestForA.GetCenter()) <= 91f && 
				Vector3.Angle(closestForB.sideInfo.normal, closestForA.GetCenter() - closestForB.GetCenter()) <= 91f &&
				Vector3.Angle(closestForA.sideInfo.normal, -closestForB.sideInfo.normal) <= 91f)
			{
				for (byte offsetA = 0; offsetA <= closestForA.numTiles - pathWidth; offsetA++)
				{
					for (byte offsetB = 0; offsetB <= closestForB.numTiles - pathWidth; offsetB++)
					{
						CurveSection path = Game.mapObject.AttemptConnect(pathWidth, pathHeight, closestForA, offsetA, closestForB, offsetB);
						if (path != null)
						{
							Section.LinkSides(b, closestForB, offsetB, path, path.unlinked[1], 0, pathWidth);
							Section.LinkSides(a, closestForA, offsetA, path, path.unlinked[0], 0, pathWidth);
							return path;
						}
					}
				}
			}
					//}
				//}
			//}
		}

		
		return null;
	}

	// Or it could be closest in terms of rotation
	private static Side GetClosestSide(Section a, Section b, int minTiles)
	{
		float closestDist = float.PositiveInfinity;
		Side closestSide = null;
		foreach (Side side in a.unlinked)
		{
			if (side.numTiles >= minTiles)
			{
				float dist = Vector3.Distance(side.GetCenter(), b.GetCenter());
				if (dist < closestDist)
				{
					closestDist = dist;
					closestSide = side;
				}
			}
		}
		return closestSide;
	}
}
