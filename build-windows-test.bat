@echo off
REM Test script for building TeddyBench on Windows
REM Run this on your Windows machine to verify the build works

echo Testing TeddyBench build on Windows
echo ===================================
echo.

REM Check if dotnet is installed
dotnet --version >nul 2>&1
if %ERRORLEVEL% neq 0 (
    echo ERROR: .NET SDK not found!
    echo Please install .NET 8.0 SDK from: https://dotnet.microsoft.com/download
    exit /b 1
)

echo Found .NET SDK version:
dotnet --version
echo.

echo Building TeddyBench (Windows Forms GUI)...
echo ------------------------------------------
dotnet build TeddyBench\TeddyBench.csproj --configuration Release

if %ERRORLEVEL% neq 0 (
    echo.
    echo BUILD FAILED!
    exit /b 1
)

echo.
echo ==========================================
echo BUILD SUCCESSFUL!
echo ==========================================
echo.
echo Output location:
dir TeddyBench\bin\Release\net8.0-windows\win10-x64\ 2>nul
if %ERRORLEVEL% equ 0 (
    echo TeddyBench\bin\Release\net8.0-windows\win10-x64\TeddyBench.exe
) else (
    dir TeddyBench\bin\Release\net8.0-windows\
)

echo.
echo The legacy TeddyBench application compiles successfully on .NET 8.0!