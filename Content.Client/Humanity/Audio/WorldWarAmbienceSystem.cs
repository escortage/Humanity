using Content.Client.Audio;
using Content.Client.Gameplay;
using Content.Client.Weather;
using Content.Shared.CCVar;
using Content.Shared.GameTicking;
using Content.Shared.Humanity.Audio;
using Content.Shared.Light.Components;
using Robust.Client.Player;
using Robust.Client.State;
using Robust.Shared.Audio.Components;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Configuration;
using Robust.Shared.Map.Components;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Client.Humanity.Audio;

public sealed partial class WorldWarAmbienceSystem : EntitySystem
{
    [Dependency] private IPlayerManager _players = default!;
    [Dependency] private IStateManager _state = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private ContentAudioSystem _contentAudio = default!;
    [Dependency] private SharedMapSystem _maps = default!;
    [Dependency] private WeatherSystem _weather = default!;
    [Dependency] private IConfigurationManager _config = default!;
    [Dependency] private IPrototypeManager _prototypes = default!;
    [Dependency] private IRobustRandom _random = default!;

    private sealed class LayerState(WorldWarAmbienceLayer layer, float delay)
    {
        public readonly WorldWarAmbienceLayer Layer = layer;
        public EntityUid? Stream;
        public float Delay = delay;
    }

    private readonly List<LayerState> _layers = new();
    private WorldWarAmbiencePrototype? _soundscape;
    private EntityUid? _activeMap;
    private float _volume;
    private float _exposure;
    private float _targetExposure;
    private float _sample;
    private bool _roundEnded;

    public override void Initialize()
    {
        base.Initialize();
        Subs.CVar(_config, CCVars.AmbienceVolume, value => _volume = value, true);
        _state.OnStateChanged += OnStateChanged;
        SubscribeLocalEvent<PlayAmbientMusicEvent>(OnAmbientMusic);
        SubscribeNetworkEvent<RoundEndMessageEvent>(_ =>
        {
            _roundEnded = true;
            Stop();
        });
        SubscribeNetworkEvent<RoundRestartCleanupEvent>(_ =>
        {
            _roundEnded = false;
            Stop();
        });
        SubscribeNetworkEvent<TickerConnectionStatusEvent>(_ =>
        {
            _roundEnded = false;
            Stop();
        });
    }

    public override void Shutdown()
    {
        _state.OnStateChanged -= OnStateChanged;
        Stop();
        base.Shutdown();
    }

    private void OnStateChanged(StateChangedEventArgs args)
    {
        if (args.NewState is GameplayState)
            return;
        Stop();
    }

    private void OnAmbientMusic(ref PlayAmbientMusicEvent ev)
    {
        if (TryComp<TransformComponent>(_players.LocalEntity, out var player) &&
            TryComp<WorldWarAmbienceComponent>(player.MapUid, out var ambience) &&
            _prototypes.TryIndex(ambience.Soundscape, out _))
            ev.Cancelled = true;
    }

    private void Stop()
    {
        foreach (var layer in _layers)
        {
            if (TryComp<AudioComponent>(layer.Stream, out var audio))
                _audio.Stop(layer.Stream, audio);
        }
        _layers.Clear();
        _soundscape = null;
        _activeMap = null;
        _exposure = 0;
        _targetExposure = 0;
        _sample = 0;
    }

    public override void FrameUpdate(float frameTime)
    {
        base.FrameUpdate(frameTime);
        if (_roundEnded || _state.CurrentState is not GameplayState || _volume <= 0 ||
            !TryComp<TransformComponent>(_players.LocalEntity, out var player) ||
            !TryComp<WorldWarAmbienceComponent>(player.MapUid, out var ambience) ||
            !_prototypes.TryIndex(ambience.Soundscape, out var soundscape))
        {
            if (_activeMap != null)
                Stop();
            return;
        }

        if (_activeMap != player.MapUid || _soundscape != soundscape)
        {
            Stop();
            _activeMap = player.MapUid;
            _soundscape = soundscape;
            _contentAudio.DisableAmbientMusic();
            foreach (var layer in _soundscape.Layers)
                _layers.Add(new LayerState(layer, NextDelay(layer) / 2f));
        }

        _sample -= frameTime;
        if (_sample <= 0)
        {
            _sample = 0.5f;
            _targetExposure = 1f;
            if (player.GridUid is { } grid && TryComp<MapGridComponent>(grid, out var gridComp) &&
                _maps.TryGetTileRef(grid, gridComp, player.Coordinates, out var tile))
            {
                TryComp<RoofComponent>(grid, out var roof);
                if (!_weather.CanWeatherAffect((grid, gridComp, roof), tile))
                    _targetExposure = Math.Clamp(soundscape.IndoorGain, 0f, 1f);
            }
        }

        var fadeDuration = MathF.Max(soundscape.FadeDuration, float.Epsilon);
        _exposure += (_targetExposure - _exposure) * (1f - MathF.Exp(-frameTime / fadeDuration));
        foreach (var state in _layers)
        {
            state.Delay -= frameTime;
            if (!TryComp<AudioComponent>(state.Stream, out var audio) && state.Delay <= 0)
            {
                var stream = _audio.PlayGlobal(state.Layer.Sound, Filter.Local(), false,
                    state.Layer.Sound.Params.WithVolume(-60f));
                state.Stream = stream?.Entity;
                audio = stream?.Component;
                state.Delay = MathF.Max(1f, NextDelay(state.Layer));
            }
            if (audio != null)
                _audio.SetGain(state.Stream,
                    SharedAudioSystem.VolumeToGain(state.Layer.Sound.Params.Volume) * _exposure * _volume, audio);
        }
    }

    private float NextDelay(WorldWarAmbienceLayer layer)
    {
        if (layer.Sound.Params.Loop)
            return 0;
        var min = MathF.Max(1f, layer.Interval.X);
        return _random.NextFloat(min, MathF.Max(min, layer.Interval.Y));
    }
}
