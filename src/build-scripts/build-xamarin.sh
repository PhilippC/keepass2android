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

# Determine if we use msbuild or xabuild to build.
if which msbuild > /dev/null; then
  if [ $(uname) == "Linux" ]; then
    # For now, when running on Linux, we can't use msbuild but have to use xabuild (provided by https://github.com/xamarin/xamarin-android)
    BUILDER=xabuild
  else
    BUILDER=msbuild
  fi
else
  BUILDER=xabuild
fi

CONFIG=Debug

# check if ANDROID_HOME is defined
if [ -z ${ANDROID_HOME+x} ];
then
	$BUILDER KeePass.sln /target:keepass2android-app /p:BuildProjectReferences=true /p:Configuration="$CONFIG" /p:Platform="Any CPU" "$@"
else
	$BUILDER KeePass.sln /target:keepass2android-app /p:AndroidSdkDirectory=$ANDROID_HOME /p:BuildProjectReferences=true /p:Configuration="$CONFIG" /p:Platform="Any CPU" "$@"
fi

popd
