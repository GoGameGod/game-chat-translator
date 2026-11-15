@echo off
rem Builds ChatTranslator.exe with the C# compiler that ships with Windows (.NET Framework 4.x). Nothing to install.
setlocal
set "CSC=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
if not exist "%CSC%" set "CSC=%WINDIR%\Microsoft.NET\Framework\v4.0.30319\csc.exe"
if not exist "%CSC%" (echo csc.exe not found - .NET Framework 4.x is required & exit /b 1)

rem Windows OCR (Windows.Media.Ocr) comes from the WinRT metadata that ships with Windows 10/11.
set "FW=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319"
if not exist "%FW%\System.Runtime.dll" set "FW=%WINDIR%\Microsoft.NET\Framework\v4.0.30319"
set "WM=%WINDIR%\System32\WinMetadata"

pushd "%~dp0"
"%CSC%" /nologo /target:winexe /optimize+ /codepage:65001 /win32icon:assets\app.ico /out:ChatTranslator.exe ^
  /r:System.Web.Extensions.dll /r:System.Security.dll /r:System.Windows.Forms.dll /r:System.Drawing.dll ^
  /r:"%FW%\System.Runtime.dll" ^
  /r:"%WM%\Windows.Foundation.winmd" /r:"%WM%\Windows.Media.winmd" /r:"%WM%\Windows.Graphics.winmd" ^
  /r:"%WM%\Windows.Storage.winmd" /r:"%WM%\Windows.Globalization.winmd" ^
  /recurse:src\*.cs
set ERR=%ERRORLEVEL%
popd

if not "%ERR%"=="0" (
  echo.
  echo Build failed.
  if "%CI%"=="" pause
  exit /b 1
)
echo Done: %~dp0ChatTranslator.exe
