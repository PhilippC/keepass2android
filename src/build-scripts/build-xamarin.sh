#!/bin/bash
set -e

pushd ..

pushd Kp2aBusinessLogic/Io

if [ -f "DropboxFileStorageKeys.cs" ]
then
  echo "DropboxFileStorageKeys.cs found."
else
  cp DropboxFileStorageKeysDummy.cs DropboxFileStorageKeys.cs
fi

popd

pushd keepass2android
./UseManifestDebug.sh
popd

# call "C:\Program Files (x86)\Microsoft Visual Studio 12.0\VC\vcvarsall.bat" x86_amd64

# check if ANDROID_HOME is defined
if [ -z ${ANDROID_HOME+x} ];
then
	xabuild KeePass.sln /target:keepass2android /p:BuildProjectReferences=true /p:Configuration="Debug" /p:Platform="Any CPU" "$@"
else
	xabuild KeePass.sln /target:keepass2android /p:AndroidSdkDirectory=$ANDROID_HOME /p:BuildProjectReferences=true /p:Configuration="Debug" /p:Platform="Any CPU" "$@"
fi

popd
