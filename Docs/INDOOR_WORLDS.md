# NUMU Home, School and Supermarket

Three realistic indoor worlds join the Park. Each has:
- a third-person child with a follow camera
- characters with facial expressions
- one social-emotional activity, reachable from the island map

| World | Scene | Map level that opens it | Activity id |
|---|---|---|---|
| Home | `Assets/Scenes/Worlds/NUMU_Home.unity` | Home › 4 *Recognize family emotions* | `home_mum_sad` |
| School | `Assets/Scenes/Worlds/NUMU_School.unity` | School › 4 *Class situations* | `school_lina_nervous` |
| Supermarket | `Assets/Scenes/Worlds/NUMU_Supermarket.unity` | Supermarket › 4 *Talk to cashier* and › 5 *Wait in line* | `market_checkout` |

All three scenes are in Build Settings. Rebuild a world with **NUMU → Worlds → Build Home / School / Supermarket World**. A rebuild regenerates the scene, its meshes, materials, generated textures and baked lighting, so edit the builder code rather than the scene. Each build takes about a minute, including a GPU lightmap bake of under 20 s.

## The worlds

**Home.** A 13×9 m open-plan family home:
- **Living room:** sofa, armchair, coffee table, media cabinet with a TV, toy shelves, scattered toys, rug, curtains.
- **Dining area:** round table, four chairs and a tea set.
- **Kitchen:** sage cabinets, marble worktop, sink, tiled splashback, fridge-freezer, stove, kettle and fruit bowl.
- **Garden:** visible through five windows, lit by late-afternoon sun.
- **People:** Mum sits at the table with her broken cup. Dad is on the sofa.

**School.** A calm, small special-needs classroom of 10×8 m:
- **Front:** whiteboard with the morning message, alphabet frieze, class rules and numbers chart.
- **Room:** six desks with story sheets and pencils, the teacher's desk, a cork board of children's drawings, and supply shelves.
- **Calm corner:** rug, bean bags, and a *How do I feel today?* chart that uses the same emotion faces as the activity.
- **People:** Ms. Sara (teacher), Lina standing nervously at the front, Mia and Omar at their desks.

**Supermarket.** A 24×18 m store:
- **Front:** glass storefront with open sliding doors and a car park outside.
- **Aisles:** four stocked double-sided aisles (about 4,500 products with printed names and shelf price strips).
- **Fresh and chilled:** 20 m of lit glass-door fridges, a bakery, and a produce section with 12 crates of fruit and vegetables.
- **Checkouts:** two lanes with belts, scanners, screens and card readers, plus trolleys and baskets.
- **People:**
  - Nora (cashier)
  - a shopper paying
  - Mr. Karim, impatient in the queue with his trolley
  - a shop assistant

The floor and colours are deliberately calm and low-contrast. For example, the school uses plain planks instead of a speckled floor, and nothing flashes or makes sudden sounds.

## The activities

All three run on `NumuConversationScenario`, a data-driven script of steps:
- dialogue lines
- expression and animation changes
- "How is … feeling?" emotion questions
- picture-choice "What could you do?" questions

Each activity works like this:
- The child walks up and taps the prompt.
- The camera frames the character's face while they talk.
- For every question, a wrong answer gets a gentle hint with **Try again** or **Continue**; a right answer gets praise.
- The character's face then changes, for example Mum's sad face becomes a smile after the hug.

| Activity | Emotion question | Response question(s) |
|---|---|---|
| `home_mum_sad` | How is Mum feeling? → **Sad** | Mum is sad. What could you do? → **Give Mum a hug** (vs laugh / walk away / shout) |
| `school_lina_nervous` | How is Lina feeling? → **Scared** | What could you say to Lina? → **"You can do it! I'll listen."** (vs laugh / "Hurry up!" / say nothing) |
| `market_checkout` | How is Mr. Karim feeling? → **Angry** | What should you do while waiting? → **Wait calmly for my turn**; Nora says hello, what do you say? → **"Hello! Thank you."** |

**Recording.** Every answer is saved and sent to the host app, as in the Park (see `REACT_NATIVE_INTEGRATION.md`). Each record holds:
- the question
- the correct and selected answer
- correctness
- the attempt number
- response time in ms

**Map progress.** The level that launched the world is marked complete once every question ended with the right answer, but only if that level was unlocked.

## Code map

| Area | Files |
|---|---|
| Activity runtime | `Scripts/Runtime/World/NumuConversationScenario.cs`, `NumuWorldUI.cs` (`AskChoice` picture questions) |
| Routing | `NumuBridge.cs` → `NumuSceneRouter` (`home`, `school`, `supermarket`, `park`; `PendingLevel`), `NumuScenarioCatalog.cs` |
| Shared builders | `Scripts/Editor/World/NumuWorldScaffold.cs` (scene, meshes, props, characters, player rig, lights, probes, bake), `NumuWorldUIBuilder.cs` (world UI, also used by the Park), `NumuInteriorKit.cs` (walls with openings, windows, doors, cabinets, curtains), `NumuInteriorMaterials.cs`, `NumuSignKit.cs` (signs and posters with real text), `NumuProductKit.cs` (packaging atlas, products, produce) |
| World builders | `NumuHomeBuilder.cs`, `NumuSchoolBuilder.cs`, `NumuSupermarketBuilder.cs` |
| QA | `NumuScenarioTestDriver.cs` plays an activity in Play Mode: wrong answer, hint, retry, correct answer, with screenshots |

## Mobile cost (measured in the Editor, 1920×1080)

The Mobile tier was simulated inside Play Mode with:
- the `Mobile_RPAsset` pipeline
- Shadowmask mode
- Mesh LOD threshold 1.6

Entering Play Mode on the macOS target always selects the PC quality level, so these settings were applied by hand.

| World | View | Triangles | Draw calls | SetPass | Realtime shadow casters |
|---|---|---|---|---|---|
| Home | start | 351k | 165 | 58 | 5 |
| Home | activity | 256k | 110 | 56 | 11 |
| School | start | 187k | 116 | 47 | 6 |
| School | activity | 134k | 110 | 48 | 9 |
| Supermarket | entrance | 425k | 137 | 55 | 13 |
| Supermarket | checkout | 150k | 94 | 46 | 10 |

What keeps it down:
- **Shadowmask lighting.** Static shadows are baked. On the Mobile tier only characters cast realtime shadows. The PC tier keeps realtime shadows within 30 m (Distance Shadowmask).
- **Unity Mesh LOD** on scanned props, enabled at import (`NumuAssetImportRules`), and a stronger LOD threshold on the Mobile tier.
- **Cheap background:** lite tree canopies outside the windows with no shadows.
- **Shared materials:** one material for all supermarket stock and one for all produce.
- **Probe lighting** for small props.
- **Box-projected reflection probes** and light-probe grids.

Like the Park, **30 FPS on a real phone has not been verified.** Profile a development build on target devices.

## Known limits

- The scanned furniture comes from Poly Haven (CC0), so the styles don't all match. The sofa is classic while the kitchen is modern.
- Some props are procedural because no free model exists: kitchen cabinets, fridge, TV, shelving, products, trolleys and checkouts.
- Characters are Rocketbox (MIT) mid-poly scans. Lina, Mia and Omar re-use the Park children.
- There's no hug animation. The response is shown through dialogue and Mum's expression.
- The child can replay an activity by walking back to the prompt.
