using Robust.Shared.Serialization;

namespace Content.Shared.Humanity.Combat;

[Serializable, NetSerializable]
public sealed class RequestBattleScoreEvent : EntityEventArgs;

[Serializable, NetSerializable]
public sealed class BattleScoreUpdateEvent(string team1 = "", int team1Kills = 0, string team2 = "", int team2Kills = 0) : EntityEventArgs
{
    public readonly string Team1 = team1;
    public readonly int Team1Kills = team1Kills;
    public readonly string Team2 = team2;
    public readonly int Team2Kills = team2Kills;
}
