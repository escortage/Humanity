using Content.Shared.Clothing.EntitySystems;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.Item;
using Content.Shared.Weapons.Melee;
using Content.Shared.Weapons.Melee.Events;
using Content.Shared.Wieldable;
using Robust.Shared.Containers;

namespace Content.Shared.Humanity.Combat;

public sealed partial class BayonetWeaponSystem : EntitySystem
{
    [Dependency] private ItemSlotsSystem _slots = default!;
    [Dependency] private SharedItemSystem _items = default!;
    [Dependency] private ClothingSystem _clothing = default!;
    [Dependency] private SharedAppearanceSystem _appearance = default!;
    [Dependency] private SharedMeleeWeaponSystem _melee = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<BayonetWeaponComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<BayonetWeaponComponent, EntInsertedIntoContainerMessage>(OnInserted);
        SubscribeLocalEvent<BayonetWeaponComponent, EntRemovedFromContainerMessage>(OnRemoved);
        SubscribeLocalEvent<BayonetWeaponComponent, ItemWieldedEvent>(OnWielded, after: new[] { typeof(SharedWieldableSystem) });
        SubscribeLocalEvent<BayonetWeaponComponent, ItemUnwieldedEvent>(OnUnwielded, after: new[] { typeof(SharedWieldableSystem) });
        SubscribeLocalEvent<BayonetWeaponComponent, GetMeleeDamageEvent>(OnDamage);
    }

    private bool IsAttached(Entity<BayonetWeaponComponent> ent) => _slots.GetItemOrNull(ent.Owner, ent.Comp.Slot) != null;

    private void UpdateVisuals(Entity<BayonetWeaponComponent> ent)
    {
        var attached = IsAttached(ent);
        var prefix = attached ? ent.Comp.Prefix : null;
        _items.SetHeldPrefix(ent, prefix);
        _clothing.SetEquippedPrefix(ent, prefix);
        _appearance.SetData(ent, BayonetVisuals.Attached, attached);
    }

    private void OnStartup(Entity<BayonetWeaponComponent> ent, ref ComponentStartup args) => UpdateVisuals(ent);

    private void OnInserted(Entity<BayonetWeaponComponent> ent, ref EntInsertedIntoContainerMessage args)
    {
        if (args.Container.ID == ent.Comp.Slot)
            UpdateVisuals(ent);
    }

    private void OnRemoved(Entity<BayonetWeaponComponent> ent, ref EntRemovedFromContainerMessage args)
    {
        if (args.Container.ID == ent.Comp.Slot)
            UpdateVisuals(ent);
    }

    private void OnWielded(Entity<BayonetWeaponComponent> ent, ref ItemWieldedEvent args) => UpdateVisuals(ent);

    private void OnUnwielded(Entity<BayonetWeaponComponent> ent, ref ItemUnwieldedEvent args) => UpdateVisuals(ent);

    private void OnDamage(Entity<BayonetWeaponComponent> ent, ref GetMeleeDamageEvent args)
    {
        if (_slots.GetItemOrNull(ent.Owner, ent.Comp.Slot) is { } bayonet)
            args.Damage += _melee.GetDamage(bayonet, args.User);
    }
}
