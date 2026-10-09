using Content.Shared.DoAfter;
using Content.Shared.Standing;
using Robust.Shared.Prototypes;
using Robust.Shared.Map;
using Robust.Shared.Timing;

namespace Content.Shared.Humanity.Visuals;

public sealed partial class BattleCraterEntrySystem : EntitySystem
{
    private static readonly ProtoId<BattleCraterVisualsPrototype> VisualsPrototype = "HumanityBattleCrater";

    [Dependency] private SharedTransformSystem _transforms = default!;
    [Dependency] private IPrototypeManager _prototypes = default!;
    [Dependency] private SharedDoAfterSystem _doAfter = default!;
    [Dependency] private IGameTiming _timing = default!;

    private bool _moving;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<BattleCraterEntryComponent, BattleCraterEnterDoAfterEvent>(OnEnterCompleted);
        SubscribeLocalEvent<BattleCraterEntryComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<StandingStateComponent, MoveEvent>(OnMove);
    }

    private void OnEnterCompleted(Entity<BattleCraterEntryComponent> ent, ref BattleCraterEnterDoAfterEvent args)
    {
        if (args.Handled || ent.Comp.Action != args.DoAfter.Index)
            return;

        ent.Comp.Action = null;
        if (ent.Comp.Crater is not { } crater || !Exists(crater))
        {
            ent.Comp.Crater = null;
            ent.Comp.Entered = false;
        }
        else if (args.Cancelled)
        {
            if (!ent.Comp.Entered)
                ent.Comp.Crater = null;
        }
        else
            CompleteCrossing(ent);
        Dirty(ent);
        args.Handled = true;
    }

    private void OnShutdown(Entity<BattleCraterEntryComponent> ent, ref ComponentShutdown args)
    {
        if (ent.Comp.Action is { } action)
            _doAfter.Cancel(new DoAfterId(ent, action));
    }

    private void OnMove(Entity<StandingStateComponent> ent, ref MoveEvent args)
    {
        if (_moving || _timing.ApplyingState)
            return;

        if (TryComp<BattleCraterEntryComponent>(ent, out var entry) && entry.Crater is { } current)
        {
            var inside = IsInside(ent, current, true);
            if (entry.Action != null)
            {
                if (inside != entry.Entered)
                    RestorePosition(ent, args.OldPosition);
                return;
            }
            if (entry.Entered && inside)
                return;

            if (entry.Entered && TryComp<BattleScarComponent>(current, out _) && HasComp<DoAfterComponent>(ent))
            {
                StartCrossing((ent, entry), current, args);
                return;
            }
            entry.Crater = null;
            entry.Entered = false;
            Dirty(ent, entry);
        }

        if (!HasComp<DoAfterComponent>(ent) || FindCrater(ent, ent.Comp) is not { } crater)
            return;

        entry = EnsureComp<BattleCraterEntryComponent>(ent);
        StartCrossing((ent, entry), crater, args);
    }

    private void StartCrossing(Entity<BattleCraterEntryComponent> ent, EntityUid crater, MoveEvent args)
    {
        RestorePosition(ent, args.OldPosition);
        var scar = Comp<BattleScarComponent>(crater);
        var delay = ent.Comp.Entered ? scar.ExitDelay : scar.EnterDelay;
        var doAfter = new DoAfterArgs(EntityManager, ent, delay,
            new BattleCraterEnterDoAfterEvent(), ent, target: crater)
        {
            RequireCanInteract = false,
            DistanceThreshold = null,
            BreakOnMove = true,
        };
        if (!_doAfter.TryStartDoAfter(doAfter, out var id))
            return;

        ent.Comp.Crater = crater;
        ent.Comp.Action = _doAfter.IsRunning(id) ? id.Value.Index : null;
        ent.Comp.Destination = args.NewPosition;
        if (ent.Comp.Action == null)
            CompleteCrossing(ent);
        Dirty(ent);
    }

    private void CompleteCrossing(Entity<BattleCraterEntryComponent> ent)
    {
        ent.Comp.Entered = !ent.Comp.Entered;
        if (!ent.Comp.Entered)
            ent.Comp.Crater = null;
        RestorePosition(ent, ent.Comp.Destination);
    }

    private void RestorePosition(EntityUid uid, EntityCoordinates coordinates)
    {
        _moving = true;
        try
        {
            _transforms.SetCoordinates(uid, coordinates);
        }
        finally
        {
            _moving = false;
        }
    }

    public bool IsInside(EntityUid uid, EntityUid crater, bool includeRim = false)
    {
        if (!TryComp<BattleScarComponent>(crater, out var scar) || scar.Rubble || scar.Radius <= 0)
            return false;
        var transform = Transform(uid);
        var craterTransform = Transform(crater);
        if (transform.GridUid == null || craterTransform.GridUid != transform.GridUid)
            return false;
        var offset = _transforms.GetWorldPosition(transform) - _transforms.GetWorldPosition(craterTransform);
        var visuals = _prototypes.Index(VisualsPrototype);
        return visuals.Contains(offset, scar.Radius, includeRim ? visuals.BaseEdgeRadius : null);
    }

    private EntityUid? FindCrater(EntityUid uid, StandingStateComponent standing)
    {
        var transform = Transform(uid);
        if (transform.GridUid == null)
            return null;

        var position = _transforms.GetWorldPosition(transform);
        var visuals = _prototypes.Index(VisualsPrototype);
        var multiplier = 1f;
        EntityUid? result = null;
        var craters = EntityQueryEnumerator<BattleScarComponent, TransformComponent>();
        while (craters.MoveNext(out var crater, out var scar, out var craterTransform))
        {
            if (scar.Rubble || scar.Radius <= 0 || craterTransform.GridUid != transform.GridUid)
                continue;

            if (!visuals.Contains(position - _transforms.GetWorldPosition(craterTransform), scar.Radius, visuals.BaseEdgeRadius))
                continue;

            var protection = standing.Standing
                ? scar.ExplosionDamageMultiplier
                : scar.ProneExplosionDamageMultiplier;
            protection = Math.Clamp(protection, 0f, 1f);
            if (protection < multiplier)
            {
                multiplier = protection;
                result = crater;
            }
        }

        return result;
    }
}
