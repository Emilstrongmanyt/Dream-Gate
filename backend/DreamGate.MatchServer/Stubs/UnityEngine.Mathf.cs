namespace UnityEngine
{
    public static class Mathf
    {
        public static int Min(int a, int b) => a < b ? a : b;
        public static int Max(int a, int b) => a > b ? a : b;
        public static float Max(float a, float b) => a > b ? a : b;
        public static int Clamp(int value, int min, int max) => Max(min, Min(max, value));
        public static int CeilToInt(float value) => (int)Math.Ceiling(value);
        public static int RoundToInt(float value) => (int)Math.Round(value);
        public static float Clamp01(float value) => Clamp(value, 0f, 1f);
        public static float Clamp(float value, float min, float max) => Math.Max(min, Math.Min(max, value));
    }
}