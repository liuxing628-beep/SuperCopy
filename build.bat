@echo off
setlocal
cd /d "%~dp0"
set "CSC=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
if not exist "%CSC%" set "CSC=%WINDIR%\Microsoft.NET\Framework\v4.0.30319\csc.exe"
if not exist "%CSC%" (
  echo C# compiler not found. Install Microsoft .NET Framework 4.8 and retry.
  pause
  exit /b 1
)
if not exist dist mkdir dist
"%CSC%" /nologo /target:winexe /platform:anycpu /optimize+ /codepage:65001 /out:dist\SuperCopy.exe /win32manifest:src\app.manifest /reference:System.dll /reference:System.Core.dll /reference:System.Drawing.dll /reference:System.Windows.Forms.dll src\AssemblyInfo.cs src\Native.cs src\BlockStore.cs src\Popup.cs src\TileLayout.cs src\MouseListener.cs src\Program.cs
if errorlevel 1 (
  echo Build failed. Close any running SuperCopy instance and retry.
  pause
  exit /b 1
)
copy /y src\App.config dist\SuperCopy.exe.config >nul
echo Build succeeded: dist\SuperCopy.exe
echo Double-click that file to start.
pause
