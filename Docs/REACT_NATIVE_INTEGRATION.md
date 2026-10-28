# NUMU – Unity as a Library ↔ React Native

Status: **Unity side implemented and exercised in the Editor. Native host side not built or tested yet.**
Nothing below has been run on an Android or iOS device.

## What exists in the Unity project

| Piece | File | Purpose |
|---|---|---|
| Bridge component (auto-created, survives scene loads) | `Assets/NUMU/Scripts/Runtime/World/NumuBridge.cs` | Receives host messages, sends results back |
| Session model | `NumuSessionInfo` / `NumuSession` (same file) | Player/session info passed in by the host |
| Scene routing | `NumuSceneRouter` (same file) | `OpenWorld("home", "home_mum_sad")`, `ReturnToMap()` |
| Results store | `Assets/NUMU/Scripts/Runtime/World/NumuAssessment.cs` | Writes `numu_assessment_results.json` in `persistentDataPath` and forwards each result to the host |
| Android plugin | `Assets/Plugins/Android/NumuUnityBridge.java` | `com.numu.unity.NumuUnityBridge.setListener(...)` |
| iOS plugin | `Assets/Plugins/iOS/NumuBridge.mm` | `NumuRegisterHostHandler(...)` C function |

## Message protocol

### Host → Unity (`UnitySendMessage("NumuBridge", method, json)`)

| Method | Payload | Effect |
|---|---|---|
| `StartSession` | `{"sessionId":"…","playerId":"child-123","childName":"Sara","language":"en","reducedMotion":false,"soundOn":true,"world":"park","scenario":"park_swing_happy"}` | Stores the session, applies comfort settings, optionally opens a world |
| `OpenWorld` | `{"world":"park","scenario":"park_swing_happy"}` | Loads that world. Worlds and activities: `park`/`park_swing_happy`, `home`/`home_mum_sad`, `school`/`school_lina_nervous`, `supermarket`/`market_checkout` |
| `ExitGame` | `""` | Sends `exit`, then unloads Unity (`Application.Unload`) |

### Unity → Host (JSON envelope `{"type": "...", "payload": "<json string>"}`)

| type | When | payload |
|---|---|---|
| `ready` | Unity started | `{"type":"unityVersion","payload":"6000.6.2f1"}` |
| `sessionStarted` | after `StartSession` | the session object |
| `worldOpened` | a world starts loading | `{"world":"park","scenario":"park_swing_happy"}` |
| `assessmentResult` | **every answered question** | `{sessionId, playerId, world, scenarioId, question, correctAnswer, selectedAnswer, isCorrect, attempt, responseTimeMs, answeredAtUtc, reducedMotion}` |
| `scenarioFinished` | activity complete | Park: `{"scenario":"park_swing_happy","attempts":2}`; indoor worlds: `{"scenario":"home_mum_sad","attempts":4,"questions":2,"solved":2}` |
| `exit` | child leaves (Map button or `ExitGame`) | `{}` |
| `error` | unknown world requested | `{"world":"…"}` |

Results are also persisted on the device, so nothing is lost if the host is not listening; sync them
from the host (or re-send them later) using `sessionId` + `answeredAtUtc` as an idempotency key.

## Building the library

1. **Android** – Build Settings → Android → *Export Project*. The export contains `unityLibrary/`
   (the Unity player as an Android library, including `NumuUnityBridge.java`).
2. **iOS** – Build Settings → iOS → Build. In Xcode, the `UnityFramework` target is the library;
   set `Data` folder target membership to `UnityFramework` and make `NumuBridge.mm`'s
   `NumuRegisterHostHandler` public (add it to `UnityFramework.h` or a bridging header).

Recommended Player settings for embedding: IL2CPP, ARM64 only, `Graphics API`: Vulkan + OpenGLES3
(Android) / Metal (iOS), `Run In Background` off, Quality → **Mobile** as the default for both platforms.

## React Native host

The simplest route is the community package [`@azesmway/react-native-unity`](https://github.com/azesmway/react-native-unity)
(check that its current release supports your React Native version and Unity 6 before adopting it).
It expects the exported `unityLibrary` (Android) and `UnityFramework` (iOS) in a known folder and
exposes `<UnityView>` with `postMessage(gameObject, method, message)` and an `onUnityMessage` callback.

```tsx
<UnityView
  ref={unityRef}
  style={{ flex: 1 }}
  onUnityMessage={(e) => {
    const msg = JSON.parse(e.nativeEvent.message);        // { type, payload }
    if (msg.type === 'assessmentResult') saveResult(JSON.parse(msg.payload));
    if (msg.type === 'exit') navigation.goBack();
  }}
/>
// start a session once Unity reports "ready":
unityRef.current?.postMessage('NumuBridge', 'StartSession', JSON.stringify(session));
```

If that package's message channel is used instead of the NUMU plugins, route its native callback to
`NumuUnityBridge.setListener` (Android) / `NumuRegisterHostHandler` (iOS), or change
`NumuBridge.SendToHost` to call the package's own send method. Keep one channel only.

## Expo

Unity as a Library needs native code, so it does **not** run in Expo Go. Use a development build
(`npx expo prebuild` + `expo run:android` / `expo run:ios`, or EAS Build) with a config plugin that:

- Android: adds `include ':unityLibrary'` to `settings.gradle`, `implementation project(':unityLibrary')`
  to the app module, `ndk.abiFilters 'arm64-v8a'`, and the `unityStreamingAssets` entry to `gradle.properties`.
- iOS: embeds `UnityFramework.framework` in the app target and adds it to *Frameworks, Libraries and Embedded Content*.

## Not done yet

- No Android/iOS export has been produced from this project.
- The React Native module, Expo config plugin and device testing are outstanding.
- The memory and lifecycle behaviour of `Application.Unload` versus `Application.Quit` must be verified on each platform.
