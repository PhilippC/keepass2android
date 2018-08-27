#!/bin/bash
set -e

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

popd
