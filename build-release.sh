#!/bin/bash
set -e

VERSION="${1:-v1.0.0}"
OUTPUT_DIR="release-builds/$VERSION"

echo "Building Teddy release $VERSION"
echo "================================"

# Clean previous builds
rm -rf "$OUTPUT_DIR"
mkdir -p "$OUTPUT_DIR"

# Build CLI for all platforms
echo ""
echo "Building Teddy CLI..."
echo "---------------------"

# Windows x64
echo "Building Windows x64..."
dotnet publish Teddy/Teddy.csproj \
  -c Release \
  -r win-x64 \
  --self-contained true \
  -p:PublishSingleFile=true \
  -p:IncludeNativeLibrariesForSelfExtract=true \
  -p:EnableCompressionInSingleFile=true \
  -o "$OUTPUT_DIR/teddy-$VERSION-win-x64"

# Linux x64
echo "Building Linux x64..."
dotnet publish Teddy/Teddy.csproj \
  -c Release \
  -r linux-x64 \
  --self-contained true \
  -p:PublishSingleFile=true \
  -p:IncludeNativeLibrariesForSelfExtract=true \
  -p:EnableCompressionInSingleFile=true \
  -o "$OUTPUT_DIR/teddy-$VERSION-linux-x64"

# macOS x64 (Intel)
echo "Building macOS x64..."
dotnet publish Teddy/Teddy.csproj \
  -c Release \
  -r osx-x64 \
  --self-contained true \
  -p:PublishSingleFile=true \
  -p:IncludeNativeLibrariesForSelfExtract=true \
  -p:EnableCompressionInSingleFile=true \
  -o "$OUTPUT_DIR/teddy-$VERSION-osx-x64"

# macOS ARM64 (Apple Silicon)
echo "Building macOS ARM64..."
dotnet publish Teddy/Teddy.csproj \
  -c Release \
  -r osx-arm64 \
  --self-contained true \
  -p:PublishSingleFile=true \
  -p:IncludeNativeLibrariesForSelfExtract=true \
  -p:EnableCompressionInSingleFile=true \
  -o "$OUTPUT_DIR/teddy-$VERSION-osx-arm64"

# Build GUI for all platforms
echo ""
echo "Building TeddyBench.Avalonia GUI..."
echo "-----------------------------------"

# Windows x64
echo "Building GUI for Windows x64..."
dotnet publish TeddyBench.Avalonia/TeddyBench.Avalonia.csproj \
  -c Release \
  -r win-x64 \
  --self-contained true \
  -p:PublishSingleFile=true \
  -p:IncludeNativeLibrariesForSelfExtract=true \
  -p:EnableCompressionInSingleFile=true \
  -o "$OUTPUT_DIR/teddybench-$VERSION-win-x64"

# Linux x64
echo "Building GUI for Linux x64..."
dotnet publish TeddyBench.Avalonia/TeddyBench.Avalonia.csproj \
  -c Release \
  -r linux-x64 \
  --self-contained true \
  -p:PublishSingleFile=true \
  -p:IncludeNativeLibrariesForSelfExtract=true \
  -p:EnableCompressionInSingleFile=true \
  -o "$OUTPUT_DIR/teddybench-$VERSION-linux-x64"

# macOS x64 (Intel)
echo "Building GUI for macOS x64..."
dotnet publish TeddyBench.Avalonia/TeddyBench.Avalonia.csproj \
  -c Release \
  -r osx-x64 \
  --self-contained true \
  -p:PublishSingleFile=true \
  -p:IncludeNativeLibrariesForSelfExtract=true \
  -p:EnableCompressionInSingleFile=true \
  -o "$OUTPUT_DIR/teddybench-$VERSION-osx-x64"

# macOS ARM64 (Apple Silicon)
echo "Building GUI for macOS ARM64..."
dotnet publish TeddyBench.Avalonia/TeddyBench.Avalonia.csproj \
  -c Release \
  -r osx-arm64 \
  --self-contained true \
  -p:PublishSingleFile=true \
  -p:IncludeNativeLibrariesForSelfExtract=true \
  -p:EnableCompressionInSingleFile=true \
  -o "$OUTPUT_DIR/teddybench-$VERSION-osx-arm64"

# Create archives
echo ""
echo "Creating release archives..."
echo "----------------------------"

cd "$OUTPUT_DIR"

# Windows archives
echo "Creating Windows archives..."
cd teddy-$VERSION-win-x64 && zip -q ../teddy-$VERSION-win-x64.zip Teddy.exe && cd ..
cd teddybench-$VERSION-win-x64 && zip -q ../teddybench-$VERSION-win-x64.zip TeddyBench.Avalonia.exe appsettings.json && cd ..

# Linux archives
echo "Creating Linux archives..."
cd teddy-$VERSION-linux-x64 && tar czf ../teddy-$VERSION-linux-x64.tar.gz Teddy && cd ..
cd teddybench-$VERSION-linux-x64 && tar czf ../teddybench-$VERSION-linux-x64.tar.gz TeddyBench.Avalonia appsettings.json && cd ..

# macOS x64 archives
echo "Creating macOS x64 archives..."
cd teddy-$VERSION-osx-x64 && tar czf ../teddy-$VERSION-osx-x64.tar.gz Teddy && cd ..
cd teddybench-$VERSION-osx-x64 && tar czf ../teddybench-$VERSION-osx-x64.tar.gz TeddyBench.Avalonia appsettings.json && cd ..

# macOS ARM64 archives
echo "Creating macOS ARM64 archives..."
cd teddy-$VERSION-osx-arm64 && tar czf ../teddy-$VERSION-osx-arm64.tar.gz Teddy && cd ..
cd teddybench-$VERSION-osx-arm64 && tar czf ../teddybench-$VERSION-osx-arm64.tar.gz TeddyBench.Avalonia appsettings.json && cd ..

cd ../..

echo ""
echo "Build complete!"
echo "==============="
echo "Release artifacts created in: $OUTPUT_DIR"
echo ""
echo "Archives ready for GitHub release:"
ls -lh "$OUTPUT_DIR"/*.zip "$OUTPUT_DIR"/*.tar.gz 2>/dev/null || true
