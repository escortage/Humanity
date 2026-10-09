using Content.Server.GameTicking.Rules.Components;
using Content.Shared.GameTicking;
using Content.Shared.GameTicking.Components;
using Content.Shared.Humanity.Combat;

namespace Content.Server.GameTicking.Rules;

public sealed partial class TeamDeathMatchRuleSystem
{
    private static BattleScoreUpdateEvent GetScore(TeamDeathMatchRuleComponent match) =>
        new(match.Team1, match.Team1Kills, match.Team2, match.Team2Kills);

    private void BroadcastScore(TeamDeathMatchRuleComponent match) => RaiseNetworkEvent(GetScore(match));

    private void OnRequestScore(RequestBattleScoreEvent message, EntitySessionEventArgs args)
    {
        var query = EntityQueryEnumerator<TeamDeathMatchRuleComponent, GameRuleComponent>();
        while (query.MoveNext(out var uid, out var match, out var rule))
        {
            if (!GameTicker.IsGameRuleActive((uid, rule)))
                continue;
            RaiseNetworkEvent(GetScore(match), args.SenderSession);
            return;
        }
        RaiseNetworkEvent(new BattleScoreUpdateEvent(), args.SenderSession);
    }

    private void OnScoreReset(RoundRestartCleanupEvent args) => RaiseNetworkEvent(new BattleScoreUpdateEvent());
}
