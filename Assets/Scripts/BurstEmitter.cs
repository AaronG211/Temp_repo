using UnityEngine;

namespace DoodleArena
{
    // Aijia owns this file: the hand-rolled List<Spark> that DrawSpark rotated
    // and drew as little rectangles every frame is now a real ParticleSystem;
    // this just wraps it so GameManager.Burst(...) can still ask for a
    // variable-count, variable-color, variable-speed puff the same way it used to.
    [RequireComponent(typeof(ParticleSystem))]
    public class BurstEmitter : MonoBehaviour
    {
        private ParticleSystem system;
        private ParticleSystem.EmitParams emitParams;

        private void Awake()
        {
            system = GetComponent<ParticleSystem>();

            // Configuring these modules is only safe once the ParticleSystem is a live
            // component in a running scene (Play mode), not from an editor script right
            // after AddComponent, so it happens here instead of in the scene builder.
            var main = system.main;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startLifetime = .4f;
            main.startSize = .2f;
            main.startSpeed = 0f; // velocity is supplied per-particle via EmitParams
            main.maxParticles = 500;
            main.playOnAwake = false;

            var emission = system.emission;
            emission.rateOverTime = 0f;
        }

        public void Emit(Vector2 position, Color color, int count, float speedRange)
        {
            for (int i = 0; i < count; i++)
            {
                Vector2 dir = Random.insideUnitCircle.normalized;
                float speed = Random.Range(speedRange * .25f, speedRange);
                emitParams.position = position;
                emitParams.velocity = dir * speed;
                emitParams.startColor = color;
                emitParams.startLifetime = Random.Range(.18f, .55f);
                emitParams.startSize = Random.Range(.1f, .3f);
                system.Emit(emitParams, 1);
            }
        }
    }
}
