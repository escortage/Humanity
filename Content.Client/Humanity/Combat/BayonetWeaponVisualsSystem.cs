using Content.Client.Weapons.Ranged.Components;
using Content.Client.Weapons.Ranged.Systems;
using Content.Shared.Humanity.Combat;
using Content.Shared.Weapons.Ranged.Systems;
using Robust.Client.GameObjects;

namespace Content.Client.Humanity.Combat;

public sealed partial class BayonetWeaponVisualsSystem : EntitySystem
{
    [Dependency] private SpriteSystem _sprite = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<BayonetWeaponComponent, AppearanceChangeEvent>(OnAppearance, after: new[] { typeof(GunSystem) });
    }

    private void OnAppearance(Entity<BayonetWeaponComponent> ent, ref AppearanceChangeEvent args)
    {
        if (args.Sprite == null || !_sprite.LayerMapTryGet((ent, args.Sprite), GunVisualLayers.Base, out var layer, false))
            return;

        var state = args.TryGetData(AmmoVisuals.BoltClosed, out bool closed) && !closed ? "bolt-open" : "icon";
        if (args.TryGetData(BayonetVisuals.Attached, out bool attached) && attached)
            state = $"{ent.Comp.Prefix}-{state}";

        _sprite.LayerSetRsiState((ent, args.Sprite), layer, state);
    }
}
