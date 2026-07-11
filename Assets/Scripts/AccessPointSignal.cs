using UnityEngine;

namespace WifiPositioningPrototype
{
    public sealed class AccessPointSignal : MonoBehaviour
    {
        [SerializeField] private string identifier = "NW";
        [SerializeField] private float referenceSignalStrength = -20f;
        [SerializeField, Min(0.001f)] private float signalLossPerMeter = 5f;
        [SerializeField] private float currentSignalStrength;

        public string Identifier => identifier;
        public Vector2 Position => transform.position;
        public float ReferenceSignalStrength => referenceSignalStrength;
        public float SignalLossPerMeter => signalLossPerMeter;
        public float CurrentSignalStrength => currentSignalStrength;

        public void Configure(string stableIdentifier, Vector2 position, float referenceStrength, float lossPerMeter)
        {
            identifier = stableIdentifier;
            transform.position = new Vector3(position.x, position.y, 0f);
            referenceSignalStrength = referenceStrength;
            signalLossPerMeter = lossPerMeter;
        }

        public void Refresh(Vector2 playerPosition)
        {
            currentSignalStrength = LinearSignalModel.Calculate(referenceSignalStrength,
                signalLossPerMeter, Vector2.Distance(Position, playerPosition));
        }

        public bool TryGetEstimatedDistance(out float distance)
        {
            return LinearSignalModel.TryRecoverDistance(referenceSignalStrength, currentSignalStrength,
                signalLossPerMeter, out distance);
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
            return float.IsFinite(distance) && distance >= 0f;
        }
    }
}
