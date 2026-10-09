using Content.Shared.Humanity.Combat;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Configuration;

namespace Content.Client.Humanity.Combat;

public sealed partial class BattleScoreControl : UIWidget
{
    [Dependency] private IConfigurationManager _cfg = default!;
    private readonly Label _score = new();
    private readonly Button _toggle = new();

    public BattleScoreControl()
    {
        IoCManager.InjectDependencies(this);
        Visible = false;
        var row = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Horizontal, SeparationOverride = 8 };
        row.AddChild(_score);
        row.AddChild(_toggle);
        var panel = new PanelContainer();
        panel.AddChild(row);
        AddChild(panel);
        _toggle.ToolTip = Loc.GetString("humanity-battle-score-toggle");
        _toggle.OnPressed += _ =>
        {
            _cfg.SetCVar(BattleScoreCVars.Collapsed, !_cfg.GetCVar(BattleScoreCVars.Collapsed));
            RefreshVisibility();
        };
        RefreshVisibility();
    }

    public void Refresh(BattleScoreUpdateEvent? score)
    {
        Visible = score != null && score.Team1 != "" && score.Team2 != "";
        if (score == null)
            return;
        _score.Text = $"{BattleFactionNames.Get(score.Team1)} {score.Team1Kills} : {score.Team2Kills} {BattleFactionNames.Get(score.Team2)}";
    }

    private void RefreshVisibility()
    {
        var collapsed = _cfg.GetCVar(BattleScoreCVars.Collapsed);
        _score.Visible = !collapsed;
        _toggle.Text = collapsed ? Loc.GetString("humanity-battle-score-show") : "−";
    }
}
