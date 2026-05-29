namespace ZombieWars.Audio;

using Godot;
using Godot.Collections;
using ZombieWars.Core;

/// <summary>
/// Sound manager — loads generated WAV files and plays them via AudioStreamPlayer.
/// All files are in res://assets/audio/*.wav
/// </summary>
public partial class SoundManager : Node
{
    public static SoundManager? Instance { get; private set; }

    private AudioStreamPlayer? _musicPlayer;
    private readonly AudioStreamPlayer[] _sfxChannels = new AudioStreamPlayer[4];
    private int _sfxChannelIdx;
    private readonly Dictionary<string, AudioStream> _sfxCache = new();

    // Sound toggle state
    private bool _sfxEnabled = true;
    private bool _musicEnabled = true;
    private float _sfxVolumeMultiplier = 1.0f;
    private float _musicVolumeMultiplier = 1.0f;

    public bool SfxEnabled
    {
        get => _sfxEnabled;
        set
        {
            _sfxEnabled = value;
            foreach (var ch in _sfxChannels)
                ch.VolumeDb = value ? GameConfig.SfxVolumeDb + Mathf.LinearToDb(_sfxVolumeMultiplier) : -80.0f;
        }
    }

    public bool MusicEnabled
    {
        get => _musicEnabled;
        set
        {
            _musicEnabled = value;
            if (_musicPlayer != null)
                _musicPlayer.VolumeDb = value ? GameConfig.MusicVolumeDb + Mathf.LinearToDb(_musicVolumeMultiplier) : -80.0f;
        }
    }

    public float SfxVolume
    {
        get => _sfxVolumeMultiplier;
        set
        {
            _sfxVolumeMultiplier = Mathf.Clamp(value, 0.0f, 2.0f);
            if (_sfxEnabled)
            {
                foreach (var ch in _sfxChannels)
                    ch.VolumeDb = GameConfig.SfxVolumeDb + Mathf.LinearToDb(_sfxVolumeMultiplier);
            }
        }
    }

    public float MusicVolume
    {
        get => _musicVolumeMultiplier;
        set
        {
            _musicVolumeMultiplier = Mathf.Clamp(value, 0.0f, 2.0f);
            if (_musicEnabled && _musicPlayer != null)
                _musicPlayer.VolumeDb = GameConfig.MusicVolumeDb + Mathf.LinearToDb(_musicVolumeMultiplier);
        }
    }

    public override void _Ready()
    {
        Instance = this;
        _musicPlayer = new AudioStreamPlayer { VolumeDb = GameConfig.MusicVolumeDb, Bus = "Master" };
        AddChild(_musicPlayer);

        for (int i = 0; i < 4; i++)
        {
            var ch = new AudioStreamPlayer { VolumeDb = GameConfig.SfxVolumeDb, Bus = "Master" };
            AddChild(ch);
            _sfxChannels[i] = ch;
        }

        _PrecacheSfx();
    }

    private void _PrecacheSfx()
    {
        string[] files = {
            "melee_attack", "ranged_attack", "magic_attack",
            "zombie_hit", "zombie_die", "player_hurt", "player_die",
            "weapon_select", "upgrade", "game_over",
            "explosion", "lightning", "charm",
            "footstep", "dash"
        };
        foreach (var name in files)
        {
            var path = $"res://assets/audio/{name}.wav";
            if (ResourceLoader.Exists(path))
            {
                var stream = GD.Load<AudioStream>(path);
                if (stream != null)
                    _sfxCache[name] = stream;
            }
        }
    }

    public override void _ExitTree()
    {
        if (Instance == this) Instance = null;
    }

    private void _PlaySfx(string key)
    {
        if (!_sfxEnabled || !_sfxCache.TryGetValue(key, out var stream)) return;
        var ch = _sfxChannels[_sfxChannelIdx];
        _sfxChannelIdx = (_sfxChannelIdx + 1) % _sfxChannels.Length;
        ch.Stream = stream;
        ch.Play();
    }

    // ══ Public Playback API ══

    public void PlayMeleeAttack() => _PlaySfx("melee_attack");
    public void PlayRangedAttack() => _PlaySfx("ranged_attack");
    public void PlayMagicAttack() => _PlaySfx("magic_attack");
    public void PlayZombieHit() => _PlaySfx("zombie_hit");
    public void PlayZombieDie() => _PlaySfx("zombie_die");
    public void PlayPlayerHurt() => _PlaySfx("player_hurt");
    public void PlayPlayerDie() => _PlaySfx("player_die");
    public void PlayWeaponSelect() => _PlaySfx("weapon_select");
    public void PlayUpgrade() => _PlaySfx("upgrade");
    public void PlayGameOver() => _PlaySfx("game_over");
    public void PlayExplosion() => _PlaySfx("explosion");
    public void PlayLightning() => _PlaySfx("lightning");
    public void PlayCharm() => _PlaySfx("charm");
    public void PlayFootstep() => _PlaySfx("footstep");
    public void PlayDash() => _PlaySfx("dash");

    private int _killStreak;
    public void NotifyKill()
    {
        _killStreak++;
        if (_killStreak >= 5)
            _PlaySfx("combo_5");
        else if (_killStreak >= 3)
            _PlaySfx("combo_3");
    }

    public void ResetKillStreak()
    {
        _killStreak = 0;
    }

    public void StartMusic()
    {
        if (_musicPlayer == null) return;
        var path = "res://assets/audio/bg_music.wav";
        if (ResourceLoader.Exists(path))
        {
            _musicPlayer.Stream = GD.Load<AudioStream>(path);
            _musicPlayer.ProcessMode = Node.ProcessModeEnum.Always;
            if (!_musicPlayer.IsConnected("finished", Callable.From(() => _musicPlayer?.Play())))
                _musicPlayer.Connect("finished", Callable.From(() => _musicPlayer?.Play()));
            _musicPlayer.Play();
        }
    }

    public void StopMusic()
    {
        _musicPlayer?.Stop();
    }

    public void SetMusicVolume(float volumeDb)
    {
        if (_musicPlayer != null)
            _musicPlayer.VolumeDb = volumeDb;
    }
}
