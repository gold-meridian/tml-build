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
            var isPublished = Directory.Exists(refsPath);

            // Resolving reference assemblies requires refs folder to exist
            if (isReferenceAssembly && !isPublished)
            {
                return false;
            }

            var directories = new List<string>
            {
                basePath,
                Path.Combine(basePath, library.Name),
                Path.Combine(basePath, library.Name, library.Version),
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
            foreach (var assembly in library.Assemblies.Select(x => new[] { x, Path.GetFileName(x) }).SelectMany(x => x))
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
                return File.Exists(fullName);
            }
        }
    }

    private readonly DependencyContext dependencyContext;
    private readonly CompositeCompilationAssemblyResolver resolver;

    public AssemblyResolver(string depsPath, string[] probePaths)
    {
        using var depsStream = File.OpenRead(depsPath);

        var reader = new DependencyContextJsonReader();
        {
            dependencyContext = reader.Read(depsStream);
        }

        var baseResolvers = probePaths.Select(
            x => new ICompilationAssemblyResolver[]
            {
                new AppBaseCompilationAssemblyResolver(x),
                new AppBaseCompilationAssemblyResolverWithStrongPathCoverage(x),
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
            ? AssemblyLoadContext.Default.LoadFromAssemblyPath(assemblies.First(x => Path.GetFileNameWithoutExtension(x) == assemblyName.Name))
            : null;
    }
}
