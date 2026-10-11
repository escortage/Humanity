using Content.Shared.NPC.Components;
using Robust.Shared.Physics.Events;

namespace Content.Shared.Humanity.Combat;

public sealed partial class GracewallCollisionSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<GracewallAreaComponent, PreventCollideEvent>(OnPreventCollide);
    }

    private void OnPreventCollide(Entity<GracewallAreaComponent> ent, ref PreventCollideEvent args)
    {
        if (!ent.Comp.GracewallActive)
        {
            args.Cancelled = true;
            return;
        }

        if (ent.Comp.BlockingFactions.Contains("All"))
            return;

        if (TryComp<NpcFactionMemberComponent>(args.OtherEntity, out var factions))
        {
            foreach (var faction in factions.Factions)
            {
                if (ent.Comp.BlockingFactions.Contains(faction.Id))
                    return;
            }
        }
        args.Cancelled = true;
    }
}
