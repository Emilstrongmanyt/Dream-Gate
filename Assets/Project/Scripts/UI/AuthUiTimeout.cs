using System;
using System.Collections;
using DreamGate.Battlegrounds.Services.Backend;

namespace DreamGate.Battlegrounds.UI
{
    internal static class AuthUiTimeout
    {
        public static IEnumerator Run(
            IEnumerator authRoutine,
            Func<bool> isFinished,
            float timeoutSeconds,
            string timeoutMessage,
            Action<string> setTimedOutMessage)
        {
            CloudCoroutineHost.Instance.Run(authRoutine);
            var deadline = AuthCoroutineTimeouts.CreateDeadline(timeoutSeconds);
            while (!isFinished() && !AuthCoroutineTimeouts.HasTimedOut(deadline))
            {
                yield return null;
            }

            if (!isFinished())
            {
                setTimedOutMessage(timeoutMessage);
            }
        }
    }
}