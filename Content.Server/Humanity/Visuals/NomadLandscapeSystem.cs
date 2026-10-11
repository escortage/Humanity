using Content.Server.Construction;
using Content.Server.Construction.Components;
using Content.Shared.Civ14.CivResearch;
using Content.Shared.Humanity.Visuals;
using Content.Shared.Light.Components;
using Content.Shared.Damage;
using Content.Shared.TreeBranch;
using Content.Shared.Gatherable.Components;
using Robust.Shared.Map.Components;
using Robust.Shared.Player;

namespace Content.Server.Humanity.Visuals;

public sealed class NomadLandscapeSystem : EntitySystem
{
    private readonly HashSet<EntityUid> _configured = new();
    private float _elapsed;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<ConstructionComponent, ConstructionChangeEntityEvent>(OnBuilt);
        SubscribeLocalEvent<TreeBranchesComponent, DamageChangedEvent>(OnChop);
        SubscribeLocalEvent<GatherableComponent, NomadGatherEffectEvent>(OnGatherEffect);
    }

    private void OnGatherEffect(Entity<GatherableComponent> gathered, ref NomadGatherEffectEvent args)
    {
        Emit(gathered, args.Effect);
    }

    private void OnBuilt(EntityUid uid, ConstructionComponent component, ConstructionChangeEntityEvent args)
    {
        if (uid == args.Old)
            Emit(uid, NomadWorkEffect.Building);
    }

    private void OnChop(EntityUid uid, TreeBranchesComponent component, DamageChangedEvent args)
    {
        if (args.DamageIncreased)
            Emit(uid, NomadWorkEffect.Wood);
    }

    public void Emit(EntityUid uid, NomadWorkEffect effect)
    {
        var xform = Transform(uid);
        if (!TryComp<CivResearchComponent>(xform.MapUid, out var research) || research.IsTDM)
            return;
        RaiseNetworkEvent(new NomadWorkEffectEvent(GetNetCoordinates(xform.Coordinates), effect),
            Filter.Pvs(uid, entityManager: EntityManager));
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        _elapsed += frameTime;
        if (_elapsed < 2f)
            return;
        _elapsed = 0;
        _configured.RemoveWhere(uid => Deleted(uid));
        var maps = EntityQueryEnumerator<CivResearchComponent, MapLightComponent>();
        while (maps.MoveNext(out var uid, out var research, out var light))
        {
            if (research.IsTDM || !_configured.Add(uid))
                continue;
            var existing = TryComp<LightCycleComponent>(uid, out var cycle);
            cycle ??= AddComp<LightCycleComponent>(uid);
            if (!existing)
            {
                cycle.OriginalColor = light.AmbientLightColor;
                cycle.Offset = TimeSpan.FromMinutes(10);
            }
            cycle.Enabled = true;
            cycle.Duration = TimeSpan.FromMinutes(40);
            cycle.MinLightLevel = 0.65f;
            cycle.MaxLightLevel = 1.1f;
            cycle.ClipLight = 1f;
            cycle.MinLevel = new Color(0.72f, 0.82f, 1f);
            cycle.MaxLevel = new Color(1.25f, 1.15f, 1.2f);
            cycle.ClipLevel = Color.White;
            Dirty(uid, cycle);
        }
    }
}
