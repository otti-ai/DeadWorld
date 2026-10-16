# Dead World - Zombie Apocalypse

A zombie apocalypse mod for RimWorld 1.7.

## Download and install

Download the latest mod ZIP from the project's GitHub **Releases** page. Extract it into RimWorld's `Mods` folder. The resulting mod folder must contain `About/About.xml` directly, for example:

```text
RimWorld/Mods/DeadWorld/About/About.xml
```

Enable **Dead World - Zombie Apocalypse** in RimWorld's mod list.

The GitHub repository contains source files and mod definitions. Compiled game files are distributed separately in Releases.

## Build from source

Requirements: .NET 10 SDK and a local RimWorld installation.

From the repository root, build with the path to your RimWorld installation:

```bash
dotnet build Source/DeadWorld.csproj -p:GameDir="/path/to/RimWorld"
```

The build writes compiled files to `Assemblies/`. To prepare a GitHub Release, include the complete mod folder structure in a ZIP, including `About`, `Defs`, `Languages`, and the generated `Assemblies` files. Do not include RimWorld game files.