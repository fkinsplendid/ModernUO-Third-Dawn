using System;
using System.Collections.Generic;

namespace Server.Engines.TownLife;

/// <summary>
/// Every tile of a town a person can walk to, with its height, found by flooding out from the
/// town centre with the real movement rules. Doors count as open, so shops, the tavern and
/// homes are part of it. Built a slice at a time so a large town doesn't stall the server.
///
/// The engine's own pathfinder only looks about nineteen tiles ahead, and a town is hundreds
/// across, so long walks are planned here and then walked a short leg at a time.
/// </summary>
public sealed class StreetMap
{
    private const sbyte NotWalkable = sbyte.MinValue;
    private const int MaxClimb = 12;
    private const int WaypointSpacing = 8;
    private const int MaxSearchNodes = 120000;

    private readonly Map _map;
    private readonly Region _region;
    private readonly Rectangle2D _bounds;
    private readonly sbyte[] _z;
    private readonly Queue<int> _frontier = new();

    // Pathfinding scratch, reused: the server is single-threaded.
    private readonly int[] _cameFrom;
    private readonly int[] _cost;
    private readonly int[] _stamp;
    private int _generation;

    public StreetMap(Map map, Region region, Rectangle2D bounds)
    {
        _map = map;
        _region = region;
        _bounds = bounds;

        var size = bounds.Width * bounds.Height;
        _z = new sbyte[size];
        _cameFrom = new int[size];
        _cost = new int[size];
        _stamp = new int[size];
        Array.Fill(_z, NotWalkable);
    }

    public bool IsReady { get; private set; }

    public int WalkableTiles { get; private set; }

    public Map Map => _map;

    /// <summary>Starts the flood from <paramref name="seed"/>, a tile in the middle of town.</summary>
    public void Seed(Point3D seed)
    {
        if (!TryIndex(seed.X, seed.Y, out var index) || _z[index] != NotWalkable)
        {
            return;
        }

        _z[index] = (sbyte)seed.Z;
        WalkableTiles++;
        _frontier.Enqueue(index);
    }

    /// <summary>Floods up to <paramref name="budget"/> more tiles. Ready once nothing is left to explore.</summary>
    public void Build(Mobile probe, int budget)
    {
        if (IsReady)
        {
            return;
        }

        var ignoreDoors = Movement.MovementImpl.AlwaysIgnoreDoors;
        Movement.MovementImpl.AlwaysIgnoreDoors = true;

        try
        {
            while (budget-- > 0 && _frontier.Count > 0)
            {
                var index = _frontier.Dequeue();
                var from = ToPoint(index);

                for (var d = 0; d < 8; d++)
                {
                    if (!Movement.Movement.CheckMovement(probe, _map, from, (Direction)d, out var newZ))
                    {
                        continue;
                    }

                    var x = from.X;
                    var y = from.Y;
                    Movement.Movement.Offset((Direction)d, ref x, ref y);

                    if (!TryIndex(x, y, out var next) || _z[next] != NotWalkable || newZ is < -127 or > 127 ||
                        !_region.Contains(new Point3D(x, y, newZ)))
                    {
                        continue;
                    }

                    _z[next] = (sbyte)newZ;
                    WalkableTiles++;
                    _frontier.Enqueue(next);
                }
            }
        }
        finally
        {
            Movement.MovementImpl.AlwaysIgnoreDoors = ignoreDoors;
        }

        IsReady = _frontier.Count == 0;
    }

    public bool IsWalkable(int x, int y) => TryIndex(x, y, out var index) && _z[index] != NotWalkable;

    public bool TryGetTile(int x, int y, out Point3D tile)
    {
        if (TryIndex(x, y, out var index) && _z[index] != NotWalkable)
        {
            tile = new Point3D(x, y, _z[index]);
            return true;
        }

        tile = default;
        return false;
    }

    /// <summary>The walkable tile nearest <paramref name="p"/>, searching outward up to <paramref name="radius"/>.</summary>
    public bool TryFindNearest(Point3D p, int radius, out Point3D tile)
    {
        for (var r = 0; r <= radius; r++)
        {
            for (var dx = -r; dx <= r; dx++)
            {
                for (var dy = -r; dy <= r; dy++)
                {
                    if (Math.Max(Math.Abs(dx), Math.Abs(dy)) == r && TryGetTile(p.X + dx, p.Y + dy, out tile))
                    {
                        return true;
                    }
                }
            }
        }

        tile = default;
        return false;
    }

    /// <summary>The closest walkable tile under open sky within <paramref name="radius"/> of <paramref name="p"/>: the street outside a building.</summary>
    public bool TryFindNearestOutdoors(Point3D p, int radius, out Point3D tile)
    {
        for (var r = 0; r <= radius; r++)
        {
            for (var dx = -r; dx <= r; dx++)
            {
                for (var dy = -r; dy <= r; dy++)
                {
                    if (Math.Max(Math.Abs(dx), Math.Abs(dy)) == r && TryGetTile(p.X + dx, p.Y + dy, out tile) &&
                        IsOutdoors(tile))
                    {
                        return true;
                    }
                }
            }
        }

        tile = default;
        return false;
    }

    /// <summary>A random walkable tile within <paramref name="radius"/> of <paramref name="p"/>, so people don't all stand on one spot.</summary>
    public bool TryFindNear(Point3D p, int radius, out Point3D tile)
    {
        for (var attempt = 0; attempt < 12; attempt++)
        {
            if (TryGetTile(p.X + Utility.RandomMinMax(-radius, radius), p.Y + Utility.RandomMinMax(-radius, radius), out tile))
            {
                return true;
            }
        }

        return TryFindNearest(p, radius, out tile);
    }

    /// <summary>A random walkable tile anywhere in town, out in the open.</summary>
    public bool TryFindRandom(out Point3D tile)
    {
        for (var attempt = 0; attempt < 128; attempt++)
        {
            if (TryGetTile(_bounds.X + Utility.Random(_bounds.Width), _bounds.Y + Utility.Random(_bounds.Height), out tile) &&
                IsOutdoors(tile))
            {
                return true;
            }
        }

        tile = default;
        return false;
    }

    /// <summary>
    /// The way from <paramref name="start"/> to <paramref name="goal"/> across town, as waypoints a
    /// few tiles apart, ending at the goal. Empty if there's no way.
    /// </summary>
    public List<Point3D> FindRoute(Point3D start, Point3D goal)
    {
        var route = new List<Point3D>();

        if (!IsReady || !TryFindNearest(start, 3, out start) || !TryFindNearest(goal, 3, out goal) ||
            !TryIndex(start.X, start.Y, out var from) || !TryIndex(goal.X, goal.Y, out var to))
        {
            return route;
        }

        if (++_generation == int.MaxValue)
        {
            Array.Clear(_stamp);
            _generation = 1;
        }

        var open = new PriorityQueue<int, int>();
        _stamp[from] = _generation;
        _cost[from] = 0;
        _cameFrom[from] = -1;
        open.Enqueue(from, 0);

        var explored = 0;
        var found = false;

        while (open.TryDequeue(out var current, out _))
        {
            if (current == to)
            {
                found = true;
                break;
            }

            if (++explored > MaxSearchNodes)
            {
                break;
            }

            var cx = current % _bounds.Width;
            var cy = current / _bounds.Width;
            var cz = _z[current];

            for (var d = 0; d < 8; d++)
            {
                var x = cx;
                var y = cy;
                Movement.Movement.Offset((Direction)d, ref x, ref y);

                if (x < 0 || y < 0 || x >= _bounds.Width || y >= _bounds.Height)
                {
                    continue;
                }

                var next = y * _bounds.Width + x;
                var nz = _z[next];

                if (nz == NotWalkable || Math.Abs(nz - cz) > MaxClimb)
                {
                    continue;
                }

                // Diagonals only where both corners are open too, as people walk.
                var diagonal = (d & 1) == 1;

                if (diagonal && (_z[cy * _bounds.Width + x] == NotWalkable || _z[y * _bounds.Width + cx] == NotWalkable))
                {
                    continue;
                }

                var cost = _cost[current] + (diagonal ? 14 : 10);

                if (_stamp[next] == _generation && _cost[next] <= cost)
                {
                    continue;
                }

                _stamp[next] = _generation;
                _cost[next] = cost;
                _cameFrom[next] = current;

                var hx = Math.Abs(x - (to % _bounds.Width));
                var hy = Math.Abs(y - (to / _bounds.Width));
                open.Enqueue(next, cost + 10 * Math.Max(hx, hy) + 4 * Math.Min(hx, hy));
            }
        }

        if (!found)
        {
            return route;
        }

        // Walk back from the goal, keeping every few tiles as a waypoint.
        var step = 0;

        for (var node = to; node != -1; node = _cameFrom[node])
        {
            if (node == to || node == from || step++ % WaypointSpacing == 0)
            {
                route.Add(ToPoint(node));
            }
        }

        route.Reverse();

        // The start itself isn't a place to walk to.
        if (route.Count > 1)
        {
            route.RemoveAt(0);
        }

        return route;
    }

    /// <summary>Nothing overhead: no roof, upper floor or balcony. Out in the street, not in someone's parlour.</summary>
    public bool IsOutdoors(Point3D tile)
    {
        foreach (var item in _map.Tiles.GetStaticTiles(tile.X, tile.Y))
        {
            if (item.Z > tile.Z + 6)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>For tests, which have no map to flood: marks a tile walkable by hand.</summary>
    internal void MarkWalkable(int x, int y, int z)
    {
        if (TryIndex(x, y, out var index) && _z[index] == NotWalkable)
        {
            _z[index] = (sbyte)z;
            WalkableTiles++;
        }
    }

    /// <summary>For tests: declares the hand-made map finished.</summary>
    internal void MarkReady() => IsReady = true;

    private bool TryIndex(int x, int y, out int index)
    {
        x -= _bounds.X;
        y -= _bounds.Y;

        if (x < 0 || y < 0 || x >= _bounds.Width || y >= _bounds.Height)
        {
            index = -1;
            return false;
        }

        index = y * _bounds.Width + x;
        return true;
    }

    private Point3D ToPoint(int index) =>
        new(_bounds.X + index % _bounds.Width, _bounds.Y + index / _bounds.Width, _z[index]);
}
