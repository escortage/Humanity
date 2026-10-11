using Content.Server.GameTicking.Rules.Components;
using Content.Shared.Humanity.Combat;
using Content.Shared.Physics;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Timing;
using Content.Server.Chat.Systems;
using Robust.Shared.Physics;
using Content.Shared.GameTicking.Components;
using Robust.Shared.Physics.Components;

namespace Content.Server.GameTicking.Rules;

public sealed partial class GracewallRuleSystem : GameRuleSystem<GracewallRuleComponent>
{
    [Dependency] private SharedPhysicsSystem _physics = default!;
    [Dependency] private ChatSystem _chat = default!;

    private const int GraceWallCollisionGroup = (int)CollisionGroup.MidImpassable;

    protected override void Started(EntityUid uid, GracewallRuleComponent component, GameRuleComponent gameRule, GameRuleStartedEvent args)
    {
        base.Started(uid, component, gameRule, args);

        component.Timer = (float)component.GracewallDuration.TotalSeconds;
        component.GracewallActive = true;

        // Schedule the announcement for 15 seconds later
        var announcementMessage = "До начала боя три минуты.";
        Timer.Spawn(TimeSpan.FromSeconds(15), () =>
        {
            if (component.GracewallActive && Exists(uid) && GameTicker.IsGameRuleActive((uid, gameRule)))
                _chat.DispatchGlobalAnnouncement(announcementMessage, "Штаб", false, null, Color.Yellow);
        });
        Log.Info($"Grace wall active for {component.GracewallDuration.TotalMinutes} minutes.");

        // Activate all grace wall areas
        var query = EntityQueryEnumerator<GracewallAreaComponent, FixturesComponent>();
        while (query.MoveNext(out var wallUid, out var area, out var fixtures))
        {
            area.GracewallActive = true;
            Dirty(wallUid, area);
            UpdateGracewallPhysics(wallUid, area, fixtures, true);
        }
    }

    protected override void Ended(Entity<GracewallRuleComponent> rule, ref GameRuleEndedEvent args)
    {
        base.Ended(rule, ref args);
        var component = rule.Comp;

        // Ensure walls are deactivated if the rule ends unexpectedly
        var wasActive = component.GracewallActive;
        DeactivateAllGraceWalls(component);
        if (wasActive && GameTicker.RunLevel == GameRunLevel.InRound)
            _chat.DispatchGlobalAnnouncement("Проход открыт. Бой начался!", "Штаб", false, null, Color.Yellow);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<GracewallRuleComponent, GameRuleComponent>();
        while (query.MoveNext(out var ruleUid, out var gracewall, out var gameRule))
        {
            if (!GameTicker.IsGameRuleActive((ruleUid, gameRule)) || !gracewall.GracewallActive)
                continue;

            gracewall.Timer -= frameTime;

            if (gracewall.Timer <= 0)
            {
                Log.Info("Grace wall duration ended.");
                DeactivateAllGraceWalls(gracewall);
                _chat.DispatchGlobalAnnouncement("Проход открыт. Бой начался!", "Штаб", false, null, Color.Yellow);
            }
        }
    }

    public bool TryStartBattle()
    {
        if (GameTicker.RunLevel != GameRunLevel.InRound)
            return false;

        var query = EntityQueryEnumerator<GracewallRuleComponent, GameRuleComponent>();
        while (query.MoveNext(out var uid, out var gracewall, out var rule))
        {
            if (gracewall.GracewallActive && GameTicker.IsGameRuleActive((uid, rule)))
                return GameTicker.EndGameRule((uid, rule));
        }
        return false;
    }

    private void DeactivateAllGraceWalls(GracewallRuleComponent component)
    {
        component.GracewallActive = false;

        // Deactivate all grace wall areas
        var query = EntityQueryEnumerator<GracewallAreaComponent, FixturesComponent>();
        while (query.MoveNext(out var wallUid, out var area, out var fixtures))
        {
            if (area.Permanent == false)
            {
                area.GracewallActive = false;
                UpdateGracewallPhysics(wallUid, area, fixtures, false);
                Dirty(wallUid, area);
                QueueDel(wallUid);
            }
        }
    }

    private void UpdateGracewallPhysics(EntityUid uid, GracewallAreaComponent component, FixturesComponent fixtures, bool active)
    {
        // Check if the specific fixture we defined in the prototype exists
        if (!fixtures.Fixtures.TryGetValue("gracewall", out var fixture))
        {
            Log.Warning($"Gracewall entity {ToPrettyString(uid)} is missing the 'gracewall' fixture!");
            return;
        }

        // Modify the fixture's collision properties
        _physics.SetCollisionLayer(uid, "gracewall", fixture, active ? GraceWallCollisionGroup : (int)CollisionGroup.None);
        _physics.SetCollisionMask(uid, "gracewall", fixture, active ? (int)(CollisionGroup.LowImpassable | CollisionGroup.MidImpassable | CollisionGroup.HighImpassable) : (int)CollisionGroup.None);

        // Ensure the change takes effect immediately
        if (TryComp<PhysicsComponent>(uid, out var physics))
            _physics.WakeBody(uid, body: physics);
    }

}
