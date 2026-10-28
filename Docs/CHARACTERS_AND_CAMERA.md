# NUMU characters and camera

## Realistic Modern Character Pack: status

**Not imported yet.** The pack (publisher Chandan kumar singh) is a paid asset: $29.99, Standard Unity Asset Store EULA.
- Link: https://assetstore.unity.com/packages/3d/characters/realistic-modern-character-pack-329789
- It isn't in this project or in the local Asset Store download cache.
- Unity has no supported scripting API for buying or downloading Asset Store purchases, so this step is manual:
  1. Buy the pack with the Unity account you use in the Editor.
  2. In Unity: **Window → Package Manager → My Assets → Realistic Modern Character Pack → Download → Import**.
  3. Run **NUMU → Characters → Inspect Realistic Modern Character Pack**.

The inspector checks every character model in the pack and writes `Docs/CHARACTER_PACK_REPORT.md`:
- valid humanoid rig
- height (adult, or child under 1.55 m)
- triangles and bones
- facial blendshapes, and how many of the six emotions they can show with NUMU's presets (`NumuFacialExpressions.SupportedEmotionCount`)
- non-URP shaders
- texture sizes

The store page doesn't state what the pack contains: no character count, no children, no facial rig, no animations, no LODs. Decide on the integration only from the inspector report.

**Until then**, NUMU's people are Microsoft Rocketbox avatars (MIT). The inspector run on `Assets/ThirdParty/Rocketbox/Avatars` reports:
- 11 models, all with a valid humanoid rig
- 175 facial blendshapes each, all six emotions supported
- 6.7k–8.8k triangles, 80 bones
- URP materials only

The four children (1.44–1.46 m) are real child models, not shrunken adults. They should keep the emotion-recognition roles unless the pack proves to have expressive child faces.

**Plan once the pack is in:**
- Use its adults for parents, teachers, shop staff and park visitors, as prefabs in `Assets/NUMU/Characters/`. The pack's own files stay untouched.
- Adult faces the child is asked to read need blendshapes. If the pack has none, those roles stay on Rocketbox faces, or the pack models get an ARKit-compatible facial rig.
- The emotion component already accepts standard ARKit names (`mouthSmileLeft`, `browInnerUp`…) as well as Rocketbox's names.

**Licensing.** Under the Standard EULA the pack's source files must not be published in a public repository. Every collaborator who opens the project needs their own licence. This repository has no remote. If one is added and made public, git-ignore the pack's folder.

## Animation

Humanoid clips from Rocketbox (MIT), 50 in total:
- idle, breathing and looking around
- walk and run (one blend tree for the player)
- **turn left/right 90°**, used by NPCs when they turn to face the child, so their feet step instead of sliding
- **sit down / stand up**, plus sitting on chairs and at tables
- wave, talk (neutral, excited, sad, nervous), listen, nod, clap, cheer, laugh, angry and nervous idles
- trolley, holding a bag, documents, waiting

**Needs Mixamo (your Adobe login).** These have no free equivalent:
- **Pointing** (Mixamo "Pointing")
- **Picking up** ("Picking Up")
- **Handing over** ("Giving Item")

Download them as FBX for Unity, *Without Skin*, *In Place*, into `Assets/ThirdParty/Mixamo/`. Then add them as states in `NumuCharacterSetup.BuildNpcController`, the same way as the Rocketbox clips.

## Camera: one child, two views

`NumuFollowCamera` (on the Main Camera) is built on **Cinemachine 6.6** (official package). It creates three virtual cameras at runtime, and the Cinemachine brain blends between them (0.6 s, instant with Reduced Motion):

- **Third person (default):**
  - orbits the child: drag to look, wheel to zoom from 1.8 to 6.5 m
  - drifts back behind a walking child after 2.5 s without manual looking
  - wall collision via a Deoccluder; furniture and people on the Ignore Raycast layer don't push it
  - if a wall pushes the camera within 0.7 m of the head, the child's body is hidden so the view never enters the head
- **First person:**
  - eye height measured from the child's head bone (about 1.33 m), steady with no head bob, 12 cm ahead of the face
  - pitch limited to −60…+70°
  - the body turns with the view; it is hidden but still casts its shadow
- **Conversation shots:** the activities' face close-ups. The chosen view (first or third person) resumes afterwards.

| | Desktop | Touch | Gamepad |
|---|---|---|---|
| Move | WASD / arrows (Shift = faster) | left joystick | left stick |
| Look | right mouse button drag, or drag on the right of the screen | drag on the right 60% of the screen | right stick |
| Zoom (third person) | mouse wheel | none | none |
| Switch view | **V** or the view button (top right) | view button | north button (Y/△) |
| Interact | E / Space / Enter or the prompt | the prompt button | south button |

**Settings** (`NumuSettings`): `LookSensitivity` (0.3–2.5) and `PreferFirstPerson`, both remembered between sessions. A slider for sensitivity in the map's settings panel is not built yet.

## Tests run (Play Mode, Editor)

- **Park, the full gameplay test from the brief:**
  - walked 7.8 m in third person with the touch joystick (Locomotion state)
  - switched to first person (eye at 1.33 m, body shadow-only)
  - walked up to Mia; prompt; face close-up; wrong answer, hint, retry, correct answer, all recorded
  - first person still active afterwards
  - console: 0 errors, 0 warnings
- **Home, School, Supermarket:** activities pass in third person. The Supermarket activity also passes when started in first person, returning to first person.
- **Drag-look and limits:** 300 px ≈ 49° in both views; first-person pitch clamps at 60° up.
- **Wall collision:** the camera stays inside the Home at four tight spots (against walls, in corners, beside windows).

Not tested: real phones, Unity as a Library builds, frame rate on a device.
