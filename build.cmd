@echo off
rem Builds ChatTranslator.exe with the C# compiler that ships with Windows (.NET Framework 4.x). Nothing to install.
setlocal
set "CSC=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
if not exist "%CSC%" set "CSC=%WINDIR%\Microsoft.NET\Framework\v4.0.30319\csc.exe"
if not exist "%CSC%" (echo csc.exe not found - .NET Framework 4.x is required & exit /b 1)

pushd "%~dp0"
"%CSC%" /nologo /target:winexe /optimize+ /codepage:65001 /win32icon:assets\app.ico /out:ChatTranslator.exe ^
  /r:System.Web.Extensions.dll /r:System.Security.dll /r:System.Windows.Forms.dll /r:System.Drawing.dll ^
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
