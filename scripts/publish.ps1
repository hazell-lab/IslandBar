# Builds release versions of IslandBar into the "publish" folder and zips them.
# Run from the project root in PowerShell:   .\scripts\publish.ps1
$ErrorActionPreference = "Stop"
$project = "src/IslandBar/IslandBar.csproj"

Write-Host "1/3  Small build (needs the .NET 8 Desktop Runtime on the target PC)..."
dotnet publish $project -c Release -r win-x64 --self-contained false -o publish/IslandBar-framework-dependent

Write-Host "2/3  Self-contained build (runs on any Windows 10/11 PC, bigger)..."
dotnet publish $project -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o publish/IslandBar-self-contained

Write-Host "3/3  Zipping..."
Compress-Archive -Path publish/IslandBar-framework-dependent/* -DestinationPath publish/IslandBar-framework-dependent.zip -Force
Compress-Archive -Path publish/IslandBar-self-contained/*     -DestinationPath publish/IslandBar-self-contained.zip -Force
Write-Host "Done. See the publish folder."
