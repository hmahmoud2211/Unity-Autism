# Third-party assets used in NUMU

All third-party content lives under `Assets/ThirdParty/` and was downloaded from the sources below.
Each folder also contains a `LICENSE.md`.

| Asset | Source | License | Used for |
|---|---|---|---|
| Microsoft Rocketbox avatars: Female_Child_01/02, Male_Child_01/02, Female_Adult_01/06/12/14, Male_Adult_02/08/13 (`*_facial.fbx` with 175 ARKit/FACS blendshapes) | https://github.com/microsoft/Microsoft-Rocketbox | MIT | Player child, Mia, Lina, Omar, park adults, Mum (06), Dad (08), Ms. Sara (14), Nora the cashier (12), Mr. Karim (13) |
| Rocketbox animations (42 clips: idle, walk, run, sit, sit at table, wave, cheer, laugh, clap, talk, listen, sad/nervous/angry body language, nod, trolley, waiting, documents) | same repository | MIT | Humanoid Animator controllers |
| Poly Haven models, Park: painted_wooden_bench, wooden_picnic_table, street_lamp_02, metal_trash_can, planter_box_01, flower_gazania, shrub_03, shrub_04, fern_02, football | https://polyhaven.com | CC0 | Park props and planting |
| Poly Haven models, Home: Sofa_01, modern_arm_chair_01, coffee_table_round_01, modern_wooden_cabinet, round_wooden_table_01, dining_chair_02, wooden_display_shelves_01, drawer_cabinet, potted_plant_01/02/04, throw_pillows_01, hanging_picture_frame_02, standing_picture_frame_01, wall_clock, modern_ceiling_lamp_01, electric_stove, vintage_electric_kettle, tea_set_01, wooden_bowl_01, wooden_cutting_board, rubber_duck_toy, chess_set, ceramic_vase_01 | https://polyhaven.com | CC0 | Home furniture and props |
| Poly Haven models, School: SchoolDesk_01, SchoolChair_01, metal_office_desk, steel_frame_shelves_01, book_encyclopedia_set_01, binder_notebook, stationery_supplies, standing_chalkboard_01, mounted_fluorescent_lights, wicker_basket_01 | https://polyhaven.com | CC0 | Classroom furniture and supplies |
| Poly Haven models, Supermarket: wooden_crate_01, plastic_crate_01, cardboard_box_01, long_life_food, bananas, food_apple_01, lemon, food_avocado_01, food_kiwi_01, yellow_onion, sweet_potato, food_pears_asian_01, hamburger_buns | https://polyhaven.com | CC0 | Produce crates, hero produce, bakery rolls, checkout items |
| VRoid Project sample characters: Sendagaya Shino, Sakurada Fumiriya (`Assets/ThirdParty/VRoid`) | https://opengameart.org/content/vroid-studio-cc0-models (VRoid Studio samples) | VRoid Hub licence v1 embedded in each .vrm: everyone, commercial use and modification allowed, redistribution allowed, credit not required | Anime player (Shino), anime park visitor (Fumiriya) |
| UniVRM v0.131.4 (com.vrmc.gltf, com.vrmc.vrm) | https://github.com/vrm-c/UniVRM | MIT | VRM import, URP MToon10 toon shader, spring bones |
| Cinemachine 6.6.0 | Unity package | Unity Companion License | Third/first-person camera |
| Poly Haven HDRI: farm_field_puresky (2K) | https://polyhaven.com | CC0 | Sky and reflections in all worlds |
| ambientCG materials, Park: Grass004, PavingStones138, Ground054, Rubber004, Bark006, Bark012, Leaf001 | https://ambientcg.com | CC0 | Terrain layers, bark, safety surface, leaf atlas |
| ambientCG materials, indoor worlds: WoodFloor051, PaintedPlaster017, Plaster001, Tiles107, Tiles040, Terrazzo005, Carpet016, Carpet012, Fabric030, Marble012, Wood049, Wood051, Metal032, Plastic010 | https://ambientcg.com | CC0 | Floors, walls, worktops, rugs, curtains, cabinets, metal |

Generated in-project (no external license):
- NUMU-style boy textures (`Assets/NUMU/Characters/Stylized`), repainted from the Rocketbox Male_Child_01 textures (MIT) by `Tools/AssetPipeline/stylize_character.py`.
- Leaf-cluster and grass-blade atlases (`Assets/NUMU/Worlds/Textures`), made from ambientCG Leaf001.
- Procedural trees, playground, swing chains and lantern LODs (`NumuRealisticKit`).
- Indoor architecture, kitchen cabinets, fridge, TV, shelving, chillers, checkouts, trolleys, products and produce (`NumuInteriorKit`, `NumuProductKit`).
- Signs, posters, whiteboard, children's drawings and the packaging atlas (`NumuSignKit`, text set in Liberation Sans, the font bundled with TextMeshPro). Product names and brands are invented.
- UI icons, including the picture-choice icons.

`decorative_book_set_01` was downloaded and then dropped: it needs one material per book (117 textures), which is too many draw calls for mobile.

Raw downloads are staged in `/AssetSources` (git-ignored, about 760 MB). They can be deleted; to re-download, run `Tools/AssetPipeline/download_assets.py` (see `Docs/PARK_WORLD.md`).
