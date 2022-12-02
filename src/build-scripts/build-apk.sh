#!/bin/bash
set -e

pushd ../keepass2android

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

CONFIG=Release

# check if ANDROID_HOME is defined
if [ -z ${ANDROID_HOME+x} ];
then
	$BUILDER keepass2android-app.csproj /t:SignAndroidPackage /p:Configuration="$CONFIG" /p:Platform=AnyCPU "$@"
else
	$BUILDER keepass2android-app.csproj /p:AndroidSdkDirectory=$ANDROID_HOME /t:SignAndroidPackage /p:Configuration="$CONFIG" /p:Platform=AnyCPU "$@"
fi

popd
