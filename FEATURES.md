# Features

**tml-build** is packed with many useful features for more efficient mod development. These are primarily offered as part of the MSBuild SDK and its integration.

## version management

You may select between `stable`, `preview`, `steam`, `dev`, or a user-input `x.y.z.w` version format.

- `stable` and `preview` pull from the latest non-preview and preview GitHub releases respectively,
- `steam` will reference your local Steam installation (`tModLoader`),
- `dev` will reference your local development instance (`tModLoaderDev`),
- `x.y.z.w` will match a known GitHub release of the same version.

The list of versions is cached and only periodically updated.

As a benefit of this system, you do not need to clone projects to `ModSources/` any longer, the SDK will resolve the paths from any location. CI will also be automatically supported assuming it is sufficiently able to make web requests and handle downloads, as the SDK will download any necessary files to build against a copy of tModLoader.

You may update the cache with:

```
$ tml-build version cache
```

and may forcefully update it with:

```
$ tml-build version cache -f
```

The MSBuild SDK will also periodically refresh it in the background.

## mod packaging

Like tModLoader, the MSBuild SDK will pack your compiled mod into a `.tmod` archive.

## launch profiles

The MSBuild SDK will generate two launch profiles (one for the client, one for the server), placed in your local `Properties/launchSettings.json`. These profiles will launch an SDK-provided bootstrapping launch wrapper that will be able to apply additional plugins (configured through your project file) to improve your development workflow.

Below is a list of features, associated arguments, and a description of their function.

| plugin id            | arguments                    | description                                                                                                                                                                                                                                                                                                                                                                                   |
|----------------------|------------------------------|-----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------|
| `tomat.enablemod`    | none (uses built-in `--mod`) | Ensures that the given mod is always enabled at launch, even if an exception forcefully disabled it and no rebuild was trigged on re-launch.                                                                                                                                                                                                                                                  |
| `ppeb.netcoredbg`    | none                         | Patches assembly loading to attempt to load from disk if a PDB path is known and can be resolved to a matching assembly path. Fixes an issue with loading PDBs in-memory in netcoredbg and may improve hot reload experience, since both the assembly and PDB will be loaded from disk instead. Adapted from [ppebb/tml-netcoredbg-patcher](https://github.com/ppebb/tml-netcoredbg-patcher). |
| `lolxd87.splashskip` | none                         | Reduces initial JIT work and forcefully skips through the splash screen to launch the game faster. Adapted from [EtherealCrusaders/tml-debug-quickstart](https://github.com/EtherealCrusaders/tml-debug-quickstart).                                                                                                                                                                          |

## asset references

Source generators are provided which can understand your assets and localization keys, parsing them into type-safe references that are usable at compile time. This lets you reliably access their values without worrying about missing assets, for example.

The generated types are `{RootNamespace}.Core.AssetReferences` and `{RootNamespace}.Core.LocalizationReferences`, but they are given global usings so you can access them directly from anywhere.

