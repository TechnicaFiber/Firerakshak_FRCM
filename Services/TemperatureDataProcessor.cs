using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using FireRakshak.FRMC.Core.Models;

namespace FRCM.Services
{
    /// <summary>
    /// High-performance temperature data processor using a dedicated background thread.
    /// Uses System.Threading.Channels for lock-free producer-consumer pattern.
    ///
    /// Architecture:
    /// - DataStreamClient (network thread) -> Channel -> ProcessingThread -> UI Thread
    /// - Network thread never blocks waiting for processing
    /// - Processing thread handles data transformation
    /// - UI thread only handles final rendering (batched)
    /// </summary>
    public class TemperatureDataProcessor : IDisposable
    {
        private readonly Channel<TemperatureDataItem> _channel;
        private readonly CancellationTokenSource _cts;
        private readonly Task _processingTask;
        private bool _disposed;

        // Callback to update UI (will be invoked on processing thread, must marshal to UI)
        private readonly Action<int, List<TemperatureDataPoint>> _onDataReady;

        // Throttling - prevent overwhelming the UI
        private DateTime _lastUIUpdate = DateTime.MinValue;
        private readonly int _minUpdateIntervalMs;

        // Statistics
        private long _itemsProcessed;
        private long _itemsDropped;

        public long ItemsProcessed => Interlocked.Read(ref _itemsProcessed);
        public long ItemsDropped => Interlocked.Read(ref _itemsDropped);

        /// <summary>
        /// Creates a new temperature data processor.
        /// </summary>
        /// <param name="onDataReady">Callback when processed data is ready for UI. Called on background thread.</param>
        /// <param name="minUpdateIntervalMs">Minimum interval between UI updates (throttling)</param>
        /// <param name="maxQueueSize">Maximum items in queue before dropping old data</param>
        public TemperatureDataProcessor(
            Action<int, List<TemperatureDataPoint>> onDataReady,
            int minUpdateIntervalMs = 100,
            int maxQueueSize = 10)
        {
            _onDataReady = onDataReady ?? throw new ArgumentNullException(nameof(onDataReady));
            _minUpdateIntervalMs = minUpdateIntervalMs;

            // Bounded channel - drops oldest when full (prevents memory buildup)
            _channel = Channel.CreateBounded<TemperatureDataItem>(new BoundedChannelOptions(maxQueueSize)
            {
                FullMode = BoundedChannelFullMode.DropOldest,
                SingleReader = true,
                SingleWriter = false // Multiple network threads could write
            });

            _cts = new CancellationTokenSource();
            _processingTask = Task.Run(() => ProcessingLoopAsync(_cts.Token));

            Console.WriteLine($"[TEMP PROCESSOR] Started with {minUpdateIntervalMs}ms throttle, queue size {maxQueueSize}");
        }

        /// <summary>
        /// Enqueues temperature data for processing. Non-blocking.
        /// Called from network thread (DataStreamClient).
        /// </summary>
        public bool Enqueue(int channelId, List<TemperaturePoint> dataPoints)
        {
            if (_disposed) return false;

            var item = new TemperatureDataItem
            {
                ChannelId = channelId,
                DataPoints = dataPoints,
                ReceivedAt = DateTime.UtcNow
            };

            // TryWrite is non-blocking
            if (_channel.Writer.TryWrite(item))
            {
                return true;
            }
            else
            {
                Interlocked.Increment(ref _itemsDropped);
                return false;
            }
        }

        private async Task ProcessingLoopAsync(CancellationToken cancellationToken)
        {
            Console.WriteLine("[TEMP PROCESSOR] Processing loop started");

            try
            {
                await foreach (var item in _channel.Reader.ReadAllAsync(cancellationToken))
                {
                    try
                    {
                        ProcessItem(item);
                        Interlocked.Increment(ref _itemsProcessed);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[TEMP PROCESSOR] Error processing item: {ex.Message}");
                    }
                }
            }
            catch (OperationCanceledException)
            {
                Console.WriteLine("[TEMP PROCESSOR] Processing loop cancelled");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[TEMP PROCESSOR] Processing loop error: {ex.Message}");
            }

            Console.WriteLine($"[TEMP PROCESSOR] Processing loop ended. Processed: {_itemsProcessed}, Dropped: {_itemsDropped}");
        }

        private void ProcessItem(TemperatureDataItem item)
        {
            // Throttle UI updates
            var now = DateTime.UtcNow;
            if ((now - _lastUIUpdate).TotalMilliseconds < _minUpdateIntervalMs)
            {
                // Skip this update (too soon)
                return;
            }

            _lastUIUpdate = now;

            // Convert to TemperatureDataPoint list
            var dataPoints = new List<TemperatureDataPoint>(item.DataPoints.Count);
            foreach (var point in item.DataPoints)
            {
                dataPoints.Add(new TemperatureDataPoint(point.Temperature, point.Position));
            }

            // Invoke callback (caller must marshal to UI thread)
            _onDataReady(item.ChannelId, dataPoints);
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            Console.WriteLine("[TEMP PROCESSOR] Disposing...");

            _channel.Writer.Complete();
            _cts.Cancel();

            try
            {
                _processingTask.Wait(TimeSpan.FromSeconds(2));
            }
            catch { }

            _cts.Dispose();

            Console.WriteLine($"[TEMP PROCESSOR] Disposed. Total processed: {_itemsProcessed}, dropped: {_itemsDropped}");
        }

        private class TemperatureDataItem
        {
            public int ChannelId { get; set; }
            public List<TemperaturePoint> DataPoints { get; set; } = new();
            public DateTime ReceivedAt { get; set; }
        }
    }
}
