# features

**tml-build** has a lot of cool features...

## contemporary build system

It actually uses [MSBuild](https://en.wikipedia.org/wiki/MSBuild) instead of a mix between it and building directly in-game with a fake project.

This means you can actually use the latest features including modern C# language versions, source generators, etc. without issue.

Additionally:

- it makes it easier to handle multiple versions of tModLoader and building from CI environments,
- it's easier to develop multiple mods at once from a single solution file.

## proper reference support

It's trivial to reference both NuGet packages and raw assembly files without worrying about `dllReferences` and copying files into a `lib` directory.

### mod referencing

You can also directly reference mods from the workshop with minimal effort.

## configurable packaging

The user is given more control over how to package their mod into a `.tmod` file, including finer control over what files to include and better defaults.

### better `build.txt` format

It implements Mirsario's [proposed TOML rework](https://github.com/tModLoader/tModLoader/issues/4170), which is what's used for our custom configuration.

### better image packaging

Allows for images to be packaged as PNGs, allowing for smaller resulting file sizes.

## access transformers

ACCESS ALL THE THINGS!!
