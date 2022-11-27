#!/bin/bash
set -e

#unset ANDROID_NDK_HOME ANDROID_NDK

pushd ../java/

pushd JavaFileStorageTest-AS
./gradlew assemble
popd

pushd KP2ASoftkeyboard_AS
./gradlew assemble
popd

pushd Keepass2AndroidPluginSDK2
./gradlew assemble
popd

pushd KP2AKdbLibrary
./gradlew assemble
popd

popd
