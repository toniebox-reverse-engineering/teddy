#!/bin/bash
set -e

echo "Testing local build (similar to GitHub Actions workflow)"
echo "========================================================="
echo ""

# Test restore step (with runtime specified for cross-compilation)
echo "Step 1: Restoring dependencies..."
echo "---------------------------------"
dotnet restore Teddy/Teddy.csproj -r win-x64
dotnet restore TeddyBench.Avalonia/TeddyBench.Avalonia.csproj -r win-x64

echo ""
echo "✓ Restore successful!"
echo ""

# Test a single build to verify it works
echo "Step 2: Testing Windows x64 CLI build..."
echo "-----------------------------------------"
dotnet publish Teddy/Teddy.csproj \
  -c Release \
  -r win-x64 \
  --self-contained true \
  --no-restore \
  -p:PublishSingleFile=true \
  -p:IncludeNativeLibrariesForSelfExtract=true \
  -p:EnableCompressionInSingleFile=true \
  -o test-artifacts/teddy-win-x64

echo ""
echo "✓ Build successful!"
echo ""

# Check the output
echo "Step 3: Verifying output..."
echo "---------------------------"
if [ -f "test-artifacts/teddy-win-x64/Teddy.exe" ]; then
    echo "✓ Teddy.exe created successfully"
    ls -lh test-artifacts/teddy-win-x64/Teddy.exe
else
    echo "✗ Teddy.exe not found!"
    exit 1
fi

echo ""
echo "=========================================="
echo "Local build test PASSED! ✓"
echo "=========================================="
echo ""
echo "The GitHub Actions workflow should work correctly."
echo "You can now safely commit and push the workflow changes."