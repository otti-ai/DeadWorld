# Dead World - Zombie Apocalypse

A zombie apocalypse mod for RimWorld 1.7.

## Download and install

Download the latest mod ZIP from the project's GitHub **Releases** page. Extract it into RimWorld's `Mods` folder. The resulting mod folder must contain `About/About.xml` directly, for example:

```text
RimWorld/Mods/DeadWorld/About/About.xml
```

Enable **Dead World - Zombie Apocalypse** in RimWorld's mod list.

The GitHub repository contains source files and mod definitions only. Compiled game files are distributed separately in Releases.

## Build from source

Requirements: .NET 10 SDK and a local RimWorld installation.

From the repository root, build with the path to your RimWorld installation:

```bash
dotnet build Source/DeadWorld.csproj -p:GameDir="/path/to/RimWorld"
```

The build writes compiled files to `Assemblies/`, which is excluded from Git. Do not commit compiled files or RimWorld game files.

## Publish a GitHub release

Install and authenticate the GitHub CLI once with `gh auth login`. Then, from the repository root, build and package the first release for your local RimWorld installation:

```bash
dotnet build Source/DeadWorld.csproj --configuration Release -p:GameDir="/path/to/RimWorld"
bash scripts/package-release.sh v0.0.1
```

This creates `dist/DeadWorld-v0.0.1.zip`, with the mod folder at the archive root. Publish it as a GitHub Release:

```bash
gh release create v0.0.1 dist/DeadWorld-v0.0.1.zip --title "Dead World v0.0.1" --generate-notes
```

For later releases, replace `v0.0.1` with the new version. The ZIP contains `About`, `Defs`, `Languages`, and the compiled mod DLLs. It does not contain RimWorld game files. The generated `dist/` directory and compiled `Assemblies/` files are not committed to the source repository.