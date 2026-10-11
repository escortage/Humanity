using System.Numerics;
using Content.Shared.Humanity.Visuals;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Client.Humanity.Visuals;

public sealed partial class FoliageAtmosphereSystem : EntitySystem
{
    private static readonly ProtoId<ShaderPrototype> FoliageShader = "HumanityFoliage";
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private IPrototypeManager _prototypes = default!;
    [Dependency] private IPlayerManager _players = default!;
    [Dependency] private TransformSystem _transforms = default!;
    [Dependency] private IOverlayManager _overlays = default!;
    private readonly Dictionary<EntityUid, ShaderInstance> _shaders = new();
    private readonly HashSet<EntityUid> _brushed = new();
    private readonly Dictionary<EntityUid, float> _work = new();
    private readonly List<EntityUid> _workKeys = new();
    internal readonly List<FallingLeaf> Leaves = new();
    private float _elapsed;

    internal sealed class FallingLeaf
    {
        public MapCoordinates Origin;
        public float Height;
        public float Phase;
        public float Age;
        public float Lifetime;
        public Color Color;
    }

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<FoliageAtmosphereComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<FoliageAtmosphereComponent, ComponentShutdown>(OnShutdown);
        _overlays.AddOverlay(new FallingLeavesOverlay(this));
    }

    private void OnStartup(EntityUid uid, FoliageAtmosphereComponent component, ref ComponentStartup args)
    {
        if (!component.Enabled || !TryComp<SpriteComponent>(uid, out var sprite))
            return;
        var shader = _prototypes.Index(FoliageShader).InstanceUnique();
        shader.SetParameter("wind_phase", _random.NextFloat(0, MathF.Tau));
        shader.SetParameter("wind_speed", _random.NextFloat(0.65f, 1.15f));
        shader.SetParameter("wind_strength", _random.NextFloat(0.35f, 1.05f));
        shader.SetParameter("brush_strength", 0f);
        shader.SetParameter("work_strength", 0f);
        sprite.LayerSetShader(0, shader, "HumanityFoliage");
        if (_shaders.Remove(uid, out var old))
            old.Dispose();
        _shaders.Add(uid, shader);
    }

    private void OnShutdown(EntityUid uid, FoliageAtmosphereComponent component, ref ComponentShutdown args)
    {
        if (_shaders.Remove(uid, out var shader))
            shader.Dispose();
        _brushed.Remove(uid);
        _work.Remove(uid);
    }

    public override void Shutdown()
    {
        _overlays.RemoveOverlay<FallingLeavesOverlay>();
        foreach (var shader in _shaders.Values)
            shader.Dispose();
        _shaders.Clear();
        _brushed.Clear();
        _work.Clear();
        Leaves.Clear();
        base.Shutdown();
    }

    public override void FrameUpdate(float frameTime)
    {
        base.FrameUpdate(frameTime);
        _workKeys.Clear();
        _workKeys.AddRange(_work.Keys);
        foreach (var uid in _workKeys)
        {
            var age = _work[uid] + frameTime;
            if (!_shaders.TryGetValue(uid, out var shader))
            {
                _work.Remove(uid);
                continue;
            }
            shader.SetParameter("work_strength", age < 0.55f ? MathF.Sin(age * 38f) * (1f - age / 0.55f) : 0f);
            if (age >= 0.55f) _work.Remove(uid);
            else _work[uid] = age;
        }
        if (_players.LocalEntity is { } local)
        {
            var localTransform = Transform(local);
            var localPosition = _transforms.GetWorldPosition(localTransform);
            foreach (var (uid, shader) in _shaders)
            {
                if (!TryComp(uid, out TransformComponent? xform) || xform.MapUid != localTransform.MapUid)
                    continue;
                var delta = localPosition - _transforms.GetWorldPosition(xform);
                if (delta.LengthSquared() > 0.49f)
                {
                    if (_brushed.Remove(uid))
                        shader.SetParameter("brush_strength", 0f);
                    continue;
                }
                var brush = Math.Clamp(1f - delta.Length() / 0.7f, 0, 1) * Math.Clamp(-delta.X * 2f, -1f, 1f);
                shader.SetParameter("brush_strength", brush);
                _brushed.Add(uid);
            }
        }
        for (var i = Leaves.Count - 1; i >= 0; i--)
        {
            Leaves[i].Age += frameTime;
            if (Leaves[i].Age >= Leaves[i].Lifetime)
                Leaves.RemoveAt(i);
        }
        _elapsed += frameTime;
        if (_elapsed < 0.5f || Leaves.Count >= 20)
            return;
        _elapsed = 0;
        if (_players.LocalEntity is not { } player)
            return;
        var playerTransform = Transform(player);
        var playerPosition = _transforms.GetWorldPosition(playerTransform);
        var emitted = 0;
        var query = EntityQueryEnumerator<FoliageAtmosphereComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var foliage, out var transform))
        {
            if (!foliage.Enabled || !foliage.ShedLeaves || transform.MapUid != playerTransform.MapUid ||
                Vector2.DistanceSquared(_transforms.GetWorldPosition(transform), playerPosition) > 100f ||
                !_random.Prob(0.025f))
                continue;
            Leaves.Add(new FallingLeaf
            {
                Origin = _transforms.GetMapCoordinates(uid).Offset(new Vector2(_random.NextFloat(-0.5f, 0.5f), 0)),
                Height = _random.NextFloat(1.1f, 1.8f),
                Phase = _random.NextFloat(0, MathF.Tau),
                Lifetime = _random.NextFloat(5f, 7f),
                Color = _random.Prob(0.5f) ? Color.FromHex("#A48C48") : Color.FromHex("#879452"),
            });
            if (++emitted >= 2 || Leaves.Count >= 20)
                break;
        }
    }

    public void ShakeAt(MapCoordinates origin)
    {
        foreach (var uid in _shaders.Keys)
        {
            if (!TryComp(uid, out TransformComponent? transform) || transform.MapID != origin.MapId)
                continue;
            if (Vector2.DistanceSquared(_transforms.GetWorldPosition(transform), origin.Position) < 0.36f)
                _work[uid] = 0;
        }
    }
}
