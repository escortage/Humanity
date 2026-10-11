using Robust.Shared.GameStates;

namespace Content.Shared.Humanity.Visuals;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class BattleTrenchComponent : Component
{
    [DataField, AutoNetworkedField]
    public HashSet<Vector2i> Tiles = new();

    [DataField]
    public float BodyDepth;

    [DataField]
    public TimeSpan EnterDelay = TimeSpan.FromSeconds(1);

    [DataField]
    public float ExplosionDamageMultiplier = 1f;

    [DataField]
    public float ProneExplosionDamageMultiplier = 1f;
}
