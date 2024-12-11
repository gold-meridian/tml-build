# tml-build

> awesome tmodloader build chain for awesome people
 
**tml-build** is a collection of NuGet packages meant to integrate into MSBuild to make tModLoader mod development WAY easier.

## but how?

A LOT OF WAYS:

- easily-referenceable assemblies that don't required hardcoded paths (`ModSources`),
- configurable `.tmod` archive packaging (optimize for packing speeds, size, etc.),
- access transformers to make slow reflection obsolete and to tell God that facts don't care about your feelings,
- optional better `build.txt` format that lets you preview a [proposed TOML rework](https://github.com/tModLoader/tModLoader/issues/4170),
- lets you package your images as PNGs rather than tML's "RawImg" format (PNG is better; trust me),
- much easier CI integration (no more annoying boilerplate to download the tModLoader developer environment),
- easily supports NuGet dependencies without needing to copy references yourself,
- easily reference existing mods in your project by including their workshop IDs,
- facilitates SANE and NORMAL project structures and ENCOURAGES you to use multiple projects,
- probably a lot more?

### ok... but what do these MEAN?

See: [FEATURES.md](FEATURES.md).

## license

Source code is licensed under AGPL 3.0; your projects do not need to be under the same license to to use this package (unless you replicate code). See [LICENSE](LICENSE) for more details.
