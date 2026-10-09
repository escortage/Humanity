using Content.Client.Gameplay;
using Content.Client.UserInterface.Systems.Gameplay;
using Robust.Client.UserInterface.Controllers;

namespace Content.Client.Humanity.Combat;

public sealed partial class BattleScoreUIController : UIController, IOnStateEntered<GameplayState>, IOnSystemChanged<BattleScoreSystem>
{
    [Dependency] private GameplayStateLoadController _load = default!;
    private BattleScoreSystem? _score;

    public override void Initialize()
    {
        base.Initialize();
        _load.OnScreenLoad += Refresh;
    }

    public void OnSystemLoaded(BattleScoreSystem system)
    {
        _score = system;
        system.ScoreChanged += Refresh;
    }

    public void OnSystemUnloaded(BattleScoreSystem system)
    {
        system.ScoreChanged -= Refresh;
        _score = null;
        Refresh();
    }

    public void OnStateEntered(GameplayState state) => _score?.RequestScore();

    private void Refresh() => UIManager.GetActiveUIWidgetOrNull<BattleScoreControl>()?.Refresh(_score?.Score);
}
