namespace Content.Server.GameTicking.Rules.Components;

[RegisterComponent, Access(typeof(GracewallRuleSystem))]
public sealed partial class GracewallRuleComponent : Component
{
    /// <summary>
    /// How long the grace wall lasts since the round started
    /// </summary>
    [DataField("gracewallDuration")]
    public TimeSpan GracewallDuration { get; set; } = TimeSpan.FromMinutes(3);
    /// <summary>
    /// Is the grace wall currently active?
    /// </summary>
    [DataField("gracewallActive")]
    public bool GracewallActive { get; set; } = true;
    /// <summary>
    /// How much time is remaining until the grace wall drops.
    /// </summary>
    public float Timer;


}
