cd ../Kp2aBusinessLogic/Io
if [ -f "DropboxFileStorageKeys.cs" ]
then
  echo "DropboxFileStorageKeys.cs found."
else
  cp DropboxFileStorageKeysDummy.cs DropboxFileStorageKeys.cs
fi

cd ../../keepass2android
./UseManifestDebug.sh
cd ..

#call "C:\Program Files (x86)\Microsoft Visual Studio 12.0\VC\vcvarsall.bat" x86_amd64

xabuild KeePass.sln /target:keepass2android /p:BuildProjectReferences=true /p:Configuration="Debug" /p:Platform="Any CPU"

cd build-scripts
