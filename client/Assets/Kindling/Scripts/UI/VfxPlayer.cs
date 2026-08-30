using System.Collections.Generic;
using UnityEngine;

namespace Kindling.Client
{
    public static class VfxPlayer
    {
        static readonly Dictionary<string, GameObject> Cache = new Dictionary<string, GameObject>();
        const float DefaultScale = 0.45f;
        const float Lifetime = 2.4f;

        public static void Play(string key, RectTransform at)
        {
            if (at == null || string.IsNullOrEmpty(key)) return;
            Play(key, at.position, DefaultScale);
        }

        public static void Play(string key, Vector3 world, float scale)
        {
            GameObject prefab = Load(key);
            if (prefab == null) return;
            var go = Object.Instantiate(prefab);
            go.name = "fx_" + key;
            go.transform.position = world;
            go.transform.rotation = Quaternion.identity;
            go.transform.localScale = Vector3.one * (scale > 0.01f ? scale : DefaultScale);
            var renders = go.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renders.Length; i++)
                renders[i].sortingOrder = 80;
            var systems = go.GetComponentsInChildren<ParticleSystem>(true);
            for (int i = 0; i < systems.Length; i++)
            {
                var main = systems[i].main;
                main.scalingMode = ParticleSystemScalingMode.Hierarchy;
                systems[i].Play(true);
            }
            Object.Destroy(go, Lifetime);
        }

        static GameObject Load(string key)
        {
            GameObject go;
            if (Cache.TryGetValue(key, out go) && go != null) return go;
            go = Resources.Load<GameObject>("Fx/" + key);
            if (go != null) Cache[key] = go;
            return go;
        }
    }
}
