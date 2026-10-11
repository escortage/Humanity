using System.Numerics;
using Content.Shared.Barricade;
using Content.Shared.Civ14.CivResearch;
using Content.Shared.Destructible;
using Content.Shared.Explosion;
using Content.Shared.Explosion.Components;
using Content.Shared.Humanity.Visuals;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.Server.Humanity.Visuals;

public sealed partial class BattleLandscapeSystem : EntitySystem
{
    private static readonly EntProtoId RubblePrototype = "HumanityBattleRubble";
    [Dependency] private SharedMapSystem _maps = default!;
    [Dependency] private SharedTransformSystem _transforms = default!;
    [Dependency] private IPrototypeManager _prototypes = default!;
    private readonly HashSet<EntityUid> _explosions = new();
    private readonly Dictionary<EntityUid, Queue<EntityUid>> _scars = new();

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<BarricadeComponent, DestructionEventArgs>(OnDestroyed);
        SubscribeLocalEvent<ExplosionVisualsComponent, ComponentShutdown>(OnExplosionShutdown);
        SubscribeLocalEvent<MapRemovedEvent>(OnMapRemoved);
    }

    private void OnExplosionShutdown(Entity<ExplosionVisualsComponent> ent, ref ComponentShutdown args)
    {
        _explosions.Remove(ent.Owner);
    }

    private void OnMapRemoved(MapRemovedEvent args)
    {
        _scars.Remove(args.Uid);
    }

    private void OnDestroyed(EntityUid uid, BarricadeComponent component, DestructionEventArgs args)
    {
        CreateScar(_transforms.GetMapCoordinates(uid), RubblePrototype);
    }

    private void CreateScar(MapCoordinates origin, EntProtoId prototype, int? tileCount = null)
    {
        if (!_maps.MapExists(origin.MapId))
            return;
        var map = _maps.GetMap(origin.MapId);
        if (!TryComp<CivResearchComponent>(map, out var research) || !research.IsTDM ||
            !_maps.TryFindGridAt(origin, out var grid, out _))
            return;
        var uid = Spawn(prototype, new EntityCoordinates(grid,
            Vector2.Transform(origin.Position, _transforms.GetInvWorldMatrix(grid))));
        var scar = Comp<BattleScarComponent>(uid);
        if (tileCount is { } count)
            scar.Radius = Math.Clamp(count * scar.RadiusPerTile, scar.MinimumRadius, scar.MaximumRadius);
        Dirty(uid, scar);
        if (!_scars.TryGetValue(map, out var marks))
            _scars[map] = marks = new Queue<EntityUid>();
        marks.Enqueue(uid);
        while (marks.Count > 192)
        {
            var oldest = marks.Dequeue();
            if (!Deleted(oldest))
                QueueDel(oldest);
        }
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        var query = EntityQueryEnumerator<ExplosionVisualsComponent>();
        while (query.MoveNext(out var uid, out var explosion))
        {
            if (explosion.Intensity.Count == 0 || !_explosions.Add(uid) ||
                _prototypes.Index<ExplosionPrototype>(explosion.ExplosionType).Crater is not { } prototype)
                continue;
            CreateScar(explosion.Epicenter, prototype, explosion.Intensity.Count);
        }
    }
}
