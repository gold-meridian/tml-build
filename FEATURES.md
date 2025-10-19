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

TODO about options and settings

## launch profiles

The MSBuild SDK will generate two launch profiles (one for the client, one for the server), placed in your local `Properties/launchSettings.json`. These profiles will launch an SDK-provided bootstrapping launch wrapper that will be able to apply additional plugins (configured through your project file) to improve your development workflow.

TODO talk about feature plugins
