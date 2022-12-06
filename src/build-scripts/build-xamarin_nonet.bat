REM Build Keepass2Android Offline
REM This is the same as the build-xamarin.bat script except Flavor is set to NoNet

@echo OFF

set Flavor=NoNet
set Configuration=Debug

if NOT "%Flavor%" == "" (
  set MSBUILD_PARAMS=%MSBUILD_PARAMS% -p:Flavor=%Flavor%
)
if NOT "%Configuration%" == "" (
  set MSBUILD_PARAMS=%MSBUILD_PARAMS% -p:Configuration=%Configuration%
)

cd ..\Kp2aBusinessLogic\Io
if exist "DropboxFileStorageKeys.cs" (
  echo DropboxFileStorageKeys.cs found.
) ELSE (
  echo Put dummy DropboxFileStorageKeys.cs
  xcopy DropboxFileStorageKeysDummy.cs DropboxFileStorageKeys.cs
)
cd ..\..

REM Get Visual Studio install path & call vcvarsall.bat
FOR /F "tokens=* USEBACKQ" %%F IN (`"C:\Program Files (x86)\Microsoft Visual Studio\Installer\vswhere.exe" -property installationPath`) DO (
SET VS_INSTALL_PATH=%%F
)
echo Visual Studio Install PATH: %VS_INSTALL_PATH%

IF "%VCToolsVersion%" == "" (
  echo Setting up Visual Studio Environment
  call "%VS_INSTALL_PATH%\VC\Auxiliary\Build\vcvarsall.bat" x86_amd64 || exit /b
)

REM Download NuGet dependencies
echo Download NuGet dependencies
msbuild KeePass.sln -t:restore -p:RestorePackagesConfig=true || exit /b

REM Build
echo Start building Keepass2Android
@echo ON
msbuild KeePass.sln /target:keepass2android-app /p:BuildProjectReferences=true %MSBUILD_PARAMS% /p:Platform="Any CPU" /p:AndroidBuildApplicationPackage=True -m || exit /b

@echo OFF
cd build-scripts
echo APK can be found in src\keepass2android\bin\%Configuration%
