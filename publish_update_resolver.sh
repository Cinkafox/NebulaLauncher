#!/bin/sh
cd "$(dirname "$0")"

dotnet publish -c Release -r win-x64 -o ./publish -p:IncludeNativeLibrariesForSelfExtract=true Nebula.UpdateResolver
dotnet publish -c Release -r linux-x64 -o ./publish -p:IncludeNativeLibrariesForSelfExtract=true Nebula.UpdateResolver

mv ./publish/Nebula.UpdateResolver.exe ./publish/NebulaLauncher.exe
mv ./publish/Nebula.UpdateResolver ./publish/NebulaLauncher
mv ./publish/Nebula.UpdateResolver.pdb ./publish/NebulaLauncher.pdb
