using System.Collections;
using UnityEngine;

namespace DreamGate.Battlegrounds.Services.Backend
{
    internal static class AuthCoroutineTimeouts
    {
        public static IEnumerator WaitUntil(System.Func<bool> condition, float timeoutSeconds)
        {
            var deadline = Time.realtimeSinceStartup + timeoutSeconds;
            while (!condition() && Time.realtimeSinceStartup < deadline)
            {
                yield return null;
            }
        }

        public static bool HasTimedOut(float deadlineRealtime)
        {
            return Time.realtimeSinceStartup >= deadlineRealtime;
        }

        public static float CreateDeadline(float timeoutSeconds)
        {
            return Time.realtimeSinceStartup + timeoutSeconds;
        }
    }
}