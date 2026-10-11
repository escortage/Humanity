using System.Numerics;
using System.Linq;
using Content.Shared.Civ14.CivResearch;
using Content.Shared.Humanity.Visuals;
using Content.Shared.Mobs.Components;
using Robust.Client.Graphics;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Random;

namespace Content.Client.Humanity.Visuals;

public sealed partial class NomadLandscapeSystem : EntitySystem
{
    [Dependency] private IOverlayManager _overlays = default!;
    [Dependency] private SharedTransformSystem _transforms = default!;
    [Dependency] private SharedMapSystem _map = default!;
    [Dependency] private ITileDefinitionManager _tiles = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private HumanityAtmosphereSystem _atmosphere = default!;
    [Dependency] private FoliageAtmosphereSystem _foliage = default!;
    private readonly Dictionary<EntityUid, MapCoordinates> _positions = new();
    internal readonly List<LandscapeMark> Marks = new();
    private readonly HashSet<(EntityUid Grid, Vector2i Tile)> _water = new();
    private float _elapsed;

    internal sealed class LandscapeMark
    {
        public MapCoordinates Coordinates;
        public Vector2 Direction;
        public float Age;
        public float Lifetime;
        public Color Color;
        public bool Ripple;
        public bool Debris;
    }

    public override void Initialize()
    {
        base.Initialize();
        _overlays.AddOverlay(new NomadLandscapeOverlay(this) { ZIndex = 110 });
        SubscribeAllEvent<NomadWorkEffectEvent>(OnWork);
    }

    public override void Shutdown()
    {
        _overlays.RemoveOverlay<NomadLandscapeOverlay>();
        _positions.Clear();
        Marks.Clear();
        _water.Clear();
        base.Shutdown();
    }

    private void Add(LandscapeMark mark)
    {
        if (Marks.Count >= 384)
            Marks.RemoveAt(0);
        Marks.Add(mark);
    }

    public bool IsWater(EntityUid grid, Vector2i tile) => _water.Contains((grid, tile));

    private void UpdateWater()
    {
        _water.Clear();
        var water = EntityQueryEnumerator<NomadWaterSurfaceComponent, TransformComponent>();
        while (water.MoveNext(out _, out _, out var xform))
        {
            if (xform.GridUid is { } grid && TryComp<MapGridComponent>(grid, out var gridComp) &&
                _map.TryGetTileRef(grid, gridComp, xform.Coordinates, out var tile))
                _water.Add((grid, tile.GridIndices));
        }
    }

    private void OnWork(NomadWorkEffectEvent ev)
    {
        var coordinates = GetCoordinates(ev.Coordinates);
        if (Deleted(coordinates.EntityId))
            return;
        var origin = _transforms.ToMapCoordinates(coordinates);
        _foliage.ShakeAt(origin);
        if (ev.Effect == NomadWorkEffect.Shake)
            return;
        if (ev.Effect == NomadWorkEffect.Building)
            _atmosphere.EmitWorkDust(origin);
        for (var i = 0; i < 8; i++)
        {
            Add(new LandscapeMark
            {
                Coordinates = origin,
                Direction = _random.NextVector2(0.8f) + new Vector2(0, 0.3f),
                Lifetime = _random.NextFloat(0.8f, 1.4f),
                Debris = true,
                Color = ev.Effect switch
                {
                    NomadWorkEffect.Wood => Color.FromHex("#B99460"),
                    NomadWorkEffect.Leaves => Color.FromHex("#829353"),
                    _ => Color.FromHex("#A89C80"),
                },
            });
        }
    }

    public override void FrameUpdate(float frameTime)
    {
        base.FrameUpdate(frameTime);
        for (var i = Marks.Count - 1; i >= 0; i--)
        {
            Marks[i].Age += frameTime;
            if (Marks[i].Age >= Marks[i].Lifetime)
                Marks.RemoveAt(i);
        }
        _elapsed += frameTime;
        if (_elapsed < 0.2f)
            return;
        _elapsed = 0;
        UpdateWater();
        foreach (var uid in _positions.Keys.ToArray())
        {
            if (Deleted(uid))
                _positions.Remove(uid);
        }
        var query = EntityQueryEnumerator<MobStateComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out _, out var xform))
        {
            if (!TryComp<CivResearchComponent>(xform.MapUid, out var research) || research.IsTDM)
                continue;
            var coordinates = _transforms.GetMapCoordinates(uid);
            if (!_positions.TryGetValue(uid, out var previous) || previous.MapId != coordinates.MapId)
            {
                _positions[uid] = coordinates;
                continue;
            }
            var delta = coordinates.Position - previous.Position;
            if (delta.LengthSquared() < 0.16f)
                continue;
            _positions[uid] = coordinates;
            if (delta.LengthSquared() > 9f || xform.GridUid is not { } grid ||
                !TryComp<MapGridComponent>(grid, out var gridComp) ||
                !_map.TryGetTileRef(grid, gridComp, xform.Coordinates, out var tile))
                continue;
            var id = _tiles[tile.Tile.TypeId].ID;
            var water = IsWater(grid, tile.GridIndices) || id.Contains("Water", StringComparison.OrdinalIgnoreCase);
            if (!water && !id.Contains("Dirt", StringComparison.Ordinal) && !id.Contains("Grass", StringComparison.Ordinal))
                continue;
            Add(new LandscapeMark
            {
                Coordinates = coordinates,
                Direction = Vector2.Normalize(delta),
                Lifetime = water ? 1.2f : 90f,
                Ripple = water,
                Color = water ? new Color(0.65f, 0.82f, 0.87f, 0.65f) : new Color(0.26f, 0.22f, 0.13f, 0.22f),
            });
            if (water)
            {
                for (var i = 0; i < 4; i++)
                {
                    Add(new LandscapeMark
                    {
                        Coordinates = coordinates,
                        Direction = _random.NextVector2(0.55f) + new Vector2(0, 0.6f),
                        Lifetime = 0.55f,
                        Debris = true,
                        Color = new Color(0.65f, 0.85f, 0.95f, 0.8f),
                    });
                }
            }
        }
    }
}
