
using Photon.Deterministic;
using Quantum;

public class ProjectileAsset : AssetObject {
    public ProjectileEffectType Effect;
    public bool Bounce = true;
    public bool Ricochet;
    public bool Strict45Bounce;
    public FP Speed;
    public FP BounceStrength;
    public FP RicochetDamping = Constants._0_85;
    public FPVector2 Gravity;
    public byte LifetimeFrames;
    public bool DestroyOnSecondBounce;
    public bool DestroyOnHit = true;
    public bool LockTo45Degrees = true;
    public bool InheritShooterVelocity;
    public bool HasCollision = true;
    public bool DoesntEffectBlueShell = true;

    public ParticleEffect DestroyParticleEffect = ParticleEffect.None;
    public SoundEffect ShootSound = SoundEffect.Powerup_Fireball_Shoot;
}

public enum ProjectileEffectType {
    Fire,
    Freeze,
    KillEnemiesAndSoftKnockbackPlayers,
}
