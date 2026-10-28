# NUMU Park World

Scene: `Assets/Scenes/NUMU_Park.unity` (in Build Settings).
Rebuild: menu **NUMU → Worlds → Build Park World**, or call `Numu.EditorTools.World.NumuParkBuilder.Build()`.
Rebuilding regenerates the whole scene, prefabs, materials and baked lighting. Edit the builder code, not the scene, or your manual changes will be overwritten.

## How to play

- **From the island map:** Park › Level 4 *Identify emotions* › Play. The level must be unlocked, i.e. levels 1–3 finished.
- **Directly:** open `NUMU_Park.unity` and press Play.
- **Controls:** WASD or arrow keys (Shift to walk faster), a gamepad stick, or the on-screen joystick on touch devices. Space, E, Enter or the prompt button interacts. **V** or the view button switches between third and first person, and dragging on the right of the screen (or holding the right mouse button) looks around. See `CHARACTERS_AND_CAMERA.md`.

## The activity: two friends at the swings (`park_swing_happy`)

Modelled on JeStiMulE's in-context social scenes, built with the reusable `NumuConversationScenario` cutscene system:

1. Mia is swinging and Lina waits for her turn in front of the swings. When the child comes within about 7 m, Lina smiles and waves (`NumuNpc` greeting).
2. Walk up and press **Say hi to Lina**. Movement pauses and Lina turns to the child with a stepping turn.
3. The cutscene camera opens on an over-the-shoulder shot of both girls, then cuts to a close-up of Lina smiling and talking.
4. **How is your friend feeling?** offers four colour-coded picture cards (Plutchik colours, as in JeStiMulE: yellow happy, blue sad, red angry, green scared).
5. **Right answer:** calm praise. **Wrong answer:** a gentle hint with *Try again*.
6. The child answers Lina, she claps, and play resumes in the same view (third or first person).

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
| Activity | `NumuConversationScenario.cs` (cutscene steps, group and close-up shots), `EmotionController.cs` (face + gesture + gaze), `NumuWorldUI.cs`, `NumuAssessment.cs`, `NumuScenarioCatalog.cs` |
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

## Compared with JeStiMulE

JeStiMulE's own text says it shows nine expressions:
- happiness, anger, disgust, fear, sadness and surprise
- pain
- neutral and a "funny face"

Its figures show:
- **Characters:** stylised semi-realistic humans.
- **Camera:** close third person behind the avatar.
- **Places:** a cobbled European town and a grassy park.
- **Cutscenes:** social scenes played as cutscenes, followed by a colour-coded answer bar.

Source: Serret et al., *Molecular Autism* 2014, 5:37, open access, PMC4094670. The YouTube video itself couldn't be viewed.

**Now matched in NUMU's Park:**
- semi-realistic human child player and NPCs, with no anime or mascot characters
- realistic scale and a close third-person camera (3.8 m, 1.9 m high)
- seven facial emotions, now including disgust (`EmotionController` / `NumuFacialExpressions`)
- an NPC greeting gesture
- a cutscene with a group shot and then a close-up
- Plutchik colour-coded answers
- calm feedback

**Still different:**
- JeStiMulE's characters have more cartoon-like faces. NUMU's Rocketbox faces are more realistic, which you asked for.
- JeStiMulE's outdoor scenes sit in a town with building facades and streets. The NUMU Park is open parkland, so a town edge with a street, sidewalk and houses would be the next step.
- Pain and "funny face" expressions are not implemented.
- There is no puzzle-piece reward.
