using UnityEngine;

namespace WifiPositioningPrototype
{
    public sealed class SignalMeasurementHistory
    {
        private readonly float[] samples;
        private int nextIndex;

        public SignalMeasurementHistory(int capacity)
        {
            samples = new float[Mathf.Max(2, capacity)];
        }

        public int Count { get; private set; }
        public int Capacity => samples.Length;

        public void Add(float value)
        {
            if (!float.IsFinite(value)) return;
            samples[nextIndex] = value;
            nextIndex = (nextIndex + 1) % samples.Length;
            if (Count < samples.Length) Count++;
        }

        public float Range
        {
            get
            {
                if (Count < 2) return 0f;
                float min = float.PositiveInfinity;
                float max = float.NegativeInfinity;
                for (int i = 0; i < Count; i++)
                {
                    min = Mathf.Min(min, samples[i]);
                    max = Mathf.Max(max, samples[i]);
                }
                return max - min;
            }
        }

        public void Clear()
        {
            Count = 0;
            nextIndex = 0;
        }
    }
}
