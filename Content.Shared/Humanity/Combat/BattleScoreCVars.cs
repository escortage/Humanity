using Robust.Shared.Configuration;

namespace Content.Shared.Humanity.Combat;

[CVarDefs]
public static class BattleScoreCVars
{
    public static readonly CVarDef<bool> Collapsed = CVarDef.Create("hud.battle_score_collapsed", false, CVar.CLIENTONLY | CVar.ARCHIVE);
}
