using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public static class Assert
{
	public static void IsEqual<T>(T a, T b, string message = "")
	{
		IsTrue(a.Equals(b), "[" + a + " != " + b + "] " + message);
	}

	public static void IsTrue(bool condition, string message = "")
	{
		if (!condition)
		{
			Debug.LogError(message);
			throw new System.Exception(message);
		}
	}

	public static void Fail(string message)
	{
		Debug.LogError("Failure: " + message);
		throw new System.Exception(message);
	}

	public static void IsNull(object obj, string message = "")
	{
		IsTrue(obj == null, message);
	}

	public static void IsNotNull(object obj, string message = "")
	{
		IsTrue(obj != null, message);
	}
}
