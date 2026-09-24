using UnityEngine;

namespace DoodleArena
{
    // Wraps one ParticleSystem so gameplay code can ask for a quick puff of
    // particles with a given color, count and speed.
    [RequireComponent(typeof(ParticleSystem))]
    public class BurstEmitter : MonoBehaviour
    {
        [SerializeField] private Vector2 sizeRange = new Vector2(0.05f, 0.12f);
        [SerializeField] private Vector2 lifetimeRange = new Vector2(0.18f, 0.55f);

        private ParticleSystem system;
        private ParticleSystem.EmitParams emitParams;

        private void Awake()
        {
            system = GetComponent<ParticleSystem>();

            // Module settings are applied here rather than in the editor builder:
            // touching ps.main right after AddComponent in edit mode throws in this Unity version.
            var main = system.main;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startSpeed = 0f; // each particle gets its own velocity in Emit()
            main.maxParticles = 500;
            main.playOnAwake = false;

            var emission = system.emission;
            emission.rateOverTime = 0f;
        }

        public void Emit(Vector2 position, Color color, int count, float maxSpeed)
        {
            emitParams.position = position;
            emitParams.startColor = color;
            for (int i = 0; i < count; i++)
            {
                emitParams.velocity = Random.insideUnitCircle.normalized * Random.Range(maxSpeed * 0.25f, maxSpeed);
                emitParams.startLifetime = Random.Range(lifetimeRange.x, lifetimeRange.y);
                emitParams.startSize = Random.Range(sizeRange.x, sizeRange.y);
                system.Emit(emitParams, 1);
            }
        }
    }
}
