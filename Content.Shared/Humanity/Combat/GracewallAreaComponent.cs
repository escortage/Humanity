using Robust.Shared.GameStates;

namespace Content.Shared.Humanity.Combat;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class GracewallAreaComponent : Component
{
    [DataField]
    public float GracewallRadius = 1.5f;

    [DataField, AutoNetworkedField]
    public bool GracewallActive = true;

    [DataField, AutoNetworkedField]
    public List<string> BlockingFactions = new() { "All" };

    [DataField]
    public bool Permanent;
}
