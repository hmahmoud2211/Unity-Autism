using System;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Numu.World
{
    /// <summary>Who is playing, supplied by the host React Native app (or defaults when run standalone).</summary>
    [Serializable]
    public class NumuSessionInfo
    {
        public string sessionId;
        public string playerId = "local-player";
        public string childName = "";
        public string language = "en";
        public bool reducedMotion;
        public bool soundOn = true;
        /// <summary>Optional: world/scenario to open straight away, e.g. "park" / "park_swing_happy".</summary>
        public string world;
        public string scenario;
    }

    public static class NumuSession
    {
        static NumuSessionInfo _current;

        public static NumuSessionInfo Current => _current ??= new NumuSessionInfo { sessionId = Guid.NewGuid().ToString("N") };

        public static bool LaunchedByHost { get; private set; }

        public static void Set(NumuSessionInfo info, bool fromHost)
        {
            if (string.IsNullOrEmpty(info.sessionId)) info.sessionId = Guid.NewGuid().ToString("N");
            _current = info;
            LaunchedByHost = fromHost;
            Numu.NumuSettings.ReducedMotion = info.reducedMotion;
            Numu.NumuSettings.SoundOn = info.soundOn;
        }
    }

    /// <summary>
    /// Unity side of the "Unity as a Library" bridge.
    /// Host → Unity: UnitySendMessage("NumuBridge", "StartSession", json) / ("OpenWorld", json) / ("ExitGame", "").
    /// Unity → Host: JSON envelopes { type, payload } via the native plugin (Android: com.numu.unity.NumuUnityBridge,
    /// iOS: NumuBridge.mm). In the Editor and standalone builds the messages are logged instead.
    /// </summary>
    public class NumuBridge : MonoBehaviour
    {
        public const string GameObjectName = "NumuBridge";

        [Serializable]
        class Envelope
        {
            public string type;
            public string payload;
        }

#if UNITY_IOS && !UNITY_EDITOR
        [DllImport("__Internal")] static extern void NumuSendToHost(string message);
#endif

        static NumuBridge _instance;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Bootstrap()
        {
            if (_instance != null) return;
            var go = new GameObject(GameObjectName);
            _instance = go.AddComponent<NumuBridge>();
            DontDestroyOnLoad(go);
            SendToHost("ready", JsonUtility.ToJson(new Envelope { type = "unityVersion", payload = Application.unityVersion }));
        }

        // ------------------------------------------------------------------ host → Unity

        /// <summary>Called by the host with a NumuSessionInfo JSON.</summary>
        public void StartSession(string json)
        {
            var info = string.IsNullOrEmpty(json) ? new NumuSessionInfo() : JsonUtility.FromJson<NumuSessionInfo>(json);
            NumuSession.Set(info, true);
            SendToHost("sessionStarted", JsonUtility.ToJson(NumuSession.Current));
            if (!string.IsNullOrEmpty(info.world)) NumuSceneRouter.OpenWorld(info.world, info.scenario);
        }

        /// <summary>Called by the host with {"world":"park","scenario":"park_swing_happy"}.</summary>
        public void OpenWorld(string json)
        {
            var info = JsonUtility.FromJson<NumuSessionInfo>(json);
            NumuSceneRouter.OpenWorld(info.world, info.scenario);
        }

        public void ExitGame(string _) => ExitToHost();

        // ------------------------------------------------------------------ Unity → host

        public static void SendToHost(string type, string payloadJson)
        {
            string message = JsonUtility.ToJson(new Envelope { type = type, payload = payloadJson });
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                using var bridge = new AndroidJavaClass("com.numu.unity.NumuUnityBridge");
                bridge.CallStatic("sendToHost", message);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[NUMU] Android bridge unavailable: {e.Message}");
            }
#elif UNITY_IOS && !UNITY_EDITOR
            NumuSendToHost(message);
#else
            Debug.Log($"[NUMU → host] {message}");
#endif
        }

        /// <summary>Leaves the Unity experience: hands control back to React Native, or returns to the map when standalone.</summary>
        public static void ExitToHost()
        {
            SendToHost("exit", "{}");
            if (NumuSession.LaunchedByHost && !Application.isEditor)
            {
                // Unity as a Library: unloads Unity but keeps the host process alive.
                Application.Unload();
                return;
            }
            NumuSceneRouter.ReturnToMap();
        }
    }

    /// <summary>Scene routing between the island map and the realistic worlds.</summary>
    public static class NumuSceneRouter
    {
        public const string MapScene = "NUMU_World";
        public const string ParkScene = "NUMU_Park";
        public const string HomeScene = "NUMU_Home";
        public const string SchoolScene = "NUMU_School";
        public const string SupermarketScene = "NUMU_Supermarket";

        /// <summary>Scenario requested for the next world load (read by the world's scenario controllers).</summary>
        public static string PendingScenario { get; private set; }

        /// <summary>Map level that launched the world (0 when opened directly or by the host without one).</summary>
        public static int PendingLevel { get; private set; }

        public static string SceneFor(string world) => world?.ToLowerInvariant() switch
        {
            "park" => ParkScene,
            "home" => HomeScene,
            "school" => SchoolScene,
            "supermarket" => SupermarketScene,
            _ => null,
        };

        public static void OpenWorld(string world, string scenario = null, int level = 0)
        {
            PendingScenario = scenario;
            PendingLevel = level;
            string scene = SceneFor(world);
            if (scene == null)
            {
                Debug.LogWarning($"[NUMU] World '{world}' has no realistic scene yet.");
                NumuBridge.SendToHost("error", JsonUtility.ToJson(new NumuSessionInfo { world = world }));
                return;
            }
            NumuBridge.SendToHost("worldOpened", $"{{\"world\":\"{world}\",\"scenario\":\"{scenario}\"}}");
            SceneManager.LoadScene(scene);
        }

        public static void ReturnToMap() => SceneManager.LoadScene(MapScene);
    }
}
