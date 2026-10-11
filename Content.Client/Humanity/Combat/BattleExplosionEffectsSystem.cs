using System.Numerics;
using Content.Shared.Explosion;
using Content.Shared.Explosion.Components;
using Robust.Shared.Prototypes;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Random;
using Content.Client.Humanity.Visuals;

namespace Content.Client.Humanity.Combat;

public sealed partial class BattleExplosionEffectsSystem : EntitySystem
{
    [Dependency] private IPrototypeManager _prototypes = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private TransformSystem _transforms = default!;
    [Dependency] private IOverlayManager _overlays = default!;
    [Dependency] private SharedMapSystem _map = default!;
    [Dependency] private ITileDefinitionManager _tiles = default!;

    public override void Initialize()
    {
        _overlays.AddOverlay(new BattleExplosionWaveOverlay(EntityManager));
    }

    public override void Shutdown()
    {
        _overlays.RemoveOverlay<BattleExplosionWaveOverlay>();
        base.Shutdown();
    }

    public void SpawnEffects(ExplosionVisualsComponent explosion)
    {
        if (!_map.MapExists(explosion.Epicenter.MapId))
            return;
        if (_overlays.TryGetOverlay<HumanityAtmosphereOverlay>(out var fog))
            fog.Disperse(explosion.Epicenter, explosion.Intensity.Count * 1.2f);
        if (!_prototypes.Index<ExplosionPrototype>(explosion.ExplosionType).BattleEffects)
            return;

        Spawn("HumanityBlastFlash", explosion.Epicenter);
        var wave = Spawn(null, explosion.Epicenter);
        AddComp<BattleExplosionWaveComponent>(wave).MaxRadius = Math.Clamp(explosion.Intensity.Count, 2, 8);
        var count = Math.Clamp(explosion.Intensity.Count * 2, 8, 16);
        var dirt = false;
        if (_map.TryFindGridAt(explosion.Epicenter, out var grid, out MapGridComponent? gridComp) &&
            _map.TryGetTileRef(grid, gridComp, new EntityCoordinates(grid,
                Vector2.Transform(explosion.Epicenter.Position, _transforms.GetInvWorldMatrix(grid))), out var tile))
        {
            var id = _tiles[tile.Tile.TypeId].ID;
            dirt = id.Contains("Dirt", StringComparison.Ordinal) || id.Contains("Grass", StringComparison.Ordinal) ||
                id.Contains("Sand", StringComparison.Ordinal);
        }
        for (var i = 0; i < count; i++)
        {
            var angle = _random.NextFloat() * MathF.Tau;
            var direction = new Vector2(MathF.Cos(angle), MathF.Sin(angle));
            var entity = Spawn("HumanityBlastDust", new MapCoordinates(explosion.Epicenter.Position + direction * 0.25f, explosion.Epicenter.MapId));
            var particle = AddComp<BattleExplosionParticleComponent>(entity);
            particle.Velocity = direction * _random.NextFloat(0.4f, 0.8f);
            particle.Lifetime = _random.NextFloat(4f, 6f);
            particle.Growth = 0.25f;
            particle.Tint = dirt ? new Color(0.40f, 0.32f, 0.23f, 0.8f) : new Color(0.36f, 0.35f, 0.33f, 0.85f);
        }
        Spawn("EffectSparks", explosion.Epicenter);
    }

    public override void Update(float frameTime)
    {
        var waves = EntityQueryEnumerator<BattleExplosionWaveComponent>();
        while (waves.MoveNext(out var waveUid, out var wave))
        {
            wave.Age += frameTime;
            if (wave.Age >= wave.Lifetime)
                QueueDel(waveUid);
        }
        var query = EntityQueryEnumerator<BattleExplosionParticleComponent, SpriteComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var particle, out var sprite, out var transform))
        {
            particle.Age += frameTime;
            if (particle.Age >= particle.Lifetime)
            {
                QueueDel(uid);
                continue;
            }
            _transforms.SetWorldPosition(uid, _transforms.GetWorldPosition(transform) + particle.Velocity * frameTime);
            sprite.Scale += new Vector2(particle.Growth * frameTime);
            sprite.Color = particle.Tint.WithAlpha(particle.Tint.A * (1f - particle.Age / particle.Lifetime));
        }
    }
}
