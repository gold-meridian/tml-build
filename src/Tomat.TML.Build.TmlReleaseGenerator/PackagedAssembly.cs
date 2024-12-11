namespace Tomat.TML.Build.TmlReleaseGenerator;

public readonly record struct PackagedAssembly(
    string  Name,
    string  DllPath,
    string? XmlPath,
    string? PdbPath
);