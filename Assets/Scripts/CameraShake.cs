using UnityEngine;

namespace DoodleArena
{
    // Jitters the camera around its resting position. GameManager feeds in the
    // current shake amount (world units) every frame.
    public class CameraShake : MonoBehaviour
    {
        [SerializeField] private float strength = 0.3f;

        private Vector3 restPosition;
        private float amount;

        private void Awake() => restPosition = transform.localPosition;

        public void SetShake(float value) => amount = value;

        private void LateUpdate()
        {
            Vector3 offset = amount > 0f ? (Vector3)(Random.insideUnitCircle * amount * strength) : Vector3.zero;
            transform.localPosition = restPosition + offset;
        }
    }
}
