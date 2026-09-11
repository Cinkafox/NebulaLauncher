#!/bin/sh
cd "$(dirname "$0")"

dotnet build -c Release 
dotnet publish -c Release -r win-x64 -o ./publish -p:IncludeNativeLibrariesForSelfExtract=true -p:PublishSingleFile=true --self-contained true Nebula.Launcher
dotnet publish -c Release -r linux-x64 -o ./publish -p:IncludeNativeLibrariesForSelfExtract=true -p:PublishSingleFile=true --self-contained true Nebula.Launcher

mv ./publish/Nebula.Launcher.exe ./publish/NebulaLauncher.exe
mv ./publish/Nebula.Launcher ./publish/NebulaLauncher