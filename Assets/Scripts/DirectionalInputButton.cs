using UnityEngine;
using UnityEngine.EventSystems;

namespace WifiPositioningPrototype
{
    public sealed class DirectionalInputButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
    {
        [SerializeField] private Vector2 direction;
        [SerializeField] private PlayerMovement playerMovement;

        public void Configure(PlayerMovement movement, Vector2 inputDirection)
        {
            playerMovement = movement;
            direction = inputDirection;
        }

        public void OnPointerDown(PointerEventData eventData) => SetPressed(true);
        public void OnPointerUp(PointerEventData eventData) => SetPressed(false);
        public void OnPointerExit(PointerEventData eventData) => SetPressed(false);
        private void OnDisable() => SetPressed(false);

        private void SetPressed(bool pressed)
        {
            if (playerMovement != null) playerMovement.SetUiDirection(direction, pressed);
        }
    }
}
