dotnet clean Tomat.TML.TestMod
dotnet nuget delete -s local Tomat.Terraria.ModLoader.Sdk 1.0.0 --non-interactive
dotnet nuget locals all --clear
dotnet build Tomat.Terraria.ModLoader.Sdk -c Release
dotnet nuget push Tomat.Terraria.ModLoader.Sdk/bin/Release/Tomat.Terraria.ModLoader.Sdk.1.0.0.nupkg -s local
dotnet restore Tomat.TML.TestMod
