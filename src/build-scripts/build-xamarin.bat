cd ..\Kp2aBusinessLogic\Io
if exist "DropboxFileStorageKeys.cs" (
  echo DropboxFileStorageKeys.cs found.
) ELSE (
  xcopy DropboxFileStorageKeysDummy.cs DropboxFileStorageKeys.cs*
)

cd ..\..

IF NOT "%VSCMD_VCVARSALL_INIT%" == "1" (
  call "C:\Program Files (x86)\Microsoft Visual Studio\2019\Community\VC\Auxiliary\Build\vcvarsall.bat" x86_amd64
)

REM Download NuGet dependencies
msbuild KeePass.sln -t:restore -p:RestorePackagesConfig=true || exit /b

REM Build
set CONFIG=Debug
msbuild KeePass.sln /target:keepass2android-app /p:BuildProjectReferences=true /p:Configuration="%CONFIG%" /p:Platform="Any CPU" /p:AndroidBuildApplicationPackage=True || exit /b

cd build-scripts

echo apk can be found in src\keepass2android\bin\%CONFIG%
