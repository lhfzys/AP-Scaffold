@echo off
REM ============================================================
REM AP-Scaffold one-click build & package script
REM Steps: clean bin -> build solution -> publish app -> Inno compile
REM Usage: double-click this file, or run installer\build-installer.bat
REM Output: installer\Output\*-Setup.exe
REM Requires: .NET 10 SDK (build) + Inno Setup 6 (default path)
REM ============================================================
setlocal
cd /d "%~dp0\.."

echo === [1/4] Cleaning bin directory ===
if exist bin rmdir /s /q bin

echo === [2/4] Building solution (Release) ===
dotnet build AP-Automation.Platform.slnx -c Release
if errorlevel 1 goto :error

echo === [3/4] Publishing app (framework-dependent win-x64) ===
dotnet publish platform/hosts/AP.Host.Desktop/AP.Host.Desktop.csproj -c Release -r win-x64 --self-contained false -p:AppendRuntimeIdentifierToOutputPath=true
if errorlevel 1 goto :error

echo === [4/4] Compiling installer (Inno Setup 6) ===
"C:\Program Files (x86)\Inno Setup 6\ISCC.exe" installer\setup.iss
if errorlevel 1 goto :error

echo.
echo === DONE: get the setup file under installer\Output\ ===
exit /b 0

:error
echo.
echo *** BUILD FAILED: see errors above ***
exit /b 1
