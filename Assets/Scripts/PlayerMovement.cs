using UnityEngine;
using UnityEngine.InputSystem;

namespace WifiPositioningPrototype
{
    public sealed class PlayerMovement : MonoBehaviour
    {
        [SerializeField, Min(0.1f)] private float movementSpeed = 3f;
        [SerializeField, Min(0f)] private float boundaryInset = 0.12f;
        [SerializeField] private RoomBounds roomBounds;

        private bool uiUp;
        private bool uiDown;
        private bool uiLeft;
        private bool uiRight;

        public float MovementSpeed => movementSpeed;

        public void Configure(RoomBounds bounds, float speed, float inset)
        {
            roomBounds = bounds;
            movementSpeed = Mathf.Max(0.1f, speed);
            boundaryInset = Mathf.Max(0f, inset);
        }

        public void SetUiDirection(Vector2 direction, bool pressed)
        {
            if (direction.y > 0.5f) uiUp = pressed;
            if (direction.y < -0.5f) uiDown = pressed;
            if (direction.x < -0.5f) uiLeft = pressed;
            if (direction.x > 0.5f) uiRight = pressed;
        }

        private void Update()
        {
            ApplyMovement(ReadCurrentInput(), Time.deltaTime);
        }

        public Vector2 ReadCurrentInput()
        {
            return ReadKeyboard() + ReadUi();
        }

        public void ApplyMovement(Vector2 input, float deltaTime)
        {
            if (input.sqrMagnitude > 1f) input.Normalize();

            Vector2 next = (Vector2)transform.position + input * movementSpeed * Mathf.Max(0f, deltaTime);
            if (roomBounds != null) next = roomBounds.Clamp(next, boundaryInset);
            transform.position = new Vector3(next.x, next.y, transform.position.z);
        }

        private static Vector2 ReadKeyboard()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null) return Vector2.zero;

            float x = (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed ? 1f : 0f)
                      - (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed ? 1f : 0f);
            float y = (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed ? 1f : 0f)
                      - (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed ? 1f : 0f);
            return new Vector2(x, y);
        }

        private Vector2 ReadUi()
        {
            return new Vector2((uiRight ? 1f : 0f) - (uiLeft ? 1f : 0f),
                (uiUp ? 1f : 0f) - (uiDown ? 1f : 0f));
        }

        private void OnDisable()
        {
            uiUp = uiDown = uiLeft = uiRight = false;
        }
    }
}
