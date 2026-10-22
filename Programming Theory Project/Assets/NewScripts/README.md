# They Be Buggin — Beginner-friendly refactor

## Installation
1. Back up your Unity project or commit your current scripts to GitHub.
2. Replace the corresponding C# scripts in Assets with the files in this folder. Do not keep two copies of the same class.
3. Keep Main Camera separate from Player. Keep CameraPivot under Player.
4. Put CameraFollow on Main Camera, CameraLook on CameraPivot. Remove the old CameraCollisions component (its script is a compatibility placeholder only).
5. In CameraFollow's Collision Layers, EXCLUDE the Player AND Enemy layers so the camera does not collide with characters. Keep the walls/ground included.
6. Keep Player/Enemy physics collisions ENABLED. Enable Rigidbody Interpolate on player and enemies. Freeze enemy X/Z rotation where appropriate; the beetle's existing model orientation may require checking rotation constraints.
7. Check the Inspector references for CameraPivot, bullet spawner, UI, prefabs, and GameManager.

## What changed
- Enemy: abstract base class; private health; shared TakeDamage; static AliveCount to avoid repeated scene-wide searches.
- Spider/Beetle: Rigidbody linearVelocity for chase rather than MovePosition pushing through colliders; stop within attack range; knockback still uses AddForce. Beetle charge has a brief duration to preserve its impulse.
- PlayerController: same WASD, mouse yaw, jump, gravity, dash; ground detection now refreshes from all collision contacts every physics step rather than resetting whenever any collider exits.
- BulletShooter: same moving visual projectile, segmented Raycast, damage, knockback, cooldown.
- CameraFollow: same movement lag, jump/dash FOV, collision tests. Existing collision approach has limitations if the camera starts embedded in geometry.
- GameManager: shared SetGameState to avoid duplicate UI/controls setup.
- CrosshairFollow: cache Camera.main rather than finding it every frame.
- CameraCollisions: no-op legacy class; remove its component when using CameraFollow.

## Four OOP pillars
1. Encapsulation: Enemy health is private, modified through TakeDamage; PlayerHealth isDead has a private setter.
2. Inheritance: Spider and Beetle inherit Enemy.
3. Abstraction: Enemy is abstract and requires Attack.
4. Polymorphism: Spider and Beetle override Attack; Beetle calls its override through an Enemy reference.

## Notes / limitations
- This is a source-level refactor; it has NOT been compiled or play-tested in your Unity project. Test collisions, jump timing, beetle orientation, and camera wall behavior in Play Mode.
- Attack range must exceed combined collider sizes to stop AI from continually pressing into the player. Beetle's charge intentionally still physically pushes.
- Moving the beetle with velocity changes collision dynamics versus MovePosition; tune speed/chargeForce if needed.
- The projectile coroutine is still one coroutine per shot to preserve the original behavior. For large bullet counts, object pooling could be added later.
- The old CameraCollisions class is deliberately inactive to prevent two scripts fighting over the camera position.
