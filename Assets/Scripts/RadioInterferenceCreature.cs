using UnityEngine;

namespace WifiPositioningPrototype
{
    public sealed class RadioInterferenceCreature : MonoBehaviour
    {
        [Header("Path distance bands")]
        [SerializeField, Min(0f)] private float strongPathRadius = 0.25f;
        [SerializeField, Min(0f)] private float mediumPathRadius = 0.65f;
        [SerializeField, Min(0f)] private float interferenceRadius = 1.4f;

        [Header("Noise amplitude (dBm)")]
        [SerializeField, Min(0f)] private float maximumNoiseAmplitude = 12f;
        [SerializeField, Min(0f)] private float mediumNoiseAmplitude = 6f;
        [SerializeField, Min(0f)] private float weakNoiseAmplitude = 3f;
        [SerializeField, Min(0f)] private float apProximityInterferenceRadius = 0.9f;
        [SerializeField, Min(0.01f)] private float noiseUpdateFrequency = 0.7f;
        [SerializeField] private float noiseSeed = 17.31f;

        [Header("Optional movement")]
        [SerializeField] private bool patrolEnabled;
        [SerializeField, Min(0f)] private float patrolDistance = 1.5f;
        [SerializeField, Min(0f)] private float patrolSpeed = 0.5f;
        [SerializeField] private Vector2 patrolDirection = Vector2.right;

        private Vector3 patrolOrigin;
        private Material runtimeMaterial;

        public float InterferenceRadius => interferenceRadius;
        public float ApProximityInterferenceRadius => apProximityInterferenceRadius;

        private void Awake()
        {
            patrolOrigin = transform.position;
            BuildPlaceholder();
        }

        private void Update()
        {
            if (!patrolEnabled || patrolDistance <= 0f || patrolDirection.sqrMagnitude <= Mathf.Epsilon) return;
            float offset = Mathf.Sin(Time.time * patrolSpeed) * patrolDistance;
            Vector2 direction = patrolDirection.normalized;
            transform.position = patrolOrigin + new Vector3(direction.x, direction.y, 0f) * offset;
        }

        public InterferenceGeometry EvaluateGeometry(Vector2 accessPointPosition, Vector2 playerPosition)
        {
            return RadioInterferenceMath.Evaluate(transform.position, accessPointPosition, playerPosition,
                strongPathRadius, mediumPathRadius, interferenceRadius, maximumNoiseAmplitude,
                mediumNoiseAmplitude, weakNoiseAmplitude, apProximityInterferenceRadius);
        }

        public float SampleAttenuation(InterferenceGeometry geometry, float sampleTime, int stableChannel)
        {
            if (geometry.Amplitude <= Mathf.Epsilon) return 0f;
            float channelOffset = (stableChannel % 997) * 0.173f + noiseSeed;
            float noise = Mathf.PerlinNoise(channelOffset, sampleTime * Mathf.Max(0.01f, noiseUpdateFrequency));
            return -Mathf.Lerp(geometry.Amplitude * (2f / 3f), geometry.Amplitude, noise);
        }

        private void BuildPlaceholder()
        {
            Shader shader = Shader.Find("Sprites/Default");
            if (shader == null) shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null)
            {
                Debug.LogWarning("Radio creature could not find an unlit shader for its placeholder.", this);
                return;
            }

            runtimeMaterial = new Material(shader) { color = new Color(0.2f, 1f, 0.85f, 1f) };
            CreateCircle("Creature Icon", 0.24f, 0.07f, new Color(0.2f, 1f, 0.85f, 1f));
        }

        private void CreateCircle(string objectName, float radius, float width, Color color)
        {
            GameObject circle = new(objectName);
            circle.transform.SetParent(transform, false);
            circle.transform.localPosition = new Vector3(0f, 0f, -0.15f);
            LineRenderer line = circle.AddComponent<LineRenderer>();
            line.useWorldSpace = false;
            line.loop = true;
            line.positionCount = 48;
            line.startWidth = line.endWidth = width;
            line.sharedMaterial = runtimeMaterial;
            line.startColor = line.endColor = color;
            for (int i = 0; i < line.positionCount; i++)
            {
                float angle = i * Mathf.PI * 2f / line.positionCount;
                line.SetPosition(i, new Vector3(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius));
            }
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.2f, 1f, 0.85f, 0.9f);
            Gizmos.DrawWireSphere(transform.position, interferenceRadius);
            Gizmos.color = new Color(1f, 0.35f, 0.8f, 0.9f);
            Gizmos.DrawWireSphere(transform.position, apProximityInterferenceRadius);
        }

        private void OnDestroy()
        {
            if (runtimeMaterial == null) return;
            if (Application.isPlaying) Destroy(runtimeMaterial);
            else DestroyImmediate(runtimeMaterial);
        }
    }
}
