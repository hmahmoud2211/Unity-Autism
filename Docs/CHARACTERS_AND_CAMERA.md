# NUMU characters and camera

## NUMU cast style: one character family

**Main style reference:** `Assets/References/Character_Expression_Reference.png`, a JeStiMulE boy, surprised. Every human character follows it. The older `Character_Style_Reference.png` is kept for history.

All twelve human characters are Microsoft Rocketbox avatars (MIT), restyled by one pipeline. They keep their humanoid rigs, animations and all 175 face blendshapes, so idle, walk, sit and interaction clips and every expression work unchanged. The imported FBX files and textures are never modified.

### What is shared (the "construction") and what varies

| Shared by every character | Varies per character |
|---|---|
| **Eyes:** large and clear, with white sclera, a big round iris with a dark rim, a black pupil and a highlight, and a bold upper lash line. They are also physically enlarged in the mesh (×1.22 for children, ×1.16 for adults). | iris colour |
| **Brows:** thick, gently arched strokes from one template placed relative to the eyes, dark brown at most | brow colour; slightly thinner for women and girls |
| **Nose and mouth:** simple, with a soft mouth in a muted skin tone | skin tone (each keeps their own, harmonised toward the reference's hue) |
| **Skin:** flat, softly shaded, no pores or baked photo lighting | — |
| **Hair or headscarf:** a few clean colours with broad shading. Short painted hair gets a modelled hair cap (a mesh shell); adults keep their hair cards, recoloured. | hair colour and style; headscarves |
| **Clothes:** a few flat colours with broad shading, with no prints, denim fades or fabric texture | clothing colours and garments |
| **Materials:** matte URP Lit (smoothness 0.12–0.2), no normal or detail maps | — |
| **Head:** larger than realistic (children ×1.1–1.2, adults ×1.05) | — |
| **Expressions:** the same presets, head poses and hand poses (below) | — |

### The cast

| Character | Avatar | Role and worlds |
|---|---|---|
| **Main character** (player) | `Char_NUMU_Boy` (from Male_Child_01) | All four worlds. Ginger hair with a side-swept fringe, dark brown eyes, a green crew-neck sweater with tonal stripes. Large, round head. |
| Mia | Female_Child_01 | Park (swing), School |
| Lina | Female_Child_02 | Park (activity), School; headscarf |
| Omar | Male_Child_02 | Park, School |
| Mum | Female_Adult_06 | Home; headscarf |
| Dad | Male_Adult_08 | Home |
| Ms. Sara, teacher | Female_Adult_14 | School |
| Nora, cashier | Female_Adult_12 | Supermarket |
| Mr. Karim | Male_Adult_13 | Supermarket (plain sweater) |
| Shop assistant / park helper | Male_Adult_02 | Supermarket, Park |
| Shopper / grown-up in the park | Female_Adult_01 | Supermarket, Park |
| (spare boy) | Male_Child_01 | Not placed in a world |

Park and Supermarket use prefab instances, so they update automatically. Home and School were rebuilt so they use the main character as the player.

### Pipeline

To restyle the cast, or add a future human character:
1. **`NumuCastFeatures`** (Editor) measures each avatar's facial features from its rig: brows, eyes, eyelid openings, mouth, cheeks and nose, as UV positions. It writes them to `Tools/AssetPipeline/features/<Avatar>.json`.
2. **`Tools/AssetPipeline/stylize_character.py`** repaints every head, body and hair-card texture with the shared construction. The `CAST` table holds only colours and a few switches (headscarf, skin and clothing tolerances). It writes the textures to `Assets/NUMU/Characters/Stylized/Textures/` and the chosen colours to `features/<Name>_style.json`.
3. **`NumuCharacterSetup.SetupAll`**:
   - points each avatar's materials at its restyled textures
   - builds a derived head mesh with **`NumuStylizedHead`** (enlarged eyes, plus a hair shell for short hair), saved as `<Avatar>_Meshes.asset`
   - sets the head size and resting face (`ApplyCastStyle`)
   - adds the expression components

New character: add it to `CAST`, export its features, run the script, then run SetupAll.

### Expressions: one system for every character

**`EmotionController`** is the reusable entry point:
- `Show(emotion, intensity, withGesture)`
- `Hands(...)`
- `Perform(...)`
- `ReleaseGesture()`
- `LookAt(...)`

It drives three layers, and each one works on any humanoid on top of any clip:
1. **Face:** blendshape presets in `NumuFacialExpressions` (`NumuCharacterSetup.ConfigureFace`), shared by every Rocketbox face. VRoid faces have their own presets.
2. **Head pose:** after animation, a tilt, a lowered head or a raised head, split between neck and head.
3. **Hands:** arm IK in the animator's IK pass, with the hand orientation set exactly afterwards. The face is located from the eye bones, so the pose fits any head size.

| Emotion | Face | Head | Hands |
|---|---|---|---|
| Neutral | calm, eyes slightly wide | — | — |
| Happy | broad smile with parted lips, raised cheeks, slightly narrowed eyes, lifted brows | chin up a little, friendly tilt | (NPCs: wave) |
| Sad | inner brows up, mouth corners down, lowered lids and gaze | lowered | — |
| Angry | brows down and together, narrowed eyes, pressed lips | lowered, glaring | — |
| Surprised | whole brow raised, eyes wide, a round open "o" | back a little | **both hands to the face**, as in the reference |
| Afraid | brows raised and drawn together, eyes wide, lips stretched; child-friendly, not scary | down and slightly away | hands up in front of the chin, palms out |
| Disgust | nose wrinkled, upper lip raised, brows lowered | turned away | — |
| **Confused** (new) | one brow up, one down; mouth pulled to one side and pursed | clear tilt toward the hand | **scratching the head** |

**Brow lowering is kept low in Angry:** at full weight, `AU_04` and `SR_04` drag the whole upper forehead and the hairline unevenly. `BrowDown` alone lowers the brows cleanly.

### Test scene

**`Assets/Scenes/NUMU_CharacterTest.unity`** (menu **NUMU → Characters → Build Character Test Scene**):
- The main character on a town square, showing **Surprised with hands** on Play.
- The whole cast in a lineup beside him, at x 6–15.
- Keys **1–8** switch everyone's emotion: Neutral, Happy, Sad, Angry, Surprised, Afraid, Disgust, Confused.
- **H** toggles the hands.

### Review screenshots

All are from Unity Play Mode, in `Screenshots/Character_Cast/`:
- `01_main_character_vs_reference.jpg`: the reference, the main character surprised, and neutral.
- `02_main_character_expressions.jpg`: all eight expressions.
- `03_cast_neutral / happy / surprised / sad.jpg`: the whole cast showing the same emotion.
- `04_cast_faces.jpg`: neutral face close-ups of the cast.
- `05_worlds.jpg`: the cast in Home, School, Supermarket and Park gameplay. Console: 0 errors, 0 warnings in all four.
- `06_children_expressions.jpg`: Lina and Mia in seven emotions.

### Compared with the main reference

**Matches:**
- the costume and palette: ginger cap with fringe, green tonal-striped crew-neck, pale warm skin
- large eyes with clear whites
- thick dark-brown arched brows
- soft matte shading
- the surprised face with hands to the face and a round open mouth
- clear, child-friendly expressions
- one consistent look across children and adults

**Still different. These need custom modelling (Blender) or new assets:**
- **Face structure:** the reference has rounder cheeks, a fuller cranium, a smaller, softer nose and big ears. NUMU faces are the Rocketbox faces with enlarged eyes and scaled heads, so noses and jaws stay more realistic and adult faces stay adult-proportioned.
- **Irises:** the reference's are bigger and near-black. NUMU's are large but still a natural size.
- **Hair:** short hair is a smooth cap. The reference has a sculpted side part and a tuft. Adults keep their original hair-card shapes (recoloured), which read less simple than a modelled hair mesh.
- **Clothes:** these are the original garments flattened, not re-modelled. Some keep a simple print (Omar's "38"), and some have remaining fold shading.
- **Hands:** fingers keep the animation's relaxed curl. The reference spreads the fingers on the face. Afraid's palms-out hands look slightly stiff.

**Needs Blender or a character artist for an exact match:**
- one stylized base head (round cheeks, small nose, larger eye sockets and ears) with the same ARKit/Rocketbox blendshape names, so `NumuFacialExpressions` keeps working, re-used across the cast
- sculpted hair meshes
- simple re-modelled garments
- finger poses or a "hands to face" clip

## Anime characters (VRoid / VRM): optional, off by default

The art direction is semi-realistic humans (JeStiMulE style), so `NumuCharacterSetup.UseAnimeCharacters` is **false**: every world uses the Rocketbox human characters. The VRM pipeline below still works when it is turned on.

**Pipeline:** VRoid Studio exports `.vrm`. UniVRM v0.131.4 imports it into Unity 6 as VRM 1.0, migrating VRM 0.x files, with URP MToon10 materials. `NumuCharacterSetup.SetupVrmCharacters` then:
- builds a NUMU prefab in `Assets/NUMU/Characters/Char_VRM_<id>.prefab` with the shared humanoid animator
- adds VRoid face presets
- merges sub-meshes that share a material (draw calls)

The imported `.vrm` files are never modified. To add a character:
1. Put `<Id>/<Id>.vrm` under `Assets/ThirdParty/VRoid/`.
2. Add `<Id>` to `VrmFemale` in `NumuCharacterSetup`.
3. Rebuild the world.

**Imported now:** two official VRoid Project samples, both licensed for commercial use, modification and redistribution with no credit required; the licence is read from each file's metadata.
- **Sendagaya Shino:** the player in all four worlds. 31k triangles, 17 materials.
- **Sakurada Fumiriya:** the Park's anime visitor. 40k triangles, 17 materials.

Both are 1.65–1.9 m: **teen/young-adult characters, not children.** They are not resized to pose as children.

**Faces:** VRoid faces carry joy, angry, sorrow, fun and surprised shapes for brows, eyes and mouth, plus blink and lip-sync shapes. `NumuFacialExpressions` combines them:

| Emotion | VRoid shapes |
|---|---|
| Happy | brows fun, eyes joy (55%), mouth joy |
| Sad | brows sorrow, eyes sorrow, mouth sorrow, mouth down |
| Angry | brows angry, eyes angry, mouth angry |
| Fear (custom: VRoid has none) | brows sorrow, a little brow surprised, eyes surprised, mouth slightly open and stretched |
| Surprise | brows surprised, eyes surprised, mouth surprised |

Blinking uses `Fcl_EYE_Close`; talking uses `Fcl_MTH_A`. The component runs after UniVRM's runtime (execution order 11100 > 11000), so NUMU controls the face while UniVRM keeps the hair and clothes spring bones. Sheets: `Screenshots/Anime/10_anime_face_*.jpg` (in-game) and `20_expression_sheet_preview.jpg`. Fear and surprise differ mainly in brows and mouth shape; have the clinical team review them.

**Still missing: anime children.** No licensed anime child model was available. The children the child must read (Mia, Lina, Omar…) stay on the expressive Rocketbox child models until anime children exist. To make them:
1. Install VRoid Studio (free): https://vroid.com/en/studio
2. Create each child, using a shorter body height, larger head and child face presets.
3. Export as **VRM 1.0** (or 0.x). In the export dialog, reduce materials (atlas) and polygons to keep mobile cost down.
4. Drop the file in `Assets/ThirdParty/VRoid/<Id>/<Id>.vrm` and tell me. Faces are set up automatically and checked with the inspector.

**Mobile cost (Editor, Park start, Mobile tier):**
- **Before anime characters:** 454k triangles, 268 draw calls.
- **After, with the sub-mesh merge:** 647k triangles, 362 draw calls.
- **Without the merge it was 632 draw calls:** the player had 67 slots, now 17.

The VRoid samples are heavy for phones. Re-export them, or any new characters, from VRoid Studio with fewer polygons, a material atlas and 1024 textures before adding many on screen. Device frame rate is not tested.

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
