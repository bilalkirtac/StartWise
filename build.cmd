@echo off
setlocal
rem Builds StartWise (formerly TIA Assistant) with the C# compiler that ships with Windows. No SDK or NuGet needed.
rem   build.cmd          -> TIA V21 (main line) -> bin\V21\TiaAssistant.exe
rem   build.cmd V18      -> TIA V18             -> bin\TiaAssistant.exe
rem One exe per TIA version: the Openness API is version bound. Both exes share src\ and knowledge\.
set "CSC=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
set "VER=%~1"
if "%VER%"=="" set "VER=V21"
if /i "%VER%"=="18" set "VER=V18"
if /i "%VER%"=="21" set "VER=V21"

set "API=%TIA_API_DIR%"
set "OUT=%~dp0bin"
set "DEF="
if /i "%VER%"=="V18" (
  if "%API%"=="" set "API=%ProgramFiles%\Siemens\Automation\Portal V18\PublicAPI\V18"
  set "DEF=/define:TIA_V18"
) else if /i "%VER%"=="V21" (
  if "%API%"=="" set "API=%ProgramFiles%\Siemens\Automation\Portal V21\PublicAPI\V21\net48"
  set "OUT=%~dp0bin\V21"
) else (
  echo ERROR: unknown TIA version "%VER%". Use V18 or V21. & exit /b 1
)

if not exist "%CSC%" echo ERROR: C# compiler not found: %CSC% & exit /b 1

rem V18: one Siemens.Engineering.dll. V21: split assemblies (Base, Step7, WinCC, Safety, Startdrive ...).
set "REFS="
if exist "%API%\Siemens.Engineering.dll" set REFS=/r:"%API%\Siemens.Engineering.dll"
for %%n in (Base Step7 WinCC WinCC.Extension WinCCUnified Safety Startdrive) do if exist "%API%\Siemens.Engineering.%%n.dll" call set REFS=%%REFS%% /r:"%API%\Siemens.Engineering.%%n.dll"
if not defined REFS echo ERROR: no Openness assemblies (Siemens.Engineering*.dll) in %API% & exit /b 1
if /i "%VER%"=="V21" if not exist "%API%\Siemens.Engineering.Startdrive.dll" echo WARNING: Startdrive V21 API not found - drive tools will fail to compile.
if /i "%VER%"=="V21" if not exist "%API%\Siemens.Engineering.WinCCUnified.dll" (echo NOTE: WinCC Unified not installed - Unified tools are left out. & set "DEF=%DEF% /define:NO_UNIFIED")

if not exist "%OUT%" mkdir "%OUT%"

rem The exe keeps the file name TiaAssistant.exe (Norton flagged a newly named exe; the Claude-visible name comes from the config key).
rem A running exe cannot be overwritten but can be renamed, so move the old one aside first.
del /q "%OUT%\TiaAssistant.old.exe" >nul 2>&1
if exist "%OUT%\TiaAssistant.exe" ren "%OUT%\TiaAssistant.exe" TiaAssistant.old.exe >nul 2>&1

"%CSC%" /nologo /target:exe /platform:x64 /optimize+ %DEF% /out:"%OUT%\TiaAssistant.exe" /r:System.Web.Extensions.dll /r:System.Core.dll /r:System.Drawing.dll /r:System.Security.dll /r:System.Xml.Linq.dll /r:System.IO.Compression.dll /r:System.IO.Compression.FileSystem.dll %REFS% "%~dp0src\*.cs"
if errorlevel 1 echo. & echo BUILD FAILED - copy the lines above to Claude. & exit /b 1

echo BUILD OK (%VER%): %OUT%\TiaAssistant.exe  (StartWise)
if exist "%OUT%\TiaAssistant.exe" for /f "usebackq delims=" %%h in (`powershell -NoProfile -Command "(Get-FileHash -Algorithm SHA256 '%OUT%\TiaAssistant.exe').Hash.ToLower()"`) do echo SHA-256: %%h
echo.
echo Restart Claude now so it loads the new build.
echo --- self test ---
"%OUT%\TiaAssistant.exe" --selftest
echo.
