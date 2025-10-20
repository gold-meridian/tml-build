# tml-build

> tModLoader mod development toolkit (SDK).
 
**tml-build** encompasses a CLI tool and MSBuild SDK which enable you to quickly and easily develop tModLoader mods.

It reimplements the entire build toolchain, allowing for strong control over how your mod is built and packaged, as well as what tML version your mod targets.

## features

> See: [FEATURES.md](FEATURES.md).

---

There are a lot, but here's a quick rundown:

- [x] no need to place the mod in `ModSources`, it can resolve tModLoader from *anywhere*,
- [x] easy tModLoader version referencing/switching,
  - [x] reference locally-installed `steam` or `dev` version, as well as `stable`, `preview`, or a custom version (`x.y.z.w`),
- [x] `.tmod` archive packaging on build,
  - [ ] configurable packing and compression,
  - [ ] configurable handling of image file formats (`.rawimg` vs PNG),
- [x] sophisticated launch profile generation,
  - [x] runs tModLoader through a program in charge of wrapping it and patching it for development use (see more in [FEATURES.md](FEATURES.md)),
- [x] built-in support for access transformers,
- [ ] optional support for the [proposed TOML rework](https://github.com/tModLoader/tModLoader/issues/4170) to `build.txt`,
- [x] much easier CI integration (no more boilerplate setup; the build system installs tML for you),
- [ ] easily supports NuGet dependencies without needing to copy references yourself,
- [ ] easily reference existing mods in your project by including their workshop IDs,
- [ ] type-safe references to all sorts of assets,
  - [ ] images,
  - [ ] sounds,
  - [ ] shaders (effects),
  - [ ] and localization,
- [and more!](FEATURES.md)

## what does a project look like?

Like this:

```xml
<Project Sdk="Tomat.Terraria.ModLoader.Sdk/1.0.0" />
```

or maybe like this:

```xml
<Project Sdk="Tomat.Terraria.ModLoader.Sdk/1.0.0">

</Project>
```

and sometimes, even like this:

```xml
<Project Sdk="Tomat.Terraria.ModLoader.Sdk/1.0.0">

    <!-- TODO: Add example referencing other mods. -->

</Project>
```

It's that simple; base configuration uses sane defaults that replicate tML.

## license

Source code is licensed under AGPL 3.0; your projects do not need to be under the same license to use this package (unless you replicate code). See [LICENSE](LICENSE) for more details.
