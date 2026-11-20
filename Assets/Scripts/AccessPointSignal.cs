using UnityEngine;

namespace WifiPositioningPrototype
{
    public sealed class AccessPointSignal : MonoBehaviour
    {
        [SerializeField] private string identifier = "NW";
        [SerializeField] private float referenceSignalStrength = -20f;
        [SerializeField, Min(0.001f)] private float signalLossPerMeter = 5f;
        [SerializeField, Min(0f)] private float measurementNoiseAmplitude = 3f;
        [Tooltip("Stable loss from walls or other static obstacles. This does not count as creature instability.")]
        [SerializeField, Min(0f)] private float stableAttenuationDb;
        [SerializeField, Range(4, 64)] private int historyCapacity = 12;
        [SerializeField] private float currentSignalStrength;

        private SignalMeasurementHistory history;
        private int recentCreatureSamples;

        public string Identifier => identifier;
        public Vector2 Position => transform.position;
        public float ReferenceSignalStrength => referenceSignalStrength;
        public float SignalLossPerMeter => signalLossPerMeter;
        public float MeasurementNoiseAmplitude => measurementNoiseAmplitude;
        public float CurrentSignalStrength => currentSignalStrength;
        public float ExpectedSignalStrength { get; private set; }
        public float StableAttenuationDb => stableAttenuationDb;
        public float CreatureAttenuationDb { get; private set; }
        public float DifferenceFromExpected => currentSignalStrength - ExpectedSignalStrength;
        public float InstabilityRange => history?.Range ?? 0f;
        public int HistoryCount => history?.Count ?? 0;
        public int HistoryCapacity => history?.Capacity ?? Mathf.Max(2, historyCapacity);
        public InterferenceLevel CurrentInterferenceLevel { get; private set; }
        public bool IsApContaminated { get; private set; }
        public Vector2 ClosestPathPoint { get; private set; }
        public float PathDistanceToCreature { get; private set; } = float.PositiveInfinity;
        public bool HasRecentCreatureInterference => recentCreatureSamples > 0;

        public void Configure(string stableIdentifier, Vector2 position, float referenceStrength, float lossPerMeter,
            float noiseAmplitude = 0f, float stableLossDb = 0f)
        {
            identifier = stableIdentifier;
            transform.position = new Vector3(position.x, position.y, 0f);
            referenceSignalStrength = referenceStrength;
            signalLossPerMeter = lossPerMeter;
            measurementNoiseAmplitude = Mathf.Max(0f, noiseAmplitude);
            stableAttenuationDb = Mathf.Max(0f, stableLossDb);
        }

        public void Refresh(Vector2 playerPosition)
        {
            Refresh(playerPosition, null, Time.time);
        }

        public void Refresh(Vector2 playerPosition, RadioInterferenceCreature creature, float sampleTime)
        {
            EnsureHistory();
            ExpectedSignalStrength = LinearSignalModel.Calculate(referenceSignalStrength,
                signalLossPerMeter, Vector2.Distance(Position, playerPosition));
            float noise = measurementNoiseAmplitude > 0f
                ? Random.Range(-measurementNoiseAmplitude, measurementNoiseAmplitude)
                : 0f;

            CreatureAttenuationDb = 0f;
            CurrentInterferenceLevel = InterferenceLevel.None;
            IsApContaminated = false;
            ClosestPathPoint = Position;
            PathDistanceToCreature = float.PositiveInfinity;

            if (creature != null)
            {
                InterferenceGeometry geometry = creature.EvaluateGeometry(Position, playerPosition);
                CreatureAttenuationDb = creature.SampleAttenuation(geometry, sampleTime, StableChannel(identifier));
                CurrentInterferenceLevel = geometry.Level;
                IsApContaminated = geometry.ApContaminated;
                ClosestPathPoint = geometry.ClosestPoint;
                PathDistanceToCreature = geometry.PathDistance;
                recentCreatureSamples = geometry.Level != InterferenceLevel.None
                    ? HistoryCapacity
                    : Mathf.Max(0, recentCreatureSamples - 1);
            }
            else recentCreatureSamples = 0;

            currentSignalStrength = ExpectedSignalStrength - stableAttenuationDb + noise + CreatureAttenuationDb;
            history.Add(currentSignalStrength);
        }

        public bool TryGetEstimatedDistance(out float distance)
        {
            return LinearSignalModel.TryRecoverDistance(referenceSignalStrength, currentSignalStrength,
                signalLossPerMeter, out distance);
        }

        private void EnsureHistory()
        {
            int capacity = Mathf.Max(2, historyCapacity);
            if (history == null || history.Capacity != capacity) history = new SignalMeasurementHistory(capacity);
        }

        private static int StableChannel(string value)
        {
            unchecked
            {
                int hash = 17;
                if (!string.IsNullOrEmpty(value))
                    for (int i = 0; i < value.Length; i++) hash = hash * 31 + value[i];
                return hash & 0x7fffffff;
            }
        }
    }

    public static class LinearSignalModel
    {
        public static float Calculate(float referenceStrength, float lossPerMeter, float distance)
        {
            if (!float.IsFinite(referenceStrength) || !float.IsFinite(lossPerMeter) || !float.IsFinite(distance))
                return float.NaN;
            return referenceStrength - Mathf.Max(0f, distance) * lossPerMeter;
        }

        public static bool TryRecoverDistance(float referenceStrength, float signalStrength,
            float lossPerMeter, out float distance)
        {
            distance = 0f;
            if (!float.IsFinite(referenceStrength) || !float.IsFinite(signalStrength)
                || !float.IsFinite(lossPerMeter) || lossPerMeter <= Mathf.Epsilon)
                return false;

            distance = (referenceStrength - signalStrength) / lossPerMeter;
            if (!float.IsFinite(distance)) return false;
            distance = Mathf.Max(0f, distance);
            return true;
        }
    }
}
