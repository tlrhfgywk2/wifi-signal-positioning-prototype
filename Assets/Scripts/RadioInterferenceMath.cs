using UnityEngine;

namespace WifiPositioningPrototype
{
    public enum InterferenceLevel
    {
        None,
        Weak,
        Medium,
        Strong
    }

    public readonly struct InterferenceGeometry
    {
        public InterferenceGeometry(float pathDistance, Vector2 closestPoint, float amplitude,
            InterferenceLevel level, bool apContaminated)
        {
            PathDistance = pathDistance;
            ClosestPoint = closestPoint;
            Amplitude = amplitude;
            Level = level;
            ApContaminated = apContaminated;
        }

        public float PathDistance { get; }
        public Vector2 ClosestPoint { get; }
        public float Amplitude { get; }
        public InterferenceLevel Level { get; }
        public bool ApContaminated { get; }
    }

    public static class RadioInterferenceMath
    {
        public static float PointToSegmentDistance(Vector2 point, Vector2 segmentStart,
            Vector2 segmentEnd, out Vector2 closestPoint)
        {
            Vector2 segment = segmentEnd - segmentStart;
            float squaredLength = segment.sqrMagnitude;
            if (squaredLength <= Mathf.Epsilon)
            {
                closestPoint = segmentStart;
                return Vector2.Distance(point, segmentStart);
            }

            // Clamp the projection so points behind the AP or beyond the player use an endpoint.
            float t = Mathf.Clamp01(Vector2.Dot(point - segmentStart, segment) / squaredLength);
            closestPoint = segmentStart + segment * t;
            return Vector2.Distance(point, closestPoint);
        }

        public static InterferenceGeometry Evaluate(Vector2 creaturePosition, Vector2 accessPointPosition,
            Vector2 playerPosition, float strongRadius, float mediumRadius, float interferenceRadius,
            float strongAmplitude, float mediumAmplitude, float weakAmplitude, float apProximityRadius)
        {
            strongRadius = Mathf.Max(0f, strongRadius);
            mediumRadius = Mathf.Max(strongRadius, mediumRadius);
            interferenceRadius = Mathf.Max(mediumRadius, interferenceRadius);
            strongAmplitude = Mathf.Max(0f, strongAmplitude);
            mediumAmplitude = Mathf.Clamp(mediumAmplitude, 0f, strongAmplitude);
            weakAmplitude = Mathf.Clamp(weakAmplitude, 0f, mediumAmplitude);

            float distance = PointToSegmentDistance(creaturePosition, accessPointPosition,
                playerPosition, out Vector2 closestPoint);
            float amplitude;
            InterferenceLevel level;

            if (distance <= strongRadius)
            {
                amplitude = strongAmplitude;
                level = InterferenceLevel.Strong;
            }
            else if (distance <= mediumRadius)
            {
                amplitude = mediumAmplitude;
                level = InterferenceLevel.Medium;
            }
            else if (distance <= interferenceRadius)
            {
                amplitude = weakAmplitude;
                level = InterferenceLevel.Weak;
            }
            else
            {
                amplitude = 0f;
                level = InterferenceLevel.None;
            }

            bool contaminated = Vector2.Distance(creaturePosition, accessPointPosition)
                <= Mathf.Max(0f, apProximityRadius);
            if (contaminated && amplitude < mediumAmplitude)
            {
                amplitude = mediumAmplitude;
                level = InterferenceLevel.Medium;
            }

            return new InterferenceGeometry(distance, closestPoint, amplitude, level, contaminated);
        }
    }
}
