# NUMU Park World

Scene: `Assets/Scenes/Worlds/NUMU_Park.unity` (in Build Settings).
Rebuild: menu **NUMU → Worlds → Build Park World**, or call `Numu.EditorTools.World.NumuParkBuilder.Build()`.
Rebuilding regenerates the whole scene, prefabs, materials and baked lighting. Edit the builder code, not the scene, or your manual changes will be overwritten.

## How to play

- **From the island map:** Park › Level 4 *Identify emotions* › Play. The level must be unlocked, i.e. levels 1–3 finished.
- **Directly:** open `NUMU_Park.unity` and press Play.
- **Controls:** WASD or arrow keys (Shift to walk faster), a gamepad stick, or the on-screen joystick on touch devices. Space, E, Enter or the prompt button interacts. **V** or the view button switches between third and first person, and dragging on the right of the screen (or holding the right mouse button) looks around. See `CHARACTERS_AND_CAMERA.md`.

## The activity: "A happy child on a swing" (`park_swing_happy`)

1. Walk to the swings. A **Say hi to Mia** prompt appears when you are within about 2 m.
2. Mia slows to a stop and the camera frames her face; she smiles, blinks and talks with lip movement.
3. **How is Mia feeling?** offers four picture choices: Happy, Sad, Angry and Scared.
4. **Right answer:** star and praise. **Wrong answer:** a heart, a gentle hint and *Try again* or *Continue*.
5. Control returns to the child and a toast invites further exploring.

Every answer is recorded with:
- the selected and correct answer
- correctness
- the attempt number
- response time in ms
- a UTC timestamp, session and player IDs

Results are saved to `numu_assessment_results.json` (in `Application.persistentDataPath`) and sent to the host app (see `REACT_NATIVE_INTEGRATION.md`). A correct answer also completes map level Park › 4 when it is unlocked.

## Code map

| Area | Files |
|---|---|
| Player, camera, input | `Scripts/Runtime/World/NumuPlayerController.cs`, `NumuFollowCamera.cs`, `NumuVirtualJoystick.cs` |
| Interaction | `NumuInteractable.cs`, `NumuInteractionSystem.cs` |
| Characters | `NumuNpc.cs` (idle variety, look-at and hand IK, strolling), `NumuFacialExpressions.cs` (6 emotions, blink, talk) |
| Activity | `NumuEmotionScenario.cs`, `NumuWorldUI.cs`, `NumuAssessment.cs`, `NumuScenarioCatalog.cs` |
| Host bridge | `NumuBridge.cs`, `Assets/Plugins/Android|iOS` |
| Rendering | `NumuWorldRenderSettings.cs` (per-tier shadow distance, cascades and resolution) |
| Builders (Editor) | `Scripts/Editor/World/NumuParkBuilder*.cs`, `NumuWorldUIBuilder.cs` (UI, shared with the indoor worlds), `NumuCharacterSetup.cs`, `NumuPolyHavenSetup.cs`, `NumuWorldMaterials.cs`, `NumuAssetImportRules.cs` |

The Home, School and Supermarket worlds are described in `INDOOR_WORLDS.md`.

## Facial expressions

The faces use the 52 ARKit-style blendshapes plus FACS action units that ship with the Rocketbox `_facial` meshes. Each emotion combines mouth, brow and eye cues (`NumuCharacterSetup.ConfigureFace`). A preview sheet is in `Screenshots/Park/12_facial_expressions_six_emotions.png`. Expressions are tuned to be clear but not exaggerated; review them with your clinical team.

## Measured cost (Editor, 1920×1080)

These numbers were measured in the Editor, not on a device.

| Tier | View | Triangles | Draw calls | SetPass |
|---|---|---|---|---|
| PC | arrival | 904k | 521 | 69 |
| PC | playground | 602k | 339 | 68 |
| Mobile | arrival | 454k | 268 | 55 |
| Mobile | playground | 364k | 208 | 48 |

What was done for mobile:
- LOD groups on trees and props; a low-poly lantern LOD1 replaces the 20k-triangle scanned lamp.
- No shadow casting for foliage, lamp heads or terrain.
- Light-probe lighting for thin and small meshes.
- Baked indirect lighting, AO and a reflection probe.
- ASTC texture overrides; Rocketbox textures capped at 1024 on mobile.
- The camera skips the depth and opaque textures.
- Mobile tier: 35 m shadow distance, 2 cascades, 1024 shadow map.

30 FPS on a mid-range phone is **not yet verified**; profile a development build with the Unity Profiler on target devices.

## Re-importing source assets

The processed assets are committed under `Assets/ThirdParty`, so you don't need these scripts to build. To refresh the sources:

```bash
python3 Tools/AssetPipeline/download_assets.py AssetSources        # MIT / CC0 downloads → AssetSources/ (git-ignored)
python3 Tools/AssetPipeline/process_assets.py AssetSources .        # TGA→PNG, mask packing, leaf/grass atlases → Assets/
```

Needs Python 3 with Pillow and NumPy. Then run **NUMU → Worlds → Build Park World**.
