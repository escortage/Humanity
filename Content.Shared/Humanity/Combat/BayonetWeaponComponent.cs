using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared.Humanity.Combat;

[RegisterComponent, NetworkedComponent]
public sealed partial class BayonetWeaponComponent : Component
{
    [DataField]
    public string Slot = "bayonet";

    [DataField]
    public string Prefix = "bayonet";
}

[Serializable, NetSerializable]
public enum BayonetVisuals : byte
{
    Attached,
}
