using Content.Shared.Explosion;
using Content.Shared.Humanity.Combat;
using Content.Shared.Humanity.Visuals;
using Content.Shared.Projectiles;
using Content.Shared.Standing;

namespace Content.Server.Humanity.Visuals;

public sealed partial class BattleCraterProtectionSystem : EntitySystem
{
    [Dependency] private BattleCraterEntrySystem _entry = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<StandingStateComponent, GetExplosionResistanceEvent>(OnExplosionResistance);
        SubscribeLocalEvent<ShrapnelComponent, BeforeProjectileHitEvent>(OnShrapnelHit);
    }

    private void OnExplosionResistance(Entity<StandingStateComponent> ent, ref GetExplosionResistanceEvent args)
    {
        args.DamageCoefficient *= GetDamageMultiplier(ent, ent.Comp);
    }

    private void OnShrapnelHit(Entity<ShrapnelComponent> ent, ref BeforeProjectileHitEvent args)
    {
        if (TryComp<StandingStateComponent>(args.Target, out var standing))
            args.Damage *= GetDamageMultiplier(args.Target, standing);
    }

    private float GetDamageMultiplier(EntityUid uid, StandingStateComponent standing)
    {
        if (!TryComp<BattleCraterEntryComponent>(uid, out var entry)
            || !entry.Entered || entry.Crater is not { } crater || !_entry.IsInside(uid, crater))
            return 1f;

        var scar = Comp<BattleScarComponent>(crater);
        return Math.Clamp(standing.Standing ? scar.ExplosionDamageMultiplier : scar.ProneExplosionDamageMultiplier, 0f, 1f);
    }
}
