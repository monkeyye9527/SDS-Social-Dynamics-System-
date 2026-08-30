using System;
using System.Collections.Generic;

namespace ONIModPack.Core
{
    /// <summary>
    /// Partitions the world grid into regions and refreshes one partition per tick
    /// instead of updating every cell every frame.
    /// </summary>
    public class PartitionedRegionalUpdater<TCell>
    {
        private readonly int _regionSize;
        private readonly int _refreshIntervalCycles;
        private readonly Func<int, int, TCell> _cellFactory;
        private readonly Dictionary<(int, int), TCell> _cells = new Dictionary<(int, int), TCell>();
        private int _cycleCounter;
        private int _partitionIndex;
        private int _partitionCount;

        public int RegionSize => _regionSize;
        public int RefreshIntervalCycles => _refreshIntervalCycles;

        public PartitionedRegionalUpdater(
            int regionSize = 8,
            int refreshIntervalCycles = 15,
            Func<int, int, TCell> cellFactory = null)
        {
            _regionSize = Math.Max(1, regionSize);
            _refreshIntervalCycles = Math.Max(1, refreshIntervalCycles);
            _cellFactory = cellFactory ?? ((_, __) => default);
            _partitionCount = 4;
        }

        public void SetPartitionCount(int count) =>
            _partitionCount = Math.Max(1, count);

        public TCell GetOrCreate(int gridX, int gridY)
        {
            var key = ToRegionKey(gridX, gridY);
            if (!_cells.TryGetValue(key, out var cell))
            {
                cell = _cellFactory(key.Item1, key.Item2);
                _cells[key] = cell;
            }
            return cell;
        }

        public bool TryGet(int gridX, int gridY, out TCell cell)
        {
            return _cells.TryGetValue(ToRegionKey(gridX, gridY), out cell);
        }

        public void TickCycle(Action<TCell, int, int> refreshAction)
        {
            _cycleCounter++;
            if (_cycleCounter % _refreshIntervalCycles != 0)
                return;

            int currentPartition = _partitionIndex % _partitionCount;
            _partitionIndex++;

            foreach (var kvp in _cells)
            {
                int partition = Math.Abs(kvp.Key.Item1 + kvp.Key.Item2) % _partitionCount;
                if (partition != currentPartition) continue;

                try
                {
                    refreshAction(kvp.Value, kvp.Key.Item1, kvp.Key.Item2);
                }
                catch (Exception ex)
                {
                    ModLogger.Error($"[PartitionedUpdater] Refresh failed at ({kvp.Key.Item1},{kvp.Key.Item2}): {ex.Message}");
                }
            }
        }

        public void ForEachInRadius(int centerX, int centerY, int radius, Action<TCell, int, int> action)
        {
            int minRx = (centerX - radius) / _regionSize;
            int maxRx = (centerX + radius) / _regionSize;
            int minRy = (centerY - radius) / _regionSize;
            int maxRy = (centerY + radius) / _regionSize;

            for (int rx = minRx; rx <= maxRx; rx++)
            {
                for (int ry = minRy; ry <= maxRy; ry++)
                {
                    if (_cells.TryGetValue((rx, ry), out var cell))
                        action(cell, rx, ry);
                }
            }
        }

        public void Clear() => _cells.Clear();

        private (int, int) ToRegionKey(int gridX, int gridY)
        {
            int rx = gridX >= 0 ? gridX / _regionSize : (gridX - _regionSize + 1) / _regionSize;
            int ry = gridY >= 0 ? gridY / _regionSize : (gridY - _regionSize + 1) / _regionSize;
            return (rx, ry);
        }
    }
}
