# Third-party assets used in NUMU

All third-party content lives under `Assets/ThirdParty/` and was downloaded from the sources below.
Each folder also contains a `LICENSE.md`.

| Asset | Source | License | Used for |
|---|---|---|---|
| Microsoft Rocketbox avatars: Female_Child_01/02, Male_Child_01/02, Female_Adult_01, Male_Adult_02 (`*_facial.fbx` with 175 ARKit/FACS blendshapes) | https://github.com/microsoft/Microsoft-Rocketbox | MIT | Player child, Mia, Lina, Omar, park adults |
| Rocketbox animations (20 clips: idle, walk, run, sit, wave, cheer, laugh, clap, talk, listen) | same repository | MIT | Humanoid Animator controllers |
| Poly Haven models: painted_wooden_bench, wooden_picnic_table, street_lamp_02, metal_trash_can, planter_box_01, flower_gazania, shrub_03, fern_02, football | https://polyhaven.com | CC0 | Park props and planting |
| Poly Haven HDRI: farm_field_puresky (2K) | https://polyhaven.com | CC0 | Sky and reflections |
| ambientCG materials: Grass004, PavingStones138, Ground054, Rubber004, Bark006, Bark012, Leaf001 | https://ambientcg.com | CC0 | Terrain layers, bark, safety surface, leaf atlas |

Generated in-project (no external license):
- Leaf-cluster and grass-blade atlases (`Assets/NUMU/Worlds/Textures`), made from ambientCG Leaf001.
- Procedural trees, playground, swing chains and lantern LODs (`NumuRealisticKit`).
- UI icons.

Raw downloads are staged in `/AssetSources` (git-ignored, about 760 MB). They can be deleted; to re-download, run `Tools/AssetPipeline/download_assets.py` (see `Docs/PARK_WORLD.md`).
