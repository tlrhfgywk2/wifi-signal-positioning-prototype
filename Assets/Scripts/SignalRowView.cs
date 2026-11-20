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
            float minimumBarSignal, float maximumBarSignal, float unstableRangeThreshold,
            float severeRangeThreshold)
        {
            if (accessPoint == null) return;
            float signal = accessPoint.CurrentSignalStrength;
            identifierText.text = accessPoint.Identifier;
            valueText.text = $"{Mathf.RoundToInt(signal),4} dBm  Δ{accessPoint.DifferenceFromExpected:+0;-0;0}";
            statusText.text = SignalStatusClassifier.Classify(accessPoint.IsApContaminated,
                accessPoint.CurrentInterferenceLevel, accessPoint.InstabilityRange,
                accessPoint.HasRecentCreatureInterference, unstableRangeThreshold, severeRangeThreshold);
            fillImage.fillAmount = Mathf.InverseLerp(minimumBarSignal, maximumBarSignal, signal);
        }
    }

    public static class SignalStatusClassifier
    {
        public static string Classify(bool contaminated, InterferenceLevel level, float instabilityRange,
            bool hasRecentCreatureInterference, float unstableThreshold, float severeThreshold)
        {
            if (contaminated) return "AP CONTAMINATED";
            if (level == InterferenceLevel.Strong
                || hasRecentCreatureInterference && instabilityRange >= severeThreshold)
                return "SEVERE INTERFERENCE";
            if (level == InterferenceLevel.Medium
                || hasRecentCreatureInterference && instabilityRange >= unstableThreshold)
                return "UNSTABLE";
            if (level == InterferenceLevel.Weak) return "WEAK INTERFERENCE";
            return "STABLE";
        }
    }
}
