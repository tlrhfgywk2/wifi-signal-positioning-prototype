using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace WifiPositioningPrototype
{
    public sealed class SignalPanelController : MonoBehaviour
    {
        [Header("Configurable classifications")]
        [SerializeField] private float strongThreshold = -45f;
        [SerializeField] private float goodThreshold = -60f;
        [SerializeField] private float fairThreshold = -75f;
        [SerializeField] private float minimumBarSignal = -90f;
        [SerializeField] private float maximumBarSignal = -20f;

        private readonly List<SignalRowView> rows = new();
        private Text positionText;

        public void Configure(Text coordinates) => positionText = coordinates;
        public void AddRow(SignalRowView row) => rows.Add(row);

        public void Refresh(Vector2 estimated, float error, bool valid)
        {
            foreach (SignalRowView row in rows)
                row.Refresh(strongThreshold, goodThreshold, fairThreshold, minimumBarSignal, maximumBarSignal);

            if (positionText != null)
                positionText.text = valid
                    ? $"ESTIMATED POSITION\nX  {estimated.x,6:0.00} m\nY  {estimated.y,6:0.00} m\nERROR  {error:0.000} m"
                    : "ESTIMATED POSITION\nNO VALID SOLUTION";
        }
    }
}
