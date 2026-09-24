using UnityEngine;

namespace DoodleArena
{
    public enum GameState { Title, Playing, BetweenWave, GameOver, Victory }
    public enum EnemyKind { Chaser, Shooter, Tank, Boss }
    public enum ShotKind { Player, Enemy, Bottle, Crate, BossOrb }

    // Anything the player can hit. Regular enemies and the boss both implement this
    // so attacks and the HUD don't need to care which one they're dealing with.
    public interface IDamageableEnemy
    {
        float Hp { get; }
        float MaxHp { get; }
        EnemyKind Kind { get; }
        Vector2 Position { get; }
        void TakeDamage(float amount, Vector2 push);
    }
}
