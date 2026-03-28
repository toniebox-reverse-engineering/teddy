# Release Process

This document describes how to create a new release of Teddy.

## Automated Release (Recommended)

The automated release process uses GitHub Actions to build binaries for all platforms and create a GitHub release.

### Prerequisites
1. Ensure you have push access to the repository
2. Make sure all changes are committed and pushed to `master`
3. Update the changelog file for the new version (see below)

### Steps

1. **Create the changelog file:**
   ```bash
   # Copy the template and edit it
   cp CHANGELOG-v1.0.0.md CHANGELOG-v1.0.1.md
   # Edit the changelog to include new changes
   ```

2. **Commit the changelog:**
   ```bash
   git add CHANGELOG-v1.0.1.md
   git commit -m "Prepare release v1.0.1"
   git push origin master
   ```

3. **Create and push the tag:**
   ```bash
   git tag -a v1.0.1 -m "Release v1.0.1"
   git push origin v1.0.1
   ```

4. **Wait for GitHub Actions to complete:**
   - Go to https://github.com/BdN3504/teddy/actions
   - Watch the "Release" workflow complete
   - The workflow will automatically create a GitHub release with all binaries

5. **Verify the release:**
   - Go to https://github.com/BdN3504/teddy/releases
   - Check that the release has been created with all assets
   - Download and test one of the binaries

## Manual Release Process

If you need to build and release manually (e.g., GitHub Actions is unavailable):

### Prerequisites
- .NET 8.0 SDK
- Git
- zip (for Windows archives)
- tar (for Unix archives)
- `gh` CLI tool (for creating GitHub releases)

### Steps

1. **Update the changelog:**
   ```bash
   # Create changelog for this version
   cp CHANGELOG-v1.0.0.md CHANGELOG-v1.0.1.md
   # Edit CHANGELOG-v1.0.1.md
   ```

2. **Build all binaries:**
   ```bash
   ./build-release.sh v1.0.1
   ```

   This will create all binaries in `release-builds/v1.0.1/`

3. **Create and push the tag:**
   ```bash
   git add CHANGELOG-v1.0.1.md
   git commit -m "Prepare release v1.0.1"
   git push origin master

   git tag -a v1.0.1 -m "Release v1.0.1"
   git push origin v1.0.1
   ```

4. **Create the GitHub release:**
   ```bash
   gh release create v1.0.1 \
     --title "Teddy v1.0.1" \
     --notes-file CHANGELOG-v1.0.1.md \
     release-builds/v1.0.1/*.zip \
     release-builds/v1.0.1/*.tar.gz
   ```

5. **Verify the release:**
   - Visit https://github.com/BdN3504/teddy/releases/tag/v1.0.1
   - Verify all assets are present
   - Test download one of the binaries

## Release Checklist

Before creating a release:

- [ ] All changes are committed and pushed
- [ ] Tests pass (`dotnet test`)
- [ ] Changelog is updated with all changes
- [ ] Version number follows semantic versioning
- [ ] README is up to date
- [ ] No sensitive information in code

After creating a release:

- [ ] GitHub release is created successfully
- [ ] All platform binaries are present (8 total: 4 CLI + 4 GUI)
- [ ] Binaries are downloadable
- [ ] At least one binary has been tested
- [ ] Release notes are accurate

## Version Numbering

Teddy follows [Semantic Versioning](https://semver.org/):

- **MAJOR** version (X.0.0): Incompatible API changes
- **MINOR** version (0.X.0): New functionality in a backwards-compatible manner
- **PATCH** version (0.0.X): Backwards-compatible bug fixes

## Binary Naming Convention

Release binaries follow this naming pattern:

**CLI:**
- `teddy-{version}-win-x64.zip` - Windows 64-bit
- `teddy-{version}-linux-x64.tar.gz` - Linux 64-bit
- `teddy-{version}-osx-x64.tar.gz` - macOS Intel
- `teddy-{version}-osx-arm64.tar.gz` - macOS Apple Silicon

**GUI:**
- `teddybench-{version}-win-x64.zip` - Windows 64-bit
- `teddybench-{version}-linux-x64.tar.gz` - Linux 64-bit
- `teddybench-{version}-osx-x64.tar.gz` - macOS Intel
- `teddybench-{version}-osx-arm64.tar.gz` - macOS Apple Silicon

## Troubleshooting

### Build fails on GitHub Actions
- Check the workflow logs for specific errors
- Ensure all NuGet packages are available
- Verify .NET SDK version matches project requirements

### Release assets missing
- Check that the `body_path` in the workflow points to the correct changelog file
- Ensure the changelog file exists in the repository before tagging

### Manual build fails
- Ensure you have .NET 8.0 SDK installed: `dotnet --version`
- Try building each platform separately to identify the issue
- Check that all project dependencies are restored: `dotnet restore`
