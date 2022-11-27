cd ..\Kp2aBusinessLogic\Io
if exist "DropboxFileStorageKeys.cs" (
  echo DropboxFileStorageKeys.cs found.
) ELSE (
  xcopy DropboxFileStorageKeysDummy.cs DropboxFileStorageKeys.cs*
)

cd ..\..\keepass2android
call UseManifestDebug.bat
cd ..

call "C:\Program Files (x86)\Microsoft Visual Studio 12.0\VC\vcvarsall.bat" x86_amd64

msbuild KeePass.sln /target:keepass2android-app /p:BuildProjectReferences=true /p:Configuration="Debug" /p:Platform="Any CPU"

cd build-scripts