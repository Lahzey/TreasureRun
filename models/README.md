# models

Our own MagicaVoxel models (`.vox`, optional `.json` sidecar). They're copied to `BepInEx/plugins/TreasureRun/models` on every build.

- `overrides/`: replacements for vanilla meshes (the sidecar `Source` says which one).
- Anything else: loaded by name from code, e.g. `planes/strike_fighter`. Generated from code by VoxTool recipes (`castles/`, `planes/`; ascensions will go to `ascensions/<tower>/`).

Don't commit unmodified vanilla models extracted from the game. See [../docs/voxel-workflow.md](../docs/voxel-workflow.md).
