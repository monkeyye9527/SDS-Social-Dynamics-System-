namespace UnityEngine
{
    public static class Debug
    {
        public static void Log(object message) { }
        public static void LogWarning(object message) { }
        public static void LogError(object message) { }
    }

    public class Application
    {
        public static event System.Action quitting;
    }

    public static class Time
    {
        public static float time = 0f;
        public static float deltaTime = 0.016f;
    }

    /// <summary>测试替身：Mathf 常用数学函数（与 Unity 语义一致）。</summary>
    public static class Mathf
    {
        public static float Clamp01(float value)
        {
            if (value < 0f) return 0f;
            if (value > 1f) return 1f;
            return value;
        }

        public static float Clamp(float value, float min, float max)
        {
            if (value < min) return min;
            if (value > max) return max;
            return value;
        }

        public static float Lerp(float a, float b, float t)
        {
            if (t < 0f) t = 0f;
            if (t > 1f) t = 1f;
            return a + (b - a) * t;
        }

        public static float Min(float a, float b) => a < b ? a : b;
        public static float Max(float a, float b) => a > b ? a : b;
    }
}
