using System.Collections;
using UnityEngine;

namespace DreamGate.Battlegrounds.Services.Backend
{
    public sealed class CloudCoroutineHost : MonoBehaviour
    {
        private static CloudCoroutineHost instance;

        public static CloudCoroutineHost Instance
        {
            get
            {
                if (instance != null)
                {
                    return instance;
                }

                var existing = FindAnyObjectByType<CloudCoroutineHost>();
                if (existing != null)
                {
                    instance = existing;
                    return instance;
                }

                var go = new GameObject(nameof(CloudCoroutineHost));
                DontDestroyOnLoad(go);
                instance = go.AddComponent<CloudCoroutineHost>();
                return instance;
            }
        }

        public Coroutine Run(IEnumerator routine)
        {
            gameObject.SetActive(true);
            return StartCoroutine(routine);
        }

        public void Stop(IEnumerator routine)
        {
            if (routine != null)
            {
                StopCoroutine(routine);
            }
        }

        public void Stop(Coroutine routine)
        {
            if (routine != null)
            {
                StopCoroutine(routine);
            }
        }

        private void OnDestroy()
        {
            if (instance == this)
            {
                instance = null;
            }
        }
    }
}