using System.Collections.Generic;
using UnityEngine;

namespace FunGuy.Runner
{
    public enum GridLandingType
    {
        Missing,
        Safe,
        Hazard
    }

    public sealed class GridWorld : MonoBehaviour
    {
        private sealed class CellOccupancy
        {
            public GridSurfaceActor Surface;
            public GridCollectibleActor Collectible;
        }

        private readonly Dictionary<Vector3Int, CellOccupancy> cells = new();

        private RunnerGridSystem gridSystem;

        public int ActiveCellCount => cells.Count;

        public void Configure(RunnerGridSystem runnerGridSystem)
        {
            gridSystem = runnerGridSystem;
        }

        public void ClearAll()
        {
            cells.Clear();
        }

        public void RegisterSurface(GridSurfaceActor surface)
        {
            if (surface == null)
            {
                return;
            }

            CellOccupancy occupancy = GetOrCreateOccupancy(surface.Cell);
            occupancy.Surface = surface;
        }

        public void UnregisterSurface(GridSurfaceActor surface)
        {
            if (surface == null)
            {
                return;
            }

            if (!cells.TryGetValue(surface.Cell, out CellOccupancy occupancy))
            {
                return;
            }

            if (occupancy.Surface == surface)
            {
                occupancy.Surface = null;
                TrimCell(surface.Cell, occupancy);
            }
        }

        public void RegisterCollectible(GridCollectibleActor collectible)
        {
            if (collectible == null)
            {
                return;
            }

            CellOccupancy occupancy = GetOrCreateOccupancy(collectible.Cell);
            occupancy.Collectible = collectible;
        }

        public void UnregisterCollectible(GridCollectibleActor collectible)
        {
            if (collectible == null)
            {
                return;
            }

            if (!cells.TryGetValue(collectible.Cell, out CellOccupancy occupancy))
            {
                return;
            }

            if (occupancy.Collectible == collectible)
            {
                occupancy.Collectible = null;
                TrimCell(collectible.Cell, occupancy);
            }
        }

        public GridLandingType GetLandingType(Vector3Int cell, out GridSurfaceActor surface)
        {
            surface = null;

            if (gridSystem == null || !gridSystem.IsWithinPlayableBounds(cell))
            {
                return GridLandingType.Missing;
            }

            if (!cells.TryGetValue(cell, out CellOccupancy occupancy) ||
                occupancy.Surface == null ||
                !occupancy.Surface.IsLandingEnabled)
            {
                return GridLandingType.Missing;
            }

            surface = occupancy.Surface;
            return surface.IsHazard ? GridLandingType.Hazard : GridLandingType.Safe;
        }

        public bool HasCollectible(Vector3Int cell)
        {
            return cells.TryGetValue(cell, out CellOccupancy occupancy) && occupancy.Collectible != null;
        }

        public bool TryConsumeCollectible(Vector3Int cell, out GridCollectibleActor collectible)
        {
            collectible = null;

            if (!cells.TryGetValue(cell, out CellOccupancy occupancy) || occupancy.Collectible == null)
            {
                return false;
            }

            collectible = occupancy.Collectible;
            occupancy.Collectible = null;
            collectible.Collect();
            TrimCell(cell, occupancy);
            return true;
        }

        private CellOccupancy GetOrCreateOccupancy(Vector3Int cell)
        {
            if (!cells.TryGetValue(cell, out CellOccupancy occupancy))
            {
                occupancy = new CellOccupancy();
                cells.Add(cell, occupancy);
            }

            return occupancy;
        }

        private void TrimCell(Vector3Int cell, CellOccupancy occupancy)
        {
            if (occupancy.Surface == null && occupancy.Collectible == null)
            {
                cells.Remove(cell);
            }
        }
    }
}
