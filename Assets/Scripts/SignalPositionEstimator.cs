using System.Collections.Generic;
using UnityEngine;

namespace WifiPositioningPrototype
{
    public sealed class SignalPositionEstimator : MonoBehaviour
    {
        [SerializeField] private Vector2 estimatedPosition;
        [SerializeField] private bool hasValidEstimate;

        public Vector2 EstimatedPosition => estimatedPosition;
        public bool HasValidEstimate => hasValidEstimate;

        public bool Refresh(IReadOnlyList<AccessPointSignal> accessPoints)
        {
            hasValidEstimate = TryEstimate(accessPoints, out estimatedPosition);
            return hasValidEstimate;
        }

        public static bool TryEstimate(IReadOnlyList<AccessPointSignal> accessPoints, out Vector2 result)
        {
            result = Vector2.zero;
            if (accessPoints == null || accessPoints.Count < 3 || accessPoints[0] == null) return false;
            if (!accessPoints[0].TryGetEstimatedDistance(out float d0)) return false;

            Vector2 p0 = accessPoints[0].Position;
            float a00 = 0f, a01 = 0f, a11 = 0f, b0 = 0f, b1 = 0f;
            int equationCount = 0;

            for (int i = 1; i < accessPoints.Count; i++)
            {
                AccessPointSignal point = accessPoints[i];
                if (point == null || !point.TryGetEstimatedDistance(out float distance)) continue;

                Vector2 p = point.Position;
                Vector2 row = 2f * (p - p0);
                float rhs = d0 * d0 - distance * distance + p.sqrMagnitude - p0.sqrMagnitude;
                a00 += row.x * row.x;
                a01 += row.x * row.y;
                a11 += row.y * row.y;
                b0 += row.x * rhs;
                b1 += row.y * rhs;
                equationCount++;
            }

            float determinant = a00 * a11 - a01 * a01;
            if (equationCount < 2 || Mathf.Abs(determinant) <= 0.000001f) return false;

            result = new Vector2((b0 * a11 - b1 * a01) / determinant,
                (a00 * b1 - a01 * b0) / determinant);
            return float.IsFinite(result.x) && float.IsFinite(result.y);
        }
    }
}
