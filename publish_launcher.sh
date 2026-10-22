#!/bin/sh
cd "$(dirname "$0")"

dotnet build -c Release 
dotnet publish -c Release -r linux-x64 -o ./publish Nebula.Launcher
zip -r NebulaLauncher-linux.zip ./publish

rm -r ./publish
dotnet publish -c Release -r win-x64 -o ./publish Nebula.Launcher
zip -r NebulaLauncher-windows.zip ./publish