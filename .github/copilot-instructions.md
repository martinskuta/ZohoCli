# Copilot Instructions for ZohoCli

## Build & Test Commands

### Build
```bash
dotnet build
```

### Run
```bash
dotnet run --project ZohoCLI/ZohoCLI.csproj
```

### Clean
```bash
dotnet clean
```

## Project Structure

**ZohoCli.slnx** - Solution file (new unified format)  
**ZohoCLI/** - Main console application
- **ZohoCLI.csproj** - Project file using .NET 10 with nullable reference types and implicit usings enabled
- **Program.cs** - Entry point

## Key Stack

- **Framework**: .NET 10.0
- **Project Type**: Console Application (.exe)
- **Language Version**: Latest C# with nullable reference types enabled
- **Implicit Usings**: Enabled (no need to explicitly `using` common namespaces)

## Conventions

- **Nullable Reference Types**: Project has `<Nullable>enable</Nullable>` - use `?` for nullable types, avoid null-reference exceptions
- **Implicit Usings**: Common namespaces are automatically included; no need for explicit `using` statements in most cases
- **Build Output**: Binaries go to `bin/` and build artifacts to `obj/` (both in .gitignore)

## IDE Integration

The `.idea/` directory indicates JetBrains Rider is used. If making changes that affect project structure (new files, project settings), be aware this IDE may auto-generate config files.
