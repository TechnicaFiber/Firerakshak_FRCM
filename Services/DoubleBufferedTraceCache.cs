using System;
using System.Collections.Generic;

namespace FRCM.Services
{
    /// <summary>
    /// FM-07: Double-Buffered Trace Cache for thread-safe lock-free live graph rendering.
    /// Provides atomic snapshot swapping between background ingestion/processor threads
    /// and the UI rendering / mouse-tracking thread.
    /// Eliminates CollectionWasModified and IndexOutOfRange race conditions.
    /// </summary>
    public class DoubleBufferedTraceCache
    {
        public class TraceSnapshot
        {
            public double[] Distances { get; }
            public double[] Temperatures { get; }
            public int Count => Distances.Length;
            public double MinDistance { get; }
            public double MaxDistance { get; }
            public double MinTemperature { get; }
            public double MaxTemperature { get; }
            public DateTime TimestampUtc { get; }

            public static readonly TraceSnapshot Empty = new(Array.Empty<double>(), Array.Empty<double>());

            public TraceSnapshot(double[] distances, double[] temperatures)
            {
                Distances = distances ?? Array.Empty<double>();
                Temperatures = temperatures ?? Array.Empty<double>();
                TimestampUtc = DateTime.UtcNow;

                if (Distances.Length > 0)
                {
                    MinDistance = Distances[0];
                    MaxDistance = Distances[^1];
                }
                else
                {
                    MinDistance = 0;
                    MaxDistance = 0;
                }

                if (Temperatures.Length > 0)
                {
                    double min = double.MaxValue;
                    double max = double.MinValue;
                    for (int i = 0; i < Temperatures.Length; i++)
                    {
                        double t = Temperatures[i];
                        if (t < min) min = t;
                        if (t > max) max = t;
                    }
                    MinTemperature = min;
                    MaxTemperature = max;
                }
                else
                {
                    MinTemperature = 0;
                    MaxTemperature = 0;
                }
            }

            /// <summary>
            /// Fast, allocation-free binary search for the nearest plotted distance.
            /// </summary>
            public (double Distance, double Temperature, int Index) FindNearest(double targetDistance)
            {
                if (Distances.Length == 0)
                    return (0, 0, -1);

                int left = 0;
                int right = Distances.Length - 1;

                if (targetDistance <= Distances[left])
                    return (Distances[left], Temperatures.Length > left ? Temperatures[left] : 0, left);
                if (targetDistance >= Distances[right])
                    return (Distances[right], Temperatures.Length > right ? Temperatures[right] : 0, right);

                while (left <= right)
                {
                    int mid = (left + right) / 2;
                    if (Math.Abs(Distances[mid] - targetDistance) < 0.0001)
                    {
                        double temp = Temperatures.Length > mid ? Temperatures[mid] : 0;
                        return (Distances[mid], temp, mid);
                    }

                    if (Distances[mid] < targetDistance)
                        left = mid + 1;
                    else
                        right = mid - 1;
                }

                if (left >= Distances.Length) left = Distances.Length - 1;
                if (right < 0) right = 0;

                double d1 = Math.Abs(Distances[left] - targetDistance);
                double d2 = Math.Abs(Distances[right] - targetDistance);

                int bestIdx = d1 < d2 ? left : right;
                double bestTemp = Temperatures.Length > bestIdx ? Temperatures[bestIdx] : 0;
                return (Distances[bestIdx], bestTemp, bestIdx);
            }
        }

        private TraceSnapshot _frontBuffer = TraceSnapshot.Empty;
        private readonly object _swapLock = new();

        /// <summary>
        /// Gets the current immutable front buffer snapshot for rendering or querying.
        /// Guaranteed to never throw collection modification exceptions.
        /// </summary>
        public TraceSnapshot FrontBuffer => _frontBuffer;

        /// <summary>
        /// Updates the back buffer and atomically promotes it to the front buffer.
        /// </summary>
        public TraceSnapshot Update(double[] distances, double[] temperatures)
        {
            var newSnapshot = new TraceSnapshot(distances, temperatures);
            lock (_swapLock)
            {
                _frontBuffer = newSnapshot;
            }
            return newSnapshot;
        }

        /// <summary>
        /// Updates from lists of doubles.
        /// </summary>
        public TraceSnapshot Update(List<double>? distances, List<double>? temperatures)
        {
            return Update(distances?.ToArray() ?? Array.Empty<double>(), temperatures?.ToArray() ?? Array.Empty<double>());
        }

        /// <summary>
        /// Clears the trace buffer to empty.
        /// </summary>
        public void Clear()
        {
            lock (_swapLock)
            {
                _frontBuffer = TraceSnapshot.Empty;
            }
        }
    }
}
