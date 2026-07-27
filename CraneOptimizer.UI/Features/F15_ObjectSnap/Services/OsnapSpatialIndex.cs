using CraneOptimizer.UI.Features.F15_ObjectSnap.Models;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Windows.Media.Media3D;

namespace CraneOptimizer.UI.Features.F15_ObjectSnap.Services
{
    internal readonly record struct OsnapWorldBounds(Point3D Min, Point3D Max)
    {
        public static OsnapWorldBounds Empty { get; } = new(
            new Point3D(double.PositiveInfinity, double.PositiveInfinity, double.PositiveInfinity),
            new Point3D(double.NegativeInfinity, double.NegativeInfinity, double.NegativeInfinity));

        public bool IsEmpty =>
            Min.X > Max.X ||
            Min.Y > Max.Y ||
            Min.Z > Max.Z;

        public Point3D Center => IsEmpty
            ? default
            : new Point3D(
                Min.X / 2.0 + Max.X / 2.0,
                Min.Y / 2.0 + Max.Y / 2.0,
                Min.Z / 2.0 + Max.Z / 2.0);

        public Vector3D Extents => IsEmpty
            ? default
            : new Vector3D(
                Max.X / 2.0 - Min.X / 2.0,
                Max.Y / 2.0 - Min.Y / 2.0,
                Max.Z / 2.0 - Min.Z / 2.0);

        public double MaxExtent => IsEmpty
            ? 0
            : Math.Max(
                Max.X - Min.X,
                Math.Max(Max.Y - Min.Y, Max.Z - Min.Z));

        public OsnapWorldBounds Include(Point3D point)
        {
            if (!IsFinite(point))
                return this;

            if (IsEmpty)
                return new OsnapWorldBounds(point, point);

            return new OsnapWorldBounds(
                new Point3D(
                    Math.Min(Min.X, point.X),
                    Math.Min(Min.Y, point.Y),
                    Math.Min(Min.Z, point.Z)),
                new Point3D(
                    Math.Max(Max.X, point.X),
                    Math.Max(Max.Y, point.Y),
                    Math.Max(Max.Z, point.Z)));
        }

        public OsnapWorldBounds Include(OsnapWorldBounds other)
        {
            if (other.IsEmpty)
                return this;

            return Include(other.Min).Include(other.Max);
        }

        private static bool IsFinite(Point3D point) =>
            double.IsFinite(point.X) &&
            double.IsFinite(point.Y) &&
            double.IsFinite(point.Z);
    }

    internal sealed class OsnapSpatialIndex<T>
    {
        private const int LeafCapacity = 8;

        private readonly IndexedItem[] _items;
        private readonly int[] _order;
        private readonly Node[] _nodes;

        private OsnapSpatialIndex(
            IndexedItem[] items,
            int[] order,
            Node[] nodes,
            OsnapWorldBounds bounds)
        {
            _items = items;
            _order = order;
            _nodes = nodes;
            Bounds = bounds;
        }

        public OsnapWorldBounds Bounds { get; }
        public int Count => _items.Length;

        public static OsnapSpatialIndex<T> Build(
            IReadOnlyList<T> source,
            Func<T, OsnapWorldBounds> boundsSelector,
            CancellationToken cancellationToken)
        {
            if (source.Count == 0)
            {
                return new OsnapSpatialIndex<T>(
                    Array.Empty<IndexedItem>(),
                    Array.Empty<int>(),
                    Array.Empty<Node>(),
                    OsnapWorldBounds.Empty);
            }

            var items = new IndexedItem[source.Count];
            var order = new int[source.Count];
            OsnapWorldBounds totalBounds = OsnapWorldBounds.Empty;
            for (int index = 0; index < source.Count; index++)
            {
                if ((index & 255) == 0)
                    cancellationToken.ThrowIfCancellationRequested();

                OsnapWorldBounds bounds = boundsSelector(source[index]);
                items[index] = new IndexedItem(source[index], bounds);
                order[index] = index;
                totalBounds = totalBounds.Include(bounds);
            }

            var nodes = new List<Node>(Math.Max(1, source.Count / LeafCapacity));
            BuildNode(items, order, nodes, 0, order.Length, cancellationToken);
            return new OsnapSpatialIndex<T>(
                items,
                order,
                nodes.ToArray(),
                totalBounds);
        }

        public IReadOnlyList<T> QueryNearRay(
            OsnapViewportSnapshot snapshot,
            OsnapRay ray,
            double radiusPixels,
            int maxResults,
            Func<T, bool> filter,
            Func<T, OsnapRay, OsnapViewportSnapshot, double> distanceSelector,
            CancellationToken cancellationToken)
        {
            if (_nodes.Length == 0 || maxResults <= 0)
                return Array.Empty<T>();

            var collector = new BoundedCollector(maxResults);
            var stack = new int[Math.Min(_nodes.Length, 256)];
            int stackCount = 1;
            stack[0] = 0;
            int visitCount = 0;

            while (stackCount > 0)
            {
                if ((++visitCount & 63) == 0)
                    cancellationToken.ThrowIfCancellationRequested();

                int nodeIndex = stack[--stackCount];
                Node node = _nodes[nodeIndex];
                if (!TouchesCursorRay(node.Bounds, snapshot, ray, radiusPixels))
                    continue;

                if (node.IsLeaf)
                {
                    int end = node.Start + node.Count;
                    for (int offset = node.Start; offset < end; offset++)
                    {
                        T item = _items[_order[offset]].Item;
                        if (!filter(item))
                            continue;

                        double distance =
                            distanceSelector(item, ray, snapshot);
                        if (!double.IsFinite(distance))
                            continue;

                        collector.TryAdd(item, distance);
                    }

                    continue;
                }

                EnsureStackCapacity(ref stack, stackCount + 2);
                Node left = _nodes[node.Left];
                Node right = _nodes[node.Right];
                double leftDistance = DistanceToBoundsCenterSquared(ray, left.Bounds);
                double rightDistance = DistanceToBoundsCenterSquared(ray, right.Bounds);
                if (leftDistance <= rightDistance)
                {
                    stack[stackCount++] = node.Right;
                    stack[stackCount++] = node.Left;
                }
                else
                {
                    stack[stackCount++] = node.Left;
                    stack[stackCount++] = node.Right;
                }
            }

            return collector.ToItems();
        }

        public static double DistancePointToCursorRay(
            Point3D point,
            OsnapRay ray,
            OsnapViewportSnapshot snapshot)
        {
            double depth = OsnapProjectionMath.GetViewDepth(snapshot, point);
            if (!double.IsFinite(depth) || depth <= 0)
                return double.PositiveInfinity;

            Vector3D offset = point - ray.Origin;
            double along = Vector3D.DotProduct(offset, ray.Direction);
            if (along < 0)
                return double.PositiveInfinity;

            Point3D closest = ray.Origin + ray.Direction * along;
            return (point - closest).Length;
        }

        public static double DistanceSegmentToCursorRay(
            Point3D start,
            Point3D end,
            OsnapRay ray,
            OsnapViewportSnapshot snapshot)
        {
            double startDepth = OsnapProjectionMath.GetViewDepth(snapshot, start);
            double endDepth = OsnapProjectionMath.GetViewDepth(snapshot, end);
            if (Math.Max(startDepth, endDepth) <= 0)
                return double.PositiveInfinity;

            Vector3D segment = end - start;
            double segmentLengthSquared = segment.LengthSquared;
            if (segmentLengthSquared < 1e-12)
                return DistancePointToCursorRay(start, ray, snapshot);

            Vector3D direction = ray.Direction;
            double directionLengthSquared = direction.LengthSquared;
            if (directionLengthSquared < 1e-12)
                return double.PositiveInfinity;

            direction.Normalize();
            Vector3D fromRayOrigin = start - ray.Origin;
            double segmentDirectionDot =
                Vector3D.DotProduct(segment, direction);
            double segmentOriginDot =
                Vector3D.DotProduct(segment, fromRayOrigin);
            double directionOriginDot =
                Vector3D.DotProduct(direction, fromRayOrigin);
            double denominator =
                segmentLengthSquared -
                segmentDirectionDot * segmentDirectionDot;

            double bestDistanceSquared = double.PositiveInfinity;
            if (denominator >
                segmentLengthSquared * 1e-12)
            {
                double segmentParameter =
                    (segmentDirectionDot * directionOriginDot -
                     segmentOriginDot) /
                    denominator;
                double rayParameter =
                    directionOriginDot +
                    segmentDirectionDot * segmentParameter;
                if (segmentParameter >= 0.0 &&
                    segmentParameter <= 1.0 &&
                    rayParameter >= 0.0)
                {
                    bestDistanceSquared = DistanceSquared(
                        ray.Origin + direction * rayParameter,
                        start + segment * segmentParameter);
                }
            }

            ConsiderEndpoint(start);
            ConsiderEndpoint(end);

            double originSegmentParameter = Math.Clamp(
                -segmentOriginDot / segmentLengthSquared,
                0.0,
                1.0);
            bestDistanceSquared = Math.Min(
                bestDistanceSquared,
                DistanceSquared(
                    ray.Origin,
                    start + segment * originSegmentParameter));
            return Math.Sqrt(bestDistanceSquared);

            void ConsiderEndpoint(Point3D endpoint)
            {
                double rayParameter = Math.Max(
                    0.0,
                    Vector3D.DotProduct(
                        endpoint - ray.Origin,
                        direction));
                bestDistanceSquared = Math.Min(
                    bestDistanceSquared,
                    DistanceSquared(
                        ray.Origin + direction * rayParameter,
                        endpoint));
            }
        }

        private static double DistanceSquared(
            Point3D first,
            Point3D second) =>
            (first - second).LengthSquared;

        private static int BuildNode(
            IndexedItem[] items,
            int[] order,
            List<Node> nodes,
            int start,
            int count,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            OsnapWorldBounds bounds = OsnapWorldBounds.Empty;
            for (int index = start; index < start + count; index++)
            {
                if ((index & 255) == 0)
                    cancellationToken.ThrowIfCancellationRequested();

                bounds = bounds.Include(items[order[index]].Bounds);
            }

            int nodeIndex = nodes.Count;
            nodes.Add(default);
            if (count <= LeafCapacity)
            {
                nodes[nodeIndex] = Node.Leaf(bounds, start, count);
                return nodeIndex;
            }

            Vector3D extents = bounds.Extents;
            int axis = extents.X >= extents.Y && extents.X >= extents.Z
                ? 0
                : extents.Y >= extents.Z
                    ? 1
                    : 2;
            PartitionAroundMedian(
                items,
                order,
                start,
                count,
                axis,
                cancellationToken);

            int leftCount = count / 2;
            int left = BuildNode(
                items,
                order,
                nodes,
                start,
                leftCount,
                cancellationToken);
            int right = BuildNode(
                items,
                order,
                nodes,
                start + leftCount,
                count - leftCount,
                cancellationToken);
            nodes[nodeIndex] = Node.Branch(bounds, left, right);
            return nodeIndex;
        }

        private static bool TouchesCursorRay(
            OsnapWorldBounds bounds,
            OsnapViewportSnapshot snapshot,
            OsnapRay ray,
            double radiusPixels)
        {
            if (bounds.IsEmpty)
                return false;

            Point3D center = bounds.Center;
            Vector3D extents = bounds.Extents;
            Vector3D forward = snapshot.LookDirection;
            forward.Normalize();
            double centerDepth =
                Vector3D.DotProduct(center - snapshot.CameraPosition, forward);
            double depthExtent =
                Math.Abs(forward.X) * extents.X +
                Math.Abs(forward.Y) * extents.Y +
                Math.Abs(forward.Z) * extents.Z;
            double farDepth = centerDepth + depthExtent;
            if (farDepth <= 0)
                return false;

            double expansion = Math.Max(
                1e-6,
                OsnapProjectionMath.WorldRadiusAtDepth(
                    snapshot,
                    farDepth,
                    radiusPixels) * 1.15);
            Point3D min = new(
                bounds.Min.X - expansion,
                bounds.Min.Y - expansion,
                bounds.Min.Z - expansion);
            Point3D max = new(
                bounds.Max.X + expansion,
                bounds.Max.Y + expansion,
                bounds.Max.Z + expansion);
            return RayIntersectsBounds(ray, min, max);
        }

        private static bool RayIntersectsBounds(
            OsnapRay ray,
            Point3D min,
            Point3D max)
        {
            double minimum = 0.0;
            double maximum = double.PositiveInfinity;
            return IntersectAxis(
                       ray.Origin.X,
                       ray.Direction.X,
                       min.X,
                       max.X,
                       ref minimum,
                       ref maximum) &&
                   IntersectAxis(
                       ray.Origin.Y,
                       ray.Direction.Y,
                       min.Y,
                       max.Y,
                       ref minimum,
                       ref maximum) &&
                   IntersectAxis(
                       ray.Origin.Z,
                       ray.Direction.Z,
                       min.Z,
                       max.Z,
                       ref minimum,
                       ref maximum);
        }

        private static bool IntersectAxis(
            double origin,
            double direction,
            double min,
            double max,
            ref double minimum,
            ref double maximum)
        {
            if (Math.Abs(direction) < 1e-12)
                return origin >= min && origin <= max;

            double first = (min - origin) / direction;
            double second = (max - origin) / direction;
            if (first > second)
                (first, second) = (second, first);

            minimum = Math.Max(minimum, first);
            maximum = Math.Min(maximum, second);
            return maximum >= minimum;
        }

        private static double DistanceToBoundsCenterSquared(
            OsnapRay ray,
            OsnapWorldBounds bounds)
        {
            Point3D center = bounds.Center;
            Vector3D offset = center - ray.Origin;
            double along = Math.Max(
                0.0,
                Vector3D.DotProduct(offset, ray.Direction));
            Point3D closest = ray.Origin + ray.Direction * along;
            return (center - closest).LengthSquared;
        }

        private static void EnsureStackCapacity(ref int[] stack, int needed)
        {
            if (needed <= stack.Length)
                return;

            int newLength = Math.Max(needed, stack.Length * 2);
            Array.Resize(ref stack, newLength);
        }

        private static void PartitionAroundMedian(
            IndexedItem[] items,
            int[] order,
            int start,
            int count,
            int axis,
            CancellationToken cancellationToken)
        {
            int target = start + count / 2;
            int low = start;
            int high = start + count - 1;
            int comparisonCount = 0;
            while (low < high)
            {
                cancellationToken.ThrowIfCancellationRequested();
                double pivot = CenterValue(
                    items[order[low + (high - low) / 2]].Bounds,
                    axis);
                int less = low;
                int current = low;
                int greater = high;
                while (current <= greater)
                {
                    if ((++comparisonCount & 255) == 0)
                        cancellationToken.ThrowIfCancellationRequested();

                    double value = CenterValue(
                        items[order[current]].Bounds,
                        axis);
                    int comparison = value.CompareTo(pivot);
                    if (comparison < 0)
                    {
                        Swap(order, less, current);
                        less++;
                        current++;
                    }
                    else if (comparison > 0)
                    {
                        Swap(order, current, greater);
                        greater--;
                    }
                    else
                    {
                        current++;
                    }
                }

                if (target < less)
                {
                    high = less - 1;
                }
                else if (target > greater)
                {
                    low = greater + 1;
                }
                else
                {
                    return;
                }
            }
        }

        private static double CenterValue(
            OsnapWorldBounds bounds,
            int axis)
        {
            Point3D center = bounds.Center;
            return axis switch
            {
                0 => center.X,
                1 => center.Y,
                _ => center.Z
            };
        }

        private static void Swap(
            int[] values,
            int first,
            int second)
        {
            if (first == second)
                return;

            (values[first], values[second]) =
                (values[second], values[first]);
        }

        private readonly record struct IndexedItem(
            T Item,
            OsnapWorldBounds Bounds);

        private readonly record struct Node(
            OsnapWorldBounds Bounds,
            int Start,
            int Count,
            int Left,
            int Right)
        {
            public bool IsLeaf => Left < 0;

            public static Node Leaf(
                OsnapWorldBounds bounds,
                int start,
                int count) =>
                new(bounds, start, count, -1, -1);

            public static Node Branch(
                OsnapWorldBounds bounds,
                int left,
                int right) =>
                new(bounds, 0, 0, left, right);
        }

        private sealed class BoundedCollector
        {
            private readonly int _capacity;
            private readonly List<(T Item, double Distance)> _items;

            public BoundedCollector(int capacity)
            {
                _capacity = capacity;
                _items = new List<(T Item, double Distance)>(capacity);
            }

            public void TryAdd(T item, double distance)
            {
                if (_items.Count < _capacity)
                {
                    _items.Add((item, distance));
                    return;
                }

                int worstIndex = 0;
                double worstDistance = _items[0].Distance;
                for (int index = 1; index < _items.Count; index++)
                {
                    if (_items[index].Distance <= worstDistance)
                        continue;

                    worstIndex = index;
                    worstDistance = _items[index].Distance;
                }

                if (distance < worstDistance)
                    _items[worstIndex] = (item, distance);
            }

            public IReadOnlyList<T> ToItems()
            {
                _items.Sort(static (first, second) =>
                    first.Distance.CompareTo(second.Distance));
                var result = new T[_items.Count];
                for (int index = 0; index < _items.Count; index++)
                    result[index] = _items[index].Item;
                return result;
            }
        }
    }
}
