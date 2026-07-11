using UnityEngine;
using UnityEngine.UI;

namespace WifiPositioningPrototype
{
    public sealed class SignalRowView : MonoBehaviour
    {
        private AccessPointSignal accessPoint;
        private Text identifierText;
        private Text valueText;
        private Text statusText;
        private Image fillImage;

        public void Configure(AccessPointSignal point, Text identifier, Text value, Text status, Image fill)
        {
            accessPoint = point;
            identifierText = identifier;
            valueText = value;
            statusText = status;
            fillImage = fill;
        }

        public void Refresh(float strongThreshold, float goodThreshold, float fairThreshold,
            float minimumBarSignal, float maximumBarSignal)
        {
            if (accessPoint == null) return;
            float signal = accessPoint.CurrentSignalStrength;
            identifierText.text = CornerLabel(accessPoint.Identifier);
            valueText.text = $"{Mathf.RoundToInt(signal),4} dBm";
            statusText.text = Classify(signal, strongThreshold, goodThreshold, fairThreshold);
            fillImage.fillAmount = Mathf.InverseLerp(minimumBarSignal, maximumBarSignal, signal);
        }

        private static string Classify(float signal, float strong, float good, float fair)
        {
            if (signal >= strong) return "STRONG";
            if (signal >= good) return "GOOD";
            if (signal >= fair) return "FAIR";
            return "WEAK";
        }

        private static string CornerLabel(string id)
        {
            return id switch { "NW" => "NW  ↖", "NE" => "NE  ↗", "SW" => "SW  ↙", "SE" => "SE  ↘", _ => id };
        }
    }
}
