using System.Collections.Generic;
using UnityEngine;

namespace WifiPositioningPrototype
{
    public sealed class RadioInterferenceDebugVisualizer : MonoBehaviour
    {
        [SerializeField] private bool visualizationEnabled;
        [SerializeField, Min(0.01f)] private float closestPointMarkerSize = 0.09f;

        private IReadOnlyList<AccessPointSignal> accessPoints;
        private Transform player;
        private RadioInterferenceCreature creature;

        public bool VisualizationEnabled
        {
            get => visualizationEnabled;
            set => visualizationEnabled = value;
        }

        public void Configure(IReadOnlyList<AccessPointSignal> points, Transform playerTransform,
            RadioInterferenceCreature interferenceCreature, bool visible)
        {
            accessPoints = points;
            player = playerTransform;
            creature = interferenceCreature;
            visualizationEnabled = visible;
        }

        private void Update()
        {
            if (!visualizationEnabled || player == null || creature == null || accessPoints == null) return;
            for (int i = 0; i < accessPoints.Count; i++)
            {
                AccessPointSignal point = accessPoints[i];
                Color color = ColorFor(point.CurrentInterferenceLevel);
                Debug.DrawLine(point.Position, player.position, color);
                Debug.DrawLine(creature.transform.position, point.ClosestPathPoint, color);
            }
        }

        private void OnDrawGizmos()
        {
            if (!visualizationEnabled || player == null || creature == null || accessPoints == null) return;
            Gizmos.color = new Color(0.2f, 1f, 0.85f, 0.75f);
            Gizmos.DrawWireSphere(creature.transform.position, creature.InterferenceRadius);
            for (int i = 0; i < accessPoints.Count; i++)
            {
                AccessPointSignal point = accessPoints[i];
                Gizmos.color = ColorFor(point.CurrentInterferenceLevel);
                Gizmos.DrawLine(point.Position, player.position);
                Gizmos.DrawLine(creature.transform.position, point.ClosestPathPoint);
                Gizmos.DrawSphere(point.ClosestPathPoint, closestPointMarkerSize);
                if (point.IsApContaminated)
                    Gizmos.DrawWireSphere(point.Position, creature.ApProximityInterferenceRadius);
            }
        }

        private static Color ColorFor(InterferenceLevel level)
        {
            return level switch
            {
                InterferenceLevel.Strong => Color.red,
                InterferenceLevel.Medium => new Color(1f, 0.55f, 0f),
                InterferenceLevel.Weak => Color.yellow,
                _ => new Color(0.2f, 1f, 0.35f)
            };
        }
    }
}
