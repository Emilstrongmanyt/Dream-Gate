using System.Collections.Generic;
using UnityEngine;

namespace Kindling.Client
{
    public static class VfxPlayer
    {
        // Visual diameter of each CFXR prefab at localScale 1, from its largest startSize.
        static readonly Dictionary<string, float> Native = new Dictionary<string, float>
        {
            { "aura", 2.6f },
            { "flash", 4.5f },
            { "poof", 1.8f },
            { "slash", 3.0f },
            { "hit", 3.2f },
            { "spark", 2.0f },
            { "fire", 1.6f },
            { "venom", 3.6f },
            { "smoke", 2.0f },
            { "boom", 7.5f }
        };

        static readonly Dictionary<string, float> Cover = new Dictionary<string, float>
        {
            { "aura", 1.40f },
            { "flash", 1.15f },
            { "poof", 1.25f },
            { "slash", 1.20f },
            { "hit", 1.10f },
            { "spark", 1.15f },
            { "fire", 1.25f },
            { "venom", 1.15f },
            { "smoke", 1.15f },
            { "boom", 1.35f }
        };

        static readonly HashSet<string> Ground = new HashSet<string> { "aura", "venom" };
        static readonly Dictionary<string, GameObject> Cache = new Dictionary<string, GameObject>();
        const float Lifetime = 2.6f;

        public static void Play(string key, RectTransform at)
        {
            Play(key, at, 1f);
        }

        public static void Play(string key, RectTransform at, float coverMul)
        {
            if (at == null || string.IsNullOrEmpty(key)) return;
            Vector3 center;
            float span;
            Measure(at, out center, out span);
            float native;
            if (!Native.TryGetValue(key, out native) || native < 0.05f) native = 2f;
            float cover;
            if (!Cover.TryGetValue(key, out cover)) cover = 1.15f;
            if (coverMul < 0.1f) coverMul = 1f;
            float scale = Mathf.Clamp(span * cover * coverMul / native, 0.22f, 3.2f);
            Quaternion face = at.rotation;
            // Magic Aura / Poison Cloud are authored on XZ. The UI camera looks along Z, so tilt them into the canvas plane.
            if (Ground.Contains(key))
                face *= Quaternion.Euler(90f, 0f, 0f);
            Play(key, center, scale, face);
        }

        public static void Play(string key, Vector3 world, float scale)
        {
            Play(key, world, scale, Quaternion.identity);
        }

        static void Play(string key, Vector3 world, float scale, Quaternion rotation)
        {
            GameObject prefab = Load(key);
            if (prefab == null) return;
            var go = Object.Instantiate(prefab);
            go.name = "fx_" + key;
            var cam = Camera.main;
            if (cam != null)
            {
                Vector3 toCam = cam.transform.position - world;
                if (toCam.sqrMagnitude > 0.0001f)
                    world += toCam.normalized * 0.28f;
            }
            go.transform.SetPositionAndRotation(world, rotation);
            go.transform.localScale = Vector3.one * (scale > 0.01f ? scale : 1f);
            var renders = go.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renders.Length; i++)
            {
                renders[i].sortingOrder = 80;
                renders[i].allowOcclusionWhenDynamic = false;
            }
            var systems = go.GetComponentsInChildren<ParticleSystem>(true);
            for (int i = 0; i < systems.Length; i++)
            {
                var main = systems[i].main;
                main.scalingMode = ParticleSystemScalingMode.Hierarchy;
                main.simulationSpace = ParticleSystemSimulationSpace.Local;
                systems[i].Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                systems[i].Play(true);
            }
            Object.Destroy(go, Lifetime);
        }

        static void Measure(RectTransform at, out Vector3 center, out float span)
        {
            var corners = new Vector3[4];
            at.GetWorldCorners(corners);
            center = (corners[0] + corners[1] + corners[2] + corners[3]) * 0.25f;
            float w = Vector3.Distance(corners[0], corners[3]);
            float h = Vector3.Distance(corners[0], corners[1]);
            // Wide HUD strips must not use the long axis as the burst size.
            span = Mathf.Max(Mathf.Min(w, h) * 1.35f, Mathf.Max(w, h) * 0.42f);
            span = Mathf.Clamp(span, 0.95f, 4.2f);
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
