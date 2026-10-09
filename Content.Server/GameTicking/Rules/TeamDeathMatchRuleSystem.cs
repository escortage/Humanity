using System.Linq;
using Content.Server.Chat.Systems;
using Content.Server.GameTicking.Rules.Components;
using Content.Server.KillTracking;
using Content.Server.Overlays;
using Content.Server.RoundEnd;
using Content.Shared.Civ14.CivTDMFactions;
using Content.Shared.Alert;
using Content.Shared.Movement.Systems;
using Content.Shared.Nutrition.Components;
using Content.Shared.GameTicking;
using Content.Shared.GameTicking.Components;
using Content.Shared.Humanity.Combat;
using Content.Shared.Mobs;
using Content.Shared.NPC.Components;
using Content.Shared.Overlays;
using Robust.Server.Player;
using Robust.Shared.Utility;

namespace Content.Server.GameTicking.Rules;

public sealed partial class TeamDeathMatchRuleSystem : GameRuleSystem<TeamDeathMatchRuleComponent>
{
    [Dependency] private IPlayerManager _players = default!;
    [Dependency] private KillTrackingSystem _kills = default!;
    [Dependency] private FactionIconsSystem _factionIcons = default!;
    [Dependency] private RoundEndSystem _roundEnd = default!;
    [Dependency] private ChatSystem _chat = default!;
    [Dependency] private AlertsSystem _alerts = default!;
    [Dependency] private MovementSpeedModifierSystem _movement = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<PlayerSpawnCompleteEvent>(OnSpawnComplete);
        SubscribeLocalEvent<KillReportedEvent>(OnKillReported);
        SubscribeNetworkEvent<RequestBattleScoreEvent>(OnRequestScore);
        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnScoreReset);
    }

    protected override void Started(EntityUid uid, TeamDeathMatchRuleComponent component, GameRuleComponent rule, GameRuleStartedEvent args)
    {
        component.Elapsed = 0;
        if (component.DisableNutrition)
        {
            foreach (var session in _players.Sessions)
            {
                if (session.AttachedEntity is { } body)
                    DisableNutrition(body);
            }
        }
        if (component.RoundDuration > 0)
            _chat.DispatchGlobalAnnouncement(Loc.GetString("humanity-ww2-objective", ("minutes", component.RoundDuration / 60)), "Штаб", false, null, Color.Yellow);
        BroadcastScore(component);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        if (GameTicker.RunLevel != GameRunLevel.InRound)
            return;
        var query = EntityQueryEnumerator<TeamDeathMatchRuleComponent, GameRuleComponent>();
        while (query.MoveNext(out var uid, out var match, out var rule))
        {
            if (!GameTicker.IsGameRuleActive((uid, rule)) || match.RoundDuration <= 0 || match.TimedOut)
                continue;
            match.Elapsed += frameTime;
            if (match.Elapsed < match.RoundDuration)
                continue;
            match.TimedOut = true;
            match.WinnerTeam = match.Team1Kills > match.Team2Kills ? match.Team1 :
                match.Team2Kills > match.Team1Kills ? match.Team2 : "";
            var message = match.WinnerTeam == ""
                ? "Время боя истекло. Равный счёт — ничья."
                : $"Время боя истекло. По числу убийств противника побеждает {BattleFactionNames.Get(match.WinnerTeam)}.";
            _chat.DispatchGlobalAnnouncement(message, "Штаб", false, null, Color.Yellow);
            _roundEnd.EndRound();
            return;
        }
    }

    public void SetWinner(string faction)
    {
        var query = EntityQueryEnumerator<TeamDeathMatchRuleComponent, GameRuleComponent>();
        while (query.MoveNext(out var uid, out var match, out var rule))
        {
            if (GameTicker.IsGameRuleActive((uid, rule)))
                match.WinnerTeam = faction;
        }
    }

    private void OnSpawnComplete(PlayerSpawnCompleteEvent args)
    {
        var nutritionRules = EntityQueryEnumerator<TeamDeathMatchRuleComponent, GameRuleComponent>();
        while (nutritionRules.MoveNext(out var ruleUid, out var nutritionRule, out var gameRule))
        {
            if (!nutritionRule.DisableNutrition || !GameTicker.IsGameRuleAdded((ruleUid, gameRule)))
                continue;
            DisableNutrition(args.Mob);
            break;
        }
        if (!TryComp<NpcFactionMemberComponent>(args.Mob, out var faction))
            return;
        var team = faction.Factions.FirstOrDefault(id => id != "UnitedNations");
        if (team == default)
            return;
        var query = EntityQueryEnumerator<TeamDeathMatchRuleComponent, GameRuleComponent>();
        while (query.MoveNext(out var uid, out var match, out var rule))
        {
            if (!GameTicker.IsGameRuleActive((uid, rule)))
                continue;
            if (match.Team1 == "")
                match.Team1 = team;
            else if (match.Team2 == "" && match.Team1 != team)
                match.Team2 = team;
            if (team != match.Team1 && team != match.Team2)
                continue;
            _kills.SetKillState(args.Mob, MobState.Dead);
            var user = args.Player.UserId.ToString();
            match.Participants[args.Mob] = user;
            if (!match.KDRatio.TryGetValue(user, out var stats))
                match.KDRatio[user] = stats = new PlayerKDStats();
            stats.Name = args.Player.Name;
            stats.Team = team;
            BroadcastScore(match);
        }
    }

    private void DisableNutrition(EntityUid body)
    {
        RemComp<Content.Shared.Nutrition.Components.SatiationComponent>(body);
        _alerts.ClearAlertCategory(body, "Hunger");
        _alerts.ClearAlertCategory(body, "Thirst");
        _movement.RefreshMovementSpeedModifiers(body);
    }

    private string GetTeam(EntityUid entity, TeamDeathMatchRuleComponent match)
    {
        if (!TryComp<NpcFactionMemberComponent>(entity, out var faction))
            return "";
        if (match.Team1 != "" && faction.Factions.Any(id => id == match.Team1))
            return match.Team1;
        if (match.Team2 != "" && faction.Factions.Any(id => id == match.Team2))
            return match.Team2;
        return "";
    }

    private void OnKillReported(ref KillReportedEvent args)
    {
        var query = EntityQueryEnumerator<TeamDeathMatchRuleComponent, GameRuleComponent>();
        while (query.MoveNext(out var uid, out var match, out var rule))
        {
            if (!GameTicker.IsGameRuleActive((uid, rule)))
                continue;
            var victimTeam = GetTeam(args.Entity, match);
            if (victimTeam == "")
                continue;
            if (victimTeam == match.Team1)
                match.Team1Deaths++;
            else
                match.Team2Deaths++;

            var victimId = match.Participants.GetValueOrDefault(args.Entity);
            if (victimId != null && match.KDRatio.TryGetValue(victimId, out var victim))
                victim.Deaths++;

            var killerTeam = "";
            PlayerKDStats? killerStats = null;
            if (!args.Suicide)
            {
                switch (args.Primary)
                {
                    case KillPlayerSource player when player.PlayerId.ToString() != victimId:
                        if (match.KDRatio.TryGetValue(player.PlayerId.ToString(), out killerStats))
                            killerTeam = killerStats.Team;
                        else if (_players.TryGetSessionById(player.PlayerId, out var session) && session.AttachedEntity is { } body)
                        {
                            killerTeam = GetTeam(body, match);
                            if (killerTeam != "")
                                match.KDRatio[player.PlayerId.ToString()] = killerStats = new PlayerKDStats { Name = session.Name, Team = killerTeam };
                        }
                        break;
                    case KillNpcSource npc when npc.NpcEnt != args.Entity:
                        killerTeam = GetTeam(npc.NpcEnt, match);
                        break;
                }
            }
            if (killerTeam != "" && killerTeam != victimTeam && (killerTeam == match.Team1 || killerTeam == match.Team2))
            {
                if (killerTeam == match.Team1)
                    match.Team1Kills++;
                else
                    match.Team2Kills++;
                if (killerStats != null)
                    killerStats.Kills++;
            }

            if (TryComp<ShowFactionIconsComponent>(args.Entity, out var icons) && icons.AssignedSquadNameKey != null)
            {
                var factions = EntityQueryEnumerator<CivTDMFactionsComponent>();
                if (factions.MoveNext(out _, out var data))
                    _factionIcons.RecalculateAllCivFactionSquadCounts(data);
            }
            BroadcastScore(match);
        }
    }

    protected override void AppendRoundEndText(Entity<TeamDeathMatchRuleComponent> rule, ref RoundEndTextAppendEvent args)
    {
        var match = rule.Comp;
        if (match.WinnerTeam != "")
            args.AddLine($"Победитель: [color=lime]{BattleFactionNames.Get(match.WinnerTeam)}[/color].");
        else if (match.TimedOut)
            args.AddLine("Бой завершён вничью.");
        args.AddLine($"{BattleFactionNames.Get(match.Team1)}: убийств противника — {match.Team1Kills}, смертей — {match.Team1Deaths}.");
        args.AddLine($"{BattleFactionNames.Get(match.Team2)}: убийств противника — {match.Team2Kills}, смертей — {match.Team2Deaths}.");
        args.AddLine("");
        args.AddLine("[color=yellow]Статистика игроков[/color]");
        foreach (var team in new[] { match.Team1, match.Team2 })
        {
            args.AddLine($"[color=cyan]{BattleFactionNames.Get(team)}[/color]:");
            foreach (var player in match.KDRatio.Values.Where(player => player.Team == team).OrderByDescending(player => player.KDRatio).ThenByDescending(player => player.Kills))
                args.AddLine($"  {FormattedMessage.EscapeText(player.Name)}: убийств — {player.Kills}, смертей — {player.Deaths}, К/С — {player.KDRatio:F2}.");
        }
    }
}
