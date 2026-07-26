# Building and releasing VisualStates

VisualStates is an Avalonia desktop app targeting .NET 10. Releases are packaged for Windows, macOS, and Linux (no Native AOT for now).

## Prerequisites

- .NET SDK 10 (`global.json` pins a compatible SDK)
- Git
- Bash on Linux/macOS, or PowerShell on Windows

Optional packaging tools:

- macOS bundling: `sips`, `iconutil`, `codesign` (included with macOS)
- Linux AppImage: [appimagetool](https://github.com/AppImage/appimagetool)

## Local development

```bash
dotnet restore VisualStates.slnx
dotnet build VisualStates.slnx -c Release
dotnet test tests/VisualStates.Tests/VisualStates.Tests.csproj -c Release
dotnet run --project src/VisualStates/VisualStates.csproj
```

## Publish

Self-contained publishes go under `output/publish/Release/<rid>/`.

### Windows

```bash
dotnet publish src/VisualStates/VisualStates.csproj -c Release -r win-x64 -o output/publish/Release/win-x64 --self-contained true
```

Zip the publish folder for distribution.

### macOS

```bash
dotnet publish src/VisualStates/VisualStates.csproj -c Release -r osx-arm64 -o output/publish/Release/osx-arm64 --self-contained true
APP_VERSION=0.1.0 ./scripts/publish-macos.sh output/publish/Release/osx-arm64
```

Use `osx-x64` for Intel Macs. The script produces `VisualStates.app`.

### Linux

```bash
dotnet publish src/VisualStates/VisualStates.csproj -c Release -r linux-x64 -o output/publish/Release/linux-x64 --self-contained true
APP_VERSION=0.1.0 ./scripts/package-appimage.sh output/publish/Release/linux-x64 artifacts x86_64
```

Use `linux-arm64` / `aarch64` for ARM64. Linux releases should be AppImages only (not raw publish folders).

## CI artifacts

On every push/PR to `main`, [`.github/workflows/build.yml`](.github/workflows/build.yml) runs tests and uploads packaged artifacts:

| Asset | Platform |
|-------|----------|
| `VisualStates-windows-x64.zip` | Windows x64 |
| `VisualStates-macos-osx-arm64.zip` | macOS Apple Silicon |
| `VisualStates-macos-osx-Intel-x64.zip` | macOS Intel |
| `VisualStates-Linux-x86_64.AppImage` | Linux x64 |
| `VisualStates-aarch64.AppImage` | Linux ARM64 |

## GitHub Releases

Tag-driven releases use [`.github/workflows/release.yml`](.github/workflows/release.yml):

1. Push a tag such as `v0.1.0` or `v0.1.0-rc.1`.
2. CI builds all platform packages.
3. A GitHub Release is created/updated and assets are uploaded.

You can also run **Release** from the Actions UI (`workflow_dispatch`) to create the next patch tag automatically.

Recommended release assets match the CI table above (non-AOT only).

## Versioning

Default package version is set in [`Directory.Build.props`](Directory.Build.props). Release builds override it from the git tag (`v1.2.3` → `1.2.3`).

## References

- [.NET publishing](https://learn.microsoft.com/en-us/dotnet/core/deploying/)
- [Avalonia deployment](https://docs.avaloniaui.net/docs/deployment/)
- [AES_Lacrima packaging model](https://github.com/aruantec/AES_Lacrima/blob/main/BUILDING.md) (inspiration for this layout)
