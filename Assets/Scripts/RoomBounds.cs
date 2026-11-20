using UnityEngine;

namespace WifiPositioningPrototype
{
    public sealed class RoomBounds : MonoBehaviour
    {
        [SerializeField, Min(1f)] private float width = 10f;
        [SerializeField, Min(1f)] private float height = 10f;

        public float Width => width;
        public float Height => height;
        public Vector2 Min => (Vector2)transform.position - new Vector2(width, height) * 0.5f;
        public Vector2 Max => (Vector2)transform.position + new Vector2(width, height) * 0.5f;

        public void Configure(float roomWidth, float roomHeight)
        {
            width = Mathf.Max(1f, roomWidth);
            height = Mathf.Max(1f, roomHeight);
        }

        public Vector2 Clamp(Vector2 position, float inset = 0f)
        {
            Vector2 min = Min + Vector2.one * Mathf.Max(0f, inset);
            Vector2 max = Max - Vector2.one * Mathf.Max(0f, inset);
            return new Vector2(Mathf.Clamp(position.x, min.x, max.x), Mathf.Clamp(position.y, min.y, max.y));
        }
    }
}
