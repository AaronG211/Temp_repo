using UnityEngine;

namespace DoodleArena
{
    // Aijia owns this file: screen shake used to be faked by nudging GUI.matrix
    // every frame in OnGUI. It now actually moves the camera's own transform.
    public class CameraShake : MonoBehaviour
    {
        [SerializeField] private float intensityScale = .3f;

        private Vector3 basePosition;
        private float currentShake;

        private void Awake() => basePosition = transform.localPosition;

        public void SetShake(float amount) => currentShake = amount;

        private void LateUpdate()
        {
            transform.localPosition = currentShake > 0f
                ? basePosition + (Vector3)(Random.insideUnitCircle * currentShake * intensityScale)
                : basePosition;
        }
    }
}
