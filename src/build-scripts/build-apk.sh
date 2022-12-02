#!/bin/bash
set -e

pushd ../keepass2android

# check if ANDROID_HOME is defined
if [ -z ${ANDROID_HOME+x} ];
then
	xabuild keepass2android-app.csproj /t:SignAndroidPackage "$@"
else
	xabuild keepass2android-app.csproj /p:AndroidSdkDirectory=$ANDROID_HOME /t:SignAndroidPackage "$@"
fi

popd
