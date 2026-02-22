using Photon.Deterministic;

namespace Quantum {
    public unsafe class ProjectileSystem : SystemMainThreadEntityFilter<Projectile, ProjectileSystem.Filter>, ISignalOnProjectileHitEntity {
        public struct Filter {
            public EntityRef Entity;
            public Transform2D* Transform;
            public Projectile* Projectile;
            public PhysicsObject* PhysicsObject;
            public PhysicsCollider2D* PhysicsCollider;
        }

        public override void Update(Frame f, ref Filter filter, VersusStageData stage) {
            var collider = filter.PhysicsCollider;
            var transform = filter.Transform;

            if (filter.Transform->Position.Y + collider->Shape.Centroid.Y + collider->Shape.Box.Extents.Y < stage.StageWorldMin.Y) {
                Destroy(f, filter.Entity, ParticleEffect.None);
                return;
            }

            var projectile = filter.Projectile;
            var physicsObject = filter.PhysicsObject;
            var asset = f.FindAsset(projectile->Asset);

            // Reuse Combo as a lightweight per-projectile age counter (0-255 frames).
            if (asset.LifetimeFrames > 0) {
                if (projectile->Combo < byte.MaxValue) {
                    projectile->Combo++;
                }
                if (projectile->Combo >= asset.LifetimeFrames) {
                    Destroy(f, filter.Entity, asset.DestroyParticleEffect);
                    return;
                }
            }

            // Check to instant-despawn if spawned inside a wall
            if (!physicsObject->DisableCollision && !projectile->CheckedCollision) {
                if (PhysicsObjectSystem.BoxInGround(f, transform->Position, collider->Shape)) {
                    Destroy(f, filter.Entity, asset.DestroyParticleEffect);
                    return;
                }
                projectile->CheckedCollision = true;
            }

            HandleTileCollision(f, ref filter, asset);

            physicsObject->Velocity.X = projectile->Speed * (projectile->FacingRight ? 1 : -1);

            if (asset.LockTo45Degrees) {
                physicsObject->TerminalVelocity = -projectile->Speed;
            }
        }

        public void HandleTileCollision(Frame f, ref Filter filter, ProjectileAsset asset) {
            var projectile = filter.Projectile;
            var physicsObject = filter.PhysicsObject;

            bool touchingLeft = physicsObject->IsTouchingLeftWall;
            bool touchingRight = physicsObject->IsTouchingRightWall;
            bool touchingCeiling = physicsObject->IsTouchingCeiling;
            bool touchingGround = physicsObject->IsTouchingGround;

            if (!physicsObject->DisableCollision) {
                if (asset.Strict45Bounce) {
                    if (PhysicsObjectSystem.BoxInGround(f, filter.Transform->Position, filter.PhysicsCollider->Shape)) {
                        Destroy(f, filter.Entity, asset.DestroyParticleEffect);
                        return;
                    }

                    bool collided = false;
                    if ((touchingLeft && !projectile->FacingRight) || (touchingRight && projectile->FacingRight)) {
                        projectile->FacingRight = !projectile->FacingRight;
                        collided = true;
                    }

                    FP verticalSpeed = FPMath.Abs(projectile->Speed);
                    if (touchingGround) {
                        projectile->HasBounced = true;
                        physicsObject->Velocity.Y = verticalSpeed;
                        physicsObject->IsTouchingGround = false;
                        collided = true;
                    } else if (touchingCeiling) {
                        projectile->HasBounced = false;
                        physicsObject->Velocity.Y = -verticalSpeed;
                        collided = true;
                    } else if (collided) {
                        physicsObject->Velocity.Y = projectile->HasBounced ? verticalSpeed : -verticalSpeed;
                    }

                    return;
                }

                // Ricochet off walls/ceiling for special projectiles (e.g., Super Ball).
                if (asset.Ricochet) {
                    if (touchingLeft && physicsObject->Velocity.X < 0) {
                        projectile->FacingRight = true;
                        projectile->Speed *= asset.RicochetDamping;
                    } else if (touchingRight && physicsObject->Velocity.X > 0) {
                        projectile->FacingRight = false;
                        projectile->Speed *= asset.RicochetDamping;
                    }

                    if (touchingCeiling && physicsObject->Velocity.Y > 0) {
                        physicsObject->Velocity.Y = -FPMath.Abs(physicsObject->Velocity.Y) * asset.RicochetDamping;
                    }
                }

                // Despawn on hard collisions unless ricochet is enabled.
                if (((touchingLeft || touchingRight || touchingCeiling) && !asset.Ricochet)
                    || (touchingGround && (!asset.Bounce || (projectile->HasBounced && asset.DestroyOnSecondBounce)))
                    || PhysicsObjectSystem.BoxInGround(f, filter.Transform->Position, filter.PhysicsCollider->Shape)) {

                    Destroy(f, filter.Entity, asset.DestroyParticleEffect);
                    return;
                }
            }

            // Bounce
            if (touchingGround && asset.Bounce) {
                FP boost = asset.BounceStrength * FPMath.Abs(FPMath.Sin(physicsObject->FloorAngle * FP.Deg2Rad)) * FP._1_25;
                if ((physicsObject->FloorAngle > 0) == projectile->FacingRight) {
                    boost = 0;
                }

                physicsObject->Velocity.Y = asset.BounceStrength + boost;
                physicsObject->IsTouchingGround = false;
                projectile->HasBounced = true;
            }
        }

        public static void Destroy(Frame f, EntityRef entity, ParticleEffect particle) {
            var transform = f.Unsafe.GetPointer<Transform2D>(entity);
            f.Events.ProjectileDestroyed(entity, particle, transform->Position);
            f.Destroy(entity);
        }

        public void OnProjectileHitEntity(Frame f, Frame frame, EntityRef projectileEntity, EntityRef hitEntity) {
            var projectile = f.Unsafe.GetPointer<Projectile>(projectileEntity);
            var projectileAsset = f.FindAsset(projectile->Asset);

            if (projectileAsset.DestroyOnHit) {
                Destroy(f, projectileEntity, projectileAsset.DestroyParticleEffect);
            } else if (projectileAsset.Bounce) {
                var physicsObject = f.Unsafe.GetPointer<PhysicsObject>(projectileEntity);
                projectile->Speed *= Constants._0_85;
                physicsObject->Gravity *= Constants._0_85;
                physicsObject->Velocity.Y = projectile->Speed;

                f.Events.EnemyKicked(hitEntity, false);
                if (projectile->Speed < 1) {
                    Destroy(f, projectileEntity, projectileAsset.DestroyParticleEffect);
                }
            }
        }
    }
}
