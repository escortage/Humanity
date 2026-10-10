using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared.Humanity.Audio;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class WorldWarAmbienceComponent : Component
{
    [DataField(required: true), AutoNetworkedField]
    public ProtoId<WorldWarAmbiencePrototype> Soundscape;
}
