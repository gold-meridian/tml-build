using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Loader;
using Microsoft.Extensions.DependencyModel;
using Microsoft.Extensions.DependencyModel.Resolution;

namespace Tomat.TML.ClientBootstrap.Framework;

public sealed class AssemblyResolver
{
    private sealed class NaiveAssemblyPathResolver(string basePath) : ICompilationAssemblyResolver
    {
        public bool TryResolveAssemblyPaths(CompilationLibrary library, List<string>? assemblies)
        {
            var fullPath = Path.Combine(basePath, library.Name, library.Version, library.Name + ".dll");
            {
                fullPath = TryToCorrectCasingIGuess(fullPath);
            }

            if (File.Exists(fullPath))
            {
                assemblies?.Add(fullPath);
                return true;
            }

            return false;
        }
    }

    private sealed class AppBaseCompilationAssemblyResolverWithStrongPathCoverage(string basePath) : ICompilationAssemblyResolver
    {
        private const string refs_directory_name = "refs";

        public bool TryResolveAssemblyPaths(CompilationLibrary library, List<string>? assemblies)
        {
            var isProject = string.Equals(library.Type, "project", StringComparison.OrdinalIgnoreCase) ||
                            string.Equals(library.Type, "msbuildproject", StringComparison.OrdinalIgnoreCase);

            var isPackage = string.Equals(library.Type, "package", StringComparison.OrdinalIgnoreCase);
            var isReferenceAssembly = string.Equals(library.Type, "referenceassembly", StringComparison.OrdinalIgnoreCase);
            if (
                !isProject &&
                !isPackage &&
                !isReferenceAssembly &&
                !string.Equals(library.Type, "reference", StringComparison.OrdinalIgnoreCase)
            )
            {
                return false;
            }

            var refsPath = Path.Combine(basePath, refs_directory_name);
            {
                refsPath = TryToCorrectCasingIGuess(refsPath);
            }
            var isPublished = Directory.Exists(refsPath);

            // Resolving reference assemblies requires refs folder to exist
            if (isReferenceAssembly && !isPublished)
            {
                return false;
            }

            var directories = new List<string>
            {
                TryToCorrectCasingIGuess(basePath),
                TryToCorrectCasingIGuess(Path.Combine(basePath, library.Name)),
                TryToCorrectCasingIGuess(Path.Combine(basePath, library.Name, library.Version)),
            };

            if (isPublished)
            {
                directories.Insert(0, refsPath);
            }

            /*
            // Only packages can come from shared runtime
            string? sharedPath = _dependencyContextPaths.SharedRuntime;
            if (isPublished && isPackage && !string.IsNullOrEmpty(sharedPath))
            {
                var sharedDirectory = Path.GetDirectoryName(sharedPath);
                Debug.Assert(sharedDirectory != null);

                var sharedRefs = Path.Combine(sharedDirectory, refs_directory_name);
                if (_fileSystem.Directory.Exists(sharedRefs))
                {
                    directories.Add(sharedRefs);
                }

                directories.Add(sharedDirectory);
            }
            */

            var paths = new List<string>();

            var resolved = false;
            foreach (var assembly in library.Assemblies.Select(x => new[] { TryToCorrectCasingIGuess(x), TryToCorrectCasingIGuess(Path.GetFileName(x)) }).SelectMany(x => x))
            {
                foreach (var directory in directories)
                {
                    if (!TryResolveAssemblyFile(directory, assembly, out var fullName))
                    {
                        continue;
                    }

                    paths.Add(fullName);
                    resolved = true;
                    break;
                }
            }

            if (!resolved)
            {
                return false;
            }

            // only modify the assemblies parameter if we've resolved all files
            assemblies?.AddRange(paths);
            return true;

            static bool TryResolveAssemblyFile(string basePath, string assemblyPath, out string fullName)
            {
                fullName = Path.Combine(basePath, assemblyPath);
                {
                    fullName = TryToCorrectCasingIGuess(fullName);
                }
                return File.Exists(fullName);
            }
        }
    }

    private readonly DependencyContext dependencyContext;
    private readonly CompositeCompilationAssemblyResolver resolver;
    private readonly string[] probePaths;

    public AssemblyResolver(string depsPath, string[] probePaths)
    {
        depsPath = TryToCorrectCasingIGuess(depsPath);
        this.probePaths = probePaths;

        using var depsStream = File.OpenRead(depsPath);

        var reader = new DependencyContextJsonReader();
        {
            dependencyContext = reader.Read(depsStream);
        }

        var baseResolvers = probePaths.Select(
            x => new ICompilationAssemblyResolver[]
            {
                new AppBaseCompilationAssemblyResolver(TryToCorrectCasingIGuess(x)),
                new AppBaseCompilationAssemblyResolverWithStrongPathCoverage(TryToCorrectCasingIGuess(x)),
            }
        ).SelectMany(x => x).ToList();

        baseResolvers.Add(new ReferenceAssemblyPathResolver());
        baseResolvers.Add(new PackageCompilationAssemblyResolver());

        /*foreach (var probePath in probePaths)
        {
            baseResolvers.Add(new NaiveAssemblyPathResolver(probePath));
        }*/

        resolver = new CompositeCompilationAssemblyResolver(baseResolvers.ToArray());
    }

    public Assembly? ResolveAssembly(AssemblyName assemblyName)
    {
        var library = dependencyContext.RuntimeLibraries.FirstOrDefault(
            x => string.Equals(x.Name, assemblyName.Name, StringComparison.OrdinalIgnoreCase)
        );

        // If we couldn't match based on basic name (including package names),
        // actually take a look at runtime assets.
        if (library is null)
        {
            library = dependencyContext.RuntimeLibraries.FirstOrDefault(
                x => x.RuntimeAssemblyGroups.Any(
                    y => y.RuntimeFiles.Any(
                        z => string.Equals(Path.GetFileNameWithoutExtension(z.Path), assemblyName.Name, StringComparison.OrdinalIgnoreCase)
                    )
                )
            );

            // If there's still nothing, give up.
            if (library is null)
            {
                return null;
            }
        }

        var assemblies = new List<string>();
        var wrapper = new CompilationLibrary(
            library.Type,
            library.Name,
            library.Version,
            library.Hash,
            library.RuntimeAssemblyGroups.Where(ag => ProcessIdentifier.Compatible(ag.Runtime)).SelectMany(x => x.AssetPaths),
            library.Dependencies,
            library.Serviceable
        );
        resolver.TryResolveAssemblyPaths(wrapper, assemblies);
        return assemblies.Count != 0
            ? AssemblyLoadContext.Default.LoadFromAssemblyPath(assemblies.First(x => string.Equals(Path.GetFileNameWithoutExtension(x), assemblyName.Name, StringComparison.OrdinalIgnoreCase)))
            : null;
    }

    public IEnumerable<string> GetNativeFiles()
    {
        foreach (var probePath in probePaths)
        {
            foreach (var nativeDir in GetNativeFiles(probePath))
            {
                yield return TryToCorrectCasingIGuess(nativeDir);
            }
        }
    }
    
    public IEnumerable<string> GetNativeFiles(string path)
    {
        foreach (var runtimeLibrary in dependencyContext.RuntimeLibraries)
        {
            var paths = new List<string>();

            if (runtimeLibrary.Path is not null)
            {
                paths.Add(Path.Combine(path, runtimeLibrary.Path));
            }
            
            paths.Add(Path.Combine(path, runtimeLibrary.Name));
            paths.Add(Path.Combine(path, runtimeLibrary.Name, runtimeLibrary.Version));
            
            foreach (var nativeLibrary in runtimeLibrary.NativeLibraryGroups)
            {
                if (!ProcessIdentifier.Compatible(nativeLibrary.Runtime))
                {
                    continue;
                }

                foreach (var nativeFile in nativeLibrary.RuntimeFiles)
                {
                    foreach (var runtimePath in paths)
                    {
                        var fullNativePath = Path.Combine(runtimePath, nativeFile.Path);
                        {
                            fullNativePath = TryToCorrectCasingIGuess(fullNativePath);
                        }
                        
                        if (File.Exists(fullNativePath))
                        {
                            yield return fullNativePath;
                        }
                    }
                }
            }
        }
    }

    private static string TryToCorrectCasingIGuess(string path)
    {
        if (File.Exists(path) || Directory.Exists(path))
        {
            return path;
        }

        var fullPath = Path.GetFullPath(path);
        var root = Path.GetPathRoot(fullPath)!;
        var relative = Path.GetRelativePath(root, fullPath);

        var current = root;
        foreach (var component in relative.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries))
        {
            var match = Directory.EnumerateFileSystemEntries(current)
                                 .FirstOrDefault(x => string.Equals(Path.GetFileName(x), component, StringComparison.OrdinalIgnoreCase));
            if (match is null)
            {
                return path;
            }

            current = match;
        }

        return current;
    }
}
