using Robust.Shared.GameStates;
using Robust.Shared.Map;

namespace Content.Shared.Humanity.Visuals;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class BattleCraterEntryComponent : Component
{
    [DataField, AutoNetworkedField]
    public EntityUid? Crater;

    [DataField, AutoNetworkedField]
    public bool Entered;

    [DataField, AutoNetworkedField]
    public ushort? Action;

    [DataField, AutoNetworkedField]
    public EntityCoordinates Destination;
}
