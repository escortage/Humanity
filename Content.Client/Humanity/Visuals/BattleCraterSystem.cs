using System.Numerics;
using System.Linq;
using Content.Shared.Humanity.Visuals;
using Content.Shared.Standing;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Shared.Prototypes;

namespace Content.Client.Humanity.Visuals;

public sealed partial class BattleCraterSystem : EntitySystem
{
    private static readonly ProtoId<BattleCraterVisualsPrototype> VisualsPrototype = "HumanityBattleCrater";
    private const float PixelSize = 1f / 16;

    [Dependency] private SharedTransformSystem _transforms = default!;
    [Dependency] private IPrototypeManager _prototypes = default!;
    [Dependency] private BattleCraterEntrySystem _entry = default!;
    [Dependency] private IOverlayManager _overlays = default!;
    private BattleCraterVisualsPrototype? _lastVisuals;
    private readonly Dictionary<EntityUid, float> _depths = new();
    private Dictionary<EntityUid, Crater> _lastCraters = new();
    private Dictionary<EntityUid, Crater> _currentCraters = new();
    private readonly HashSet<EntityUid> _changedGrids = new();
    private readonly List<EntityUid> _removedBodies = new();
    private readonly Dictionary<EntityUid, List<GroundStrip>> _mergedGround = new();

    private readonly record struct Crater(EntityUid Uid, EntityUid Grid, Vector2 Position, float Radius, int Seed);
    private readonly record struct GroundStrip(Box2 Bounds, Color Color);

    public override void Initialize()
    {
        base.Initialize();
        _overlays.AddOverlay(new BattleCraterOverlay(EntityManager));
    }

    public override void FrameUpdate(float frameTime)
    {
        base.FrameUpdate(frameTime);
        var visuals = _prototypes.Index(VisualsPrototype);
        _currentCraters.Clear();
        _changedGrids.Clear();
        var visualsChanged = !ReferenceEquals(visuals, _lastVisuals);
        var craters = EntityQueryEnumerator<BattleScarComponent, TransformComponent, SpriteComponent>();
        while (craters.MoveNext(out var uid, out var scar, out var transform, out var sprite))
        {
            if (scar.Rubble || transform.GridUid is not { } grid)
                continue;
            var scale = new Vector2(scar.Radius * 2);
            if (sprite.Scale != scale)
                sprite.Scale = scale;
            var crater = new Crater(uid, grid, transform.LocalPosition, scar.Radius, uid.GetHashCode());
            _currentCraters.Add(uid, crater);
            if (visualsChanged || !_lastCraters.TryGetValue(uid, out var previous) || previous != crater)
                _changedGrids.Add(grid);
        }
        foreach (var (uid, previous) in _lastCraters)
        {
            if (visualsChanged || !_currentCraters.TryGetValue(uid, out var crater) || crater != previous)
                _changedGrids.Add(previous.Grid);
        }
        foreach (var grid in _changedGrids)
            RebuildGround(grid, _currentCraters.Values.Where(crater => crater.Grid == grid).ToList(), visuals);
        (_lastCraters, _currentCraters) = (_currentCraters, _lastCraters);
        _lastVisuals = visuals;

        var bodies = EntityQueryEnumerator<StandingStateComponent, SpriteComponent>();
        while (bodies.MoveNext(out var uid, out _, out var sprite))
        {
            var target = TryComp<BattleCraterEntryComponent>(uid, out var entry)
                && entry.Entered && entry.Crater is { } crater && HasComp<BattleScarComponent>(crater)
                && _entry.IsInside(uid, crater)
                ? visuals.BodyDepth : 0f;
            if (_entry.TryGetTrench(uid, out var trench))
                target = MathF.Max(target, trench.BodyDepth);
            _depths.TryGetValue(uid, out var old);
            if (target == old)
                continue;
            sprite.Offset += new Vector2(0, old - target);
            if (target == 0)
                _depths.Remove(uid);
            else
                _depths[uid] = target;
        }
        _removedBodies.Clear();
        foreach (var uid in _depths.Keys)
        {
            if (Deleted(uid))
                _removedBodies.Add(uid);
        }
        foreach (var uid in _removedBodies)
            _depths.Remove(uid);
    }

    public void DrawGround(EntityUid grid, DrawingHandleWorld handle, Box2 worldBounds)
    {
        if (!_mergedGround.TryGetValue(grid, out var strips))
            return;
        var localBounds = _transforms.GetInvWorldMatrix(grid).TransformBox(worldBounds);
        foreach (var strip in strips)
        {
            if (strip.Bounds.Intersects(localBounds))
                handle.DrawRect(strip.Bounds, strip.Color);
        }
    }

    private void RebuildGround(EntityUid grid, List<Crater> craters, BattleCraterVisualsPrototype visuals)
    {
        _mergedGround.Remove(grid);
        foreach (var crater in craters)
        {
            if (TryComp<SpriteComponent>(crater.Uid, out var sprite))
                sprite.Visible = true;
        }
        var visited = new HashSet<EntityUid>();
        foreach (var crater in craters)
        {
            if (!visited.Add(crater.Uid))
                continue;
            var group = new List<Crater> { crater };
            for (var i = 0; i < group.Count; i++)
            {
                foreach (var neighbor in craters)
                {
                    if (visited.Contains(neighbor.Uid))
                        continue;
                    var delta = neighbor.Position - group[i].Position;
                    delta.Y *= visuals.VerticalCompression;
                    var mergeDistance = (neighbor.Radius + group[i].Radius) * visuals.MergeRadiusFactor;
                    if (delta.LengthSquared() > mergeDistance * mergeDistance)
                        continue;
                    visited.Add(neighbor.Uid);
                    group.Add(neighbor);
                }
            }
            if (group.Count < 2)
                continue;
            foreach (var member in group)
            {
                if (TryComp<SpriteComponent>(member.Uid, out var sprite))
                    sprite.Visible = false;
            }
            if (!_mergedGround.TryGetValue(grid, out var strips))
                _mergedGround[grid] = strips = new List<GroundStrip>();
            Rasterize(group, strips, visuals);
        }
    }

    private static void Rasterize(List<Crater> group, List<GroundStrip> strips, BattleCraterVisualsPrototype visuals)
    {
        var cells = new Dictionary<Vector2i, float>();
        foreach (var crater in group)
        {
            var minimum = (crater.Position - new Vector2(crater.Radius)) / PixelSize;
            var maximum = (crater.Position + new Vector2(crater.Radius)) / PixelSize;
            for (var y = (int) MathF.Floor(minimum.Y); y <= (int) MathF.Ceiling(maximum.Y); y++)
            for (var x = (int) MathF.Floor(minimum.X); x <= (int) MathF.Ceiling(maximum.X); x++)
            {
                var delta = (new Vector2(x + 0.5f, y + 0.5f) * PixelSize - crater.Position) / crater.Radius;
                delta.Y *= visuals.VerticalCompression;
                var angle = MathF.Atan2(delta.Y, delta.X);
                var edge = visuals.BaseEdgeRadius + visuals.EdgeRoughness.X * MathF.Sin(angle * 5 + crater.Seed % 31)
                    + visuals.EdgeRoughness.Y * MathF.Sin(angle * 9 + crater.Seed % 17);
                var depth = edge - delta.Length();
                if (depth < 0)
                    continue;
                var index = new Vector2i(x, y);
                cells[index] = MathF.Max(cells.GetValueOrDefault(index), depth);
            }
        }
        foreach (var row in cells.GroupBy(cell => cell.Key.Y))
        {
            var start = int.MinValue;
            var end = 0;
            var shade = -1;
            foreach (var cell in row.OrderBy(cell => cell.Key.X))
            {
                var nextShade = cell.Value > visuals.InteriorDepth ? 0 : cell.Value > visuals.RimDepth ? 1 : 2;
                if (start != int.MinValue && (cell.Key.X != end + 1 || shade != nextShade))
                {
                    AddStrip(start, end, row.Key, shade);
                    start = int.MinValue;
                }
                if (start == int.MinValue)
                    start = cell.Key.X;
                end = cell.Key.X;
                shade = nextShade;
            }
            if (start != int.MinValue)
                AddStrip(start, end, row.Key, shade);
        }
        void AddStrip(int start, int end, int y, int shade)
        {
            var color = shade switch
            {
                0 => visuals.InteriorColor,
                1 => visuals.RimColor,
                _ => visuals.EdgeColor,
            };
            strips.Add(new GroundStrip(new Box2(start * PixelSize, y * PixelSize,
                (end + 1) * PixelSize, (y + 1) * PixelSize), color));
        }
    }

    public override void Shutdown()
    {
        _overlays.RemoveOverlay<BattleCraterOverlay>();
        foreach (var (uid, depth) in _depths)
        {
            if (TryComp<SpriteComponent>(uid, out var sprite))
                sprite.Offset += new Vector2(0, depth);
        }
        _depths.Clear();
        _currentCraters.Clear();
        _lastCraters.Clear();
        _mergedGround.Clear();
        _changedGrids.Clear();
        _removedBodies.Clear();
        _lastVisuals = null;
        base.Shutdown();
    }
}
