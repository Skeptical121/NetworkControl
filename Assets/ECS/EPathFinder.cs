using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

public struct PathFindRequest
{
	public PFNode endNode;
	public int endEntity;
	public float3 endPos;
	public PathFindRequest(PFNode endNode, float3 endPos)
	{
		this.endNode = endNode;
		endEntity = -1;
		this.endPos = endPos;
	}
	public PathFindRequest(int endEntity, float3 endPos)
	{
		endNode = new PFNode(new PFTile(0, 0, 0, -1), 0, 0);
		this.endEntity = endEntity;
		this.endPos = endPos;
	}
}
/*
public struct SetEntityAtTile
{
	public int tileIndex;
	public Entity entity;
	public SetEntityAtTile(int tileIndex, Entity entity)
	{
		this.tileIndex = tileIndex;
		this.entity = entity;
	}
}

public class MapUpdater
{
	public static long entitiesStartIndex = 0; // Every once in awhile, we need to shift the lists over...
	public static List<SetEntityAtTile> entitiesToSet = new List<SetEntityAtTile>();
	public static long lastEntitySetIndex = 0;

	public static void SetEntity(PFTile tile, Entity entity)
	{
		SetEntityAtTile tileEntity = new SetEntityAtTile(Game.map.Index(tile), entity);
		Game.map.SetEntity(tileEntity);
		entitiesToSet.Add(tileEntity);
		lastEntitySetIndex++;
	}
}*/

public struct Request
{
	public List<PFNode> startNodes;
	public List<PFNode> endNodes;
	public float3 endPos;
	public PathFindScheduler requestBuilder;
	public BotEntity builder;
}

[UpdateInGroup(typeof(MainSimSystemGroup))]
public class PathFinderSystem : JobComponentSystemWithCallback
{
	struct PathFindJobBox
	{
		public PathFindJob pathFindJob;
		public JobHandle jobHandle;
		public PathFindScheduler inUse;
	}

	private List<PathFindJob> jobs = new List<PathFindJob>();
	private List<PathFindJobBox> pathFindJobs = new List<PathFindJobBox>();

	// const int PREVIEW_JOB = 0;
	// private static PathFindJob[] jobCollection;
	// private static List<JobHandle> pathFindJobsRunning;
	// private static PathFindScheduler[] inUse;
	// private static long[] entitiesSet;
	// private static long[] startedPathFindingAt;
	// Also save a list of updates that need to be made to the map

	// Requests are queued until the FixedUpdate is completely done (since the map still might change up until then)
	private static Queue<Request> queuedRequests = new Queue<Request>();
	const int iterationLimit = 100000;
	const int maxPathFindJobsPerTick = 10;

	public static void AddPathFindJob(Request request)
	{
		queuedRequests.Enqueue(request);
	}

	protected override void OnCreate()
	{
		for (int i = 0; i < maxPathFindJobsPerTick; i++)
		{
			// int numPFNodes = Game.map.sectionStartIndex[Game.map.sectionStartIndex.Length - 1] * MapInfo.MAX_DIRS * (byte)PFR.MAX_CONNECTION_TYPES;
			jobs.Add(new PathFindJob
			{
				map = Game.map, // Copy map.. but not entirely
				path = new NativeList<PFNode>(Allocator.Persistent),
				gScore = new NativeHashMap<int, float>(10000, Allocator.Persistent),
				cameFrom = new NativeHashMap<int, PFNode>(10000, Allocator.Persistent),
				openSet = new Heap(100000),
				startNodes = new NativeList<PFNode>(Allocator.Persistent),
				endNodes = new NativeList<PFNode>(Allocator.Persistent),
				nextPFR = new NativeList<byte>(Allocator.Persistent),
				tilesTaken = new NativeList<PFTile>(Allocator.Persistent),
				outIterationLimit = new NativeArray<int>(1, Allocator.Persistent)
			});
		}
		base.OnCreate();
	}

	protected override JobHandle OnUpdate(JobHandle inputDeps)
	{
		NativeList<JobHandle> jobHandles = new NativeList<JobHandle>(Allocator.Temp);
		// So, here's how it goes... we have a certain number of requests, and we're going to run them all at once.. to a point
		for (int i = 0; i < maxPathFindJobsPerTick && queuedRequests.Count > 0; i++)
		{
			Request request = queuedRequests.Dequeue();
			jobs[i].startNodes.Clear();
			for (int n = 0; n < request.startNodes.Count; n++)
			{
				jobs[i].startNodes.Add(request.startNodes[n]);
			}
			jobs[i].endNodes.Clear();
			for (int n = 0; n < request.endNodes.Count; n++)
			{
				jobs[i].endNodes.Add(request.endNodes[n]);
			}

			PathFindJob pathFindJob = new PathFindJob
			{
				iterationLimit = iterationLimit,
				path = jobs[i].path,
				map = Game.map,
				gScore = jobs[i].gScore,
				cameFrom = jobs[i].cameFrom,
				openSet = jobs[i].openSet,
				nextPFR = jobs[i].nextPFR,
				tilesTaken = jobs[i].tilesTaken,
				startNodes = jobs[i].startNodes,
				endNodes = jobs[i].endNodes,
				endPos = request.endPos,
				builder = request.builder,
				outIterationLimit = jobs[i].outIterationLimit
			};
			JobHandle jobHandle = pathFindJob.Schedule(inputDeps);
			pathFindJobs.Add(new PathFindJobBox { pathFindJob = pathFindJob, inUse = request.requestBuilder, jobHandle = jobHandle });
			jobHandles.Add(jobHandle);
		}

		return base.OnUpdate(JobHandle.CombineDependencies(jobHandles));
	}

	public override void MainThreadSimulationCallbackTick()
	{
		// Hmm
		// Game.map.UpdateBotBorders();
		for (int i = 0; i < pathFindJobs.Count; i++)
		{
			pathFindJobs[i].jobHandle.Complete();

			// Reverse path here, I guess:
			List<PFNode> path = new List<PFNode>();
			for (int p = pathFindJobs[i].pathFindJob.path.Length - 1; p >= 0; p--)
			{
				path.Add(pathFindJobs[i].pathFindJob.path[p]);
			}

			pathFindJobs[i].inUse.PathFindingCompletion(path);
			// Debug.Log("Iterations = " + (iterationLimit - jobCollection[i].outIterationLimit[0]) + " in time " + (Game.NanoTime() - startedPathFindingAt[i]) / 1000000f + "ms for path of length " + path.Count);
		}
		pathFindJobs.Clear();
	}

	protected override void OnDestroy()
	{
		for (int i = 0; i < maxPathFindJobsPerTick; i++)
		{
			jobs[i].path.Dispose();
			jobs[i].gScore.Dispose();
			jobs[i].cameFrom.Dispose();
			jobs[i].openSet.items.Dispose();
			jobs[i].startNodes.Dispose();
			jobs[i].endNodes.Dispose();
			jobs[i].nextPFR.Dispose();
			jobs[i].tilesTaken.Dispose();
			jobs[i].outIterationLimit.Dispose();
		}
	}

	[BurstCompile]
	private struct PathFindJob : IJob
	{
		const float START_COST = -0.000000001f; // This is to distinguish the starting nodes from the undefined nodes...
		const float END_COST = -10f;

		public MapInfo map;

		// These are indexed by PathfindingNode
		public NativeHashMap<int, PFNode> cameFrom;
		public NativeHashMap<int, float> gScore;

		public Heap openSet;

		public int iterationLimit; // For determinism / if the path can't be found
		public NativeArray<int> outIterationLimit; // Testing only!

		// Pathfinding request:
		public NativeList<PFNode> startNodes;
		public NativeList<PFNode> endNodes;
		public float3 endPos;
		public BotEntity builder;

		// Return path:
		public NativeList<PFNode> path;

		// Temp stuff
		public NativeList<byte> nextPFR;
		public NativeList<PFTile> tilesTaken;

		public void Execute()
		{
			cameFrom.Clear();
			gScore.Clear();
			openSet.Clear();
			path.Clear();
			PathFind();
		}

		private void PathFind()
		{
			for (int i = 0; i < startNodes.Length; i++)
			{
				openSet.Add(new HeapItem(startNodes[i], H(startNodes[i])));
				gScore[map.Index(startNodes[i])] = START_COST;
			}

			for (int i = 0; i < endNodes.Length; i++)
			{
				int index = map.Index(endNodes[i]);
				if (gScore.ContainsKey(index))
				{
					gScore[map.Index(endNodes[i])] = END_COST;
					ReconstructFinishedPath(endNodes[i], true);
					return;
				}
				gScore[map.Index(endNodes[i])] = END_COST;
			}

			while (iterationLimit > 0 && openSet.currentItemCount > 0)
			{
				PFNode pfn = openSet.RemoveFirst();
				int pfnIndex = map.Index(pfn);
				float initialCost = gScore[pfnIndex];
				if (initialCost <= END_COST)
				{
					ReconstructFinishedPath(pfn, false);
					return;
				}

				PFTile next = map.GetToTile(pfn);
				if (next.IsValid()) // Can't be pointing to invalid tile
				{

					((PFR)pfn.pfr).SetNextPFR(nextPFR);
					for (int i = 0; i < nextPFR.Length; i++)
					{
						for (byte dir = 0; dir < ((PFR)nextPFR[i]).MaxDir(); dir++)
						{
							// We've gotta check to tile here... could cull for other things / save the value somehow?
							if (map.GetToTile(next, dir).Equals(pfn.tile))
								continue;

							PFNode node = map.GetNextPFNode(pfn.pfr, next, dir, nextPFR[i], tilesTaken);
							if (!node.tile.IsValid())
								continue;

							bool isValid = true;
							for (int t = 0; t < tilesTaken.Length; t++)
							{
								if (!tilesTaken[t].IsValid() || !map.IsEmptyAndBuildable(tilesTaken[t], builder.id))
								{
									isValid = false;
									break;
								}
							}
							if (!isValid)
								continue;

							float cost = GetCost(pfn, node);

							if (float.IsInfinity(cost))
								continue;

							int nodeIndex = map.Index(node);
							float newCost = initialCost + cost;
							float oldCost = 0;
							if (gScore.TryGetValue(nodeIndex, out float item))
								oldCost = item;

							if (oldCost == 0 || newCost < oldCost || oldCost == END_COST || newCost < END_COST - oldCost) // Is it undefined or is the cost lower? Then add it:
							{
								// Duplicates can exist in this model.. but that is fine?
								if (oldCost <= END_COST)
									gScore[nodeIndex] = END_COST - newCost;
								else
									gScore[nodeIndex] = newCost;
								cameFrom[nodeIndex] = pfn;

								float expectedCost = newCost + H(node);
								openSet.Add(new HeapItem(node, expectedCost));
							}
						}
					}
				}
				iterationLimit--; // Count invalid tiles
			}
			outIterationLimit[0] = iterationLimit; // Path not found
		}

		private float GetCost(PFNode from, PFNode to)
		{
			float extra = VectorMath.GetAngleMultiplier(map.ConnectionNormal(from), map.ConnectionNormal(to)); // num is from 0 - 2, 0 being straight, 2 being directly backwards
			if (from.pfr == (byte)PFR.BeltNormal || from.pfr == (byte)PFR.BeltUp || from.pfr == (byte)PFR.BeltDown)
			{
				if (extra > 0.5f && (from.pfr == (byte)PFR.BeltUp || from.pfr == (byte)PFR.BeltDown ||
					to.pfr == (byte)PFR.BeltUp || to.pfr == (byte)PFR.BeltDown))
				{ // Note that connectionDir uses the modified version...
					return float.PositiveInfinity;
				}
				return (1 + extra) * MapInfo.TILE_LENGTH + to.tile.y * MapInfo.TILE_HEIGHT;
			}
			else
			{
				return MapInfo.TILE_LENGTH;
			}
		}

		private float H(PFNode from)
		{
			return math.distance(map.ConnectionPoint(from), endPos); // - MapInfo.TILE_LENGTH * 5;
		}

		private void ReconstructFinishedPath(PFNode current, bool oneLong)
		{
			path.Add(current);
			int currentIndex = map.Index(current);
			gScore[currentIndex] = END_COST - gScore[currentIndex] - START_COST; // Cost was negative to signify it was the end node
			if (!oneLong)
			{
				while (gScore[map.Index(current)] > 0 && path.Length < 2000) // If somehow we reach an undefined node, end this while loop as well
				{ // The negative gScore is the starting location
					current = cameFrom[map.Index(current)];
					path.Add(current);
				}
			}
			outIterationLimit[0] = iterationLimit;
			// Done
		}
	}
}

public struct HeapItem
{
	// public int HeapIndex;
	public PFNode pfn;
	public float fScore;
	public HeapItem(PFNode pfn, float fScore)
	{
		// this.HeapIndex = 0;
		this.pfn = pfn;
		this.fScore = fScore;
	}
}

public struct Heap
{
	public NativeArray<HeapItem> items;
	public int currentItemCount;

	public Heap(int maxHeapSize)
	{
		items = new NativeArray<HeapItem>(maxHeapSize, Allocator.Persistent);
		currentItemCount = 0;
	}

	public void Clear()
	{
		currentItemCount = 0;
	}

	public void Add(HeapItem item)
	{
		// item.HeapIndex = currentItemCount;
		items[currentItemCount] = item;
		SortUp(item, currentItemCount);
		currentItemCount++;
	}

	public PFNode RemoveFirst()
	{
		PFNode pfn = items[0].pfn;
		currentItemCount--;
		HeapItem lastItem = items[currentItemCount];
		// lastItem.HeapIndex = 0;
		items[0] = lastItem;
		SortDown(items[0], 0);
		return pfn;
	}

	void SortDown(HeapItem item, int itemHeapIndex)
	{
		while (true)
		{
			int childIndexLeft = itemHeapIndex * 2 + 1;
			int childIndexRight = itemHeapIndex * 2 + 2;

			if (childIndexLeft < currentItemCount)
			{
				int swapIndex = childIndexLeft;

				if (childIndexRight < currentItemCount)
				{
					if (items[childIndexLeft].fScore > items[childIndexRight].fScore)
					{
						swapIndex = childIndexRight;
					}
				}

				if (item.fScore > items[swapIndex].fScore)
				{
					Swap(item, itemHeapIndex, items[swapIndex], swapIndex);
					// swapIndex is not used after this...
					itemHeapIndex = swapIndex;
				}
				else
				{
					return;
				}

			}
			else
			{
				return;
			}

		}
	}

	void SortUp(HeapItem item, int itemHeapIndex)
	{
		int parentIndex = (itemHeapIndex - 1) / 2;

		while (true)
		{
			HeapItem parentItem = items[parentIndex];
			if (item.fScore < parentItem.fScore)
			{
				Swap(item, itemHeapIndex, parentItem, parentIndex);
				int save = parentIndex;
				parentIndex = itemHeapIndex;
				itemHeapIndex = save;
			}
			else
			{
				break;
			}

			parentIndex = (itemHeapIndex - 1) / 2;
		}
	}

	void Swap(HeapItem itemA, int itemAHeapIndex, HeapItem itemB, int itemBHeapIndex)
	{
		items[itemAHeapIndex] = itemB;
		items[itemBHeapIndex] = itemA;
	}
}


/*[NativeContainerSupportsDeallocateOnJobCompletion]
[NativeContainerSupportsMinMaxWriteRestriction]
[NativeContainer]
public unsafe struct NativeMinHeap : IDisposable
{
	[NativeDisableUnsafePtrRestriction] private void* m_Buffer;
	private int m_capacity;
#if ENABLE_UNITY_COLLECTIONS_CHECKS
	private AtomicSafetyHandle m_Safety;
	[NativeSetClassTypeToNullOnSchedule] private DisposeSentinel m_DisposeSentinel;
#endif
	private Allocator m_AllocatorLabel;

	private int m_head;
	private int m_length;
	private int m_MinIndex;
	private int m_MaxIndex;

	public NativeMinHeap(int capacity, Allocator allocator)
	{
		Allocate(capacity, allocator, out this);
	}

	private static void Allocate(int capacity, Allocator allocator, out NativeMinHeap nativeMinHeap)
	{
		long size = (long)UnsafeUtility.SizeOf<MinHeapNode>() * capacity;
		if (allocator <= Allocator.None)
			throw new ArgumentException("Allocator must be Temp, TempJob or Persistent", nameof(allocator));
		if (capacity < 0)
			throw new ArgumentOutOfRangeException(nameof(capacity), "Length must be >= 0");
		if (size > int.MaxValue)
			throw new ArgumentOutOfRangeException(nameof(capacity),
				$"Length * sizeof(T) cannot exceed {(object)int.MaxValue} bytes");

		nativeMinHeap.m_Buffer = UnsafeUtility.Malloc(size, UnsafeUtility.AlignOf<MinHeapNode>(), allocator);
		nativeMinHeap.m_capacity = capacity;
		nativeMinHeap.m_AllocatorLabel = allocator;
		nativeMinHeap.m_MinIndex = 0;
		nativeMinHeap.m_MaxIndex = capacity - 1;
		nativeMinHeap.m_head = -1;
		nativeMinHeap.m_length = 0;

#if ENABLE_UNITY_COLLECTIONS_CHECKS
#if UNITY_2018_3_OR_NEWER
		DisposeSentinel.Create(out nativeMinHeap.m_Safety, out nativeMinHeap.m_DisposeSentinel, 1, allocator);
#else
            DisposeSentinel.Create(out nativeMinHeap.m_Safety, out nativeMinHeap.m_DisposeSentinel, 1);
#endif
#endif


	}

	public bool HasNext()
	{
#if ENABLE_UNITY_COLLECTIONS_CHECKS
		AtomicSafetyHandle.CheckReadAndThrow(m_Safety);
#endif
		return m_head >= 0;
	}

	public void Push(MinHeapNode node)
	{
#if ENABLE_UNITY_COLLECTIONS_CHECKS
		if (m_length == m_capacity)
			throw new IndexOutOfRangeException($"Capacity Reached");
		AtomicSafetyHandle.CheckReadAndThrow(m_Safety);
#endif

		UnsafeUtility.WriteArrayElement(m_Buffer, m_length, node);
		m_length += 1;

		if (m_head < 0)
		{
			m_head = m_length - 1;
		}
		else if (node.ExpectedCost < this[m_head].ExpectedCost)
		{
			node.Next = m_head;
			m_head = m_length - 1;
		}
		else
		{
			int currentPtr = m_head;
			MinHeapNode current = this[currentPtr];

			while (current.Next >= 0 && this[current.Next].ExpectedCost <= node.ExpectedCost)
			{
				currentPtr = current.Next;
				current = this[current.Next];
			}

			node.Next = current.Next;
			current.Next = m_length - 1;
		}
	}

	public int Pop()
	{
#if ENABLE_UNITY_COLLECTIONS_CHECKS
		AtomicSafetyHandle.CheckWriteAndThrow(m_Safety);
#endif
		int result = m_head;
		m_head = this[m_head].Next;
		return result;
	}

	public MinHeapNode this[int index]
	{
		get
		{
#if ENABLE_UNITY_COLLECTIONS_CHECKS
			if (index < m_MinIndex || index > m_MaxIndex)
				FailOutOfRangeError(index);
			AtomicSafetyHandle.CheckReadAndThrow(m_Safety);
#endif

			return UnsafeUtility.ReadArrayElement<MinHeapNode>(m_Buffer, index);
		}
	}

	public void Clear()
	{
		m_head = -1;
		m_length = 0;
	}

	public void Dispose()
	{
		if (!UnsafeUtility.IsValidAllocator(m_AllocatorLabel))
			throw new InvalidOperationException("The NativeArray can not be Disposed because it was not allocated with a valid allocator.");
#if ENABLE_UNITY_COLLECTIONS_CHECKS
		DisposeSentinel.Dispose(ref m_Safety, ref m_DisposeSentinel);
#endif
		UnsafeUtility.Free(m_Buffer, m_AllocatorLabel);
		m_Buffer = null;
		m_capacity = 0;
	}

#if ENABLE_UNITY_COLLECTIONS_CHECKS
	private void FailOutOfRangeError(int index)
	{
		if (index < m_capacity && (this.m_MinIndex != 0 || this.m_MaxIndex != m_capacity - 1))
			throw new IndexOutOfRangeException(
				$"Index {(object)index} is out of restricted IJobParallelFor range [{(object)this.m_MinIndex}...{(object)this.m_MaxIndex}] in ReadWriteBuffer.\nReadWriteBuffers are restricted to only read & write the element at the job index. You can use double buffering strategies to avoid race conditions due to reading & writing in parallel to the same elements from a job.");
		throw new IndexOutOfRangeException(
			$"Index {(object)index} is out of range of '{(object)m_capacity}' Length.");
	}
#endif
}

public struct MinHeapNode
{
	public MinHeapNode(PFNode pathFindingNode, float expectedCost)
	{
		PathFindingNode = pathFindingNode;
		ExpectedCost = expectedCost;
		Next = -1;
	}

	public PFNode PathFindingNode { get; } // TODO to position
	public float ExpectedCost { get; }
	public int Next { get; set; }
}*/