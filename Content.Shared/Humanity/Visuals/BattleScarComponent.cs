using Robust.Shared.GameStates;

namespace Content.Shared.Humanity.Visuals;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class BattleScarComponent : Component
{
    [DataField, AutoNetworkedField]
    public float Radius = 0.7f;

    [DataField]
    public float RadiusPerTile = 0.16f;

    [DataField]
    public float MinimumRadius = 0.45f;

    [DataField]
    public float MaximumRadius = 1.8f;

    [DataField, AutoNetworkedField]
    public int Seed;

    [DataField, AutoNetworkedField]
    public bool Rubble;

    [DataField]
    public float ExplosionDamageMultiplier = 1f;

    [DataField]
    public float ProneExplosionDamageMultiplier = 1f;

    [DataField]
    public TimeSpan EnterDelay = TimeSpan.FromSeconds(1);

    [DataField]
    public TimeSpan ExitDelay = TimeSpan.FromSeconds(1);
}
