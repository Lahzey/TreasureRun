using System.Collections;
using HarmonyLib;
using TreasureRun.Core;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TreasureRun.Patches
{
    /// <summary>
    /// Faster scene loading, in every mode. Vanilla <see cref="LevelLoader"/> plays a 1.5 s circle wipe in, waits at least
    /// 3 s on the loading screen whatever the real load time, then plays a 1.5 s wipe out in the new scene.
    /// The 3 s minimum is always removed; the wipes are skipped when <see cref="ModConfig.LoadingAnimation"/> is off
    /// (the loading screen then appears and disappears instantly).
    /// </summary>
    [HarmonyPatch(typeof(LevelLoader))]
    internal static class LoadingPatches
    {
        private const float WipeSeconds = 1.5f;

        private static bool Animate => ModConfig.LoadingAnimation == null || ModConfig.LoadingAnimation.Value;

        [HarmonyPrefix, HarmonyPatch(nameof(LevelLoader.LoadAsyncronously))]
        private static bool LoadPrefix(LevelLoader __instance, string sceneName, ref IEnumerator __result)
        {
            __result = Load(__instance, sceneName);
            return false;
        }

        [HarmonyPrefix, HarmonyPatch(nameof(LevelLoader.CircleExitStart))]
        private static bool ExitPrefix(LevelLoader __instance, ref IEnumerator __result)
        {
            __result = Exit(__instance);
            return false;
        }

        /// <summary>Vanilla LoadAsyncronously without the minimum wait (and optionally without the wipe).</summary>
        private static IEnumerator Load(LevelLoader loader, string sceneName)
        {
            RectTransform circle = loader.circle.rectTransform;
            float size = Mathf.Sqrt(Screen.height * Screen.width) * 1.1f;
            Vector2 endSize = new Vector2(2f, 1f) * size * 2f;
            Vector2 startSize = new Vector2(0f, 1f) * size * 2f;
            Vector3 endPosition = new Vector3(Screen.height / 2f, Screen.height / 2f, 0f);
            Vector3 startPosition = new Vector3(Screen.width * 1.1f, Screen.height / 2f, 0f);

            if (Animate)
            {
                for (float t = 0f; t < 1f; t += Time.deltaTime / WipeSeconds)
                {
                    circle.sizeDelta = Vector2.Lerp(startSize, endSize, t);
                    circle.position = Vector3.Lerp(startPosition, endPosition, t);
                    yield return null;
                }
            }
            circle.sizeDelta = endSize;
            circle.position = endPosition;
            loader.tipText.text = loader.GetTipsText();

            AsyncOperation operation = SceneManager.LoadSceneAsync(sceneName);
            while (!operation.isDone)
            {
                loader.loadingText.text = loader.GetLoadingText(Mathf.Clamp01(operation.progress / 0.9f));
                yield return null;
            }
        }

        /// <summary>Vanilla CircleExitStart, or an instant reveal without the animation.</summary>
        private static IEnumerator Exit(LevelLoader loader)
        {
            RectTransform circle = loader.circle.rectTransform;
            float size = Mathf.Sqrt(Screen.height * Screen.width) * 1.1f;
            Vector2 startSize = new Vector2(2f, 1f) * size * 2f;
            Vector2 endSize = new Vector2(0f, 1f) * size * 2f;
            Vector3 startPosition = new Vector3(Screen.height / 2f, Screen.height / 2f, 0f);
            Vector3 endPosition = new Vector3(-Screen.width * 0.1f, Screen.height / 2f, 0f);

            if (Animate)
            {
                circle.sizeDelta = startSize;
                circle.position = startPosition;
                for (float t = 0f; t < 1f; t += Time.deltaTime / WipeSeconds)
                {
                    circle.sizeDelta = Vector2.Lerp(startSize, endSize, t);
                    circle.position = Vector3.Lerp(startPosition, endPosition, t);
                    yield return null;
                }
            }
            circle.sizeDelta = endSize;
            circle.position = endPosition;
        }
    }
}
