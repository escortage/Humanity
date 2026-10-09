using Content.Shared.Humanity.Combat;

namespace Content.Client.Humanity.Combat;

public sealed partial class BattleScoreSystem : EntitySystem
{
    public BattleScoreUpdateEvent? Score { get; private set; }
    public event Action? ScoreChanged;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeNetworkEvent<BattleScoreUpdateEvent>(OnScore);
    }

    private void OnScore(BattleScoreUpdateEvent score)
    {
        Score = score;
        ScoreChanged?.Invoke();
    }

    public void RequestScore() => RaiseNetworkEvent(new RequestBattleScoreEvent());
}
