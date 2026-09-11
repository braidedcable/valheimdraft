# ValheimDraft

BepInEx plugin that dumps Valheim building-piece dimensions and snap points
(not exposed by any existing wiki or community JSON dump) to a JSON file, for
use by a separate web-based building planner.

Numeric spatial data only — no Iron Gate textures, meshes, or code are
extracted or redistributed.

## Build

```
export VALHEIM_INSTALL="/path/to/Valheim"   # dir containing valheim_Data/ and BepInEx/
dotnet build
```

Copy the built DLL into `$VALHEIM_INSTALL/BepInEx/plugins/` to run it.

## License

MIT (matches Jötunn, which this plugin depends on).
