#!/bin/bash
set -e

pushd ../keepass2android

xabuild keepass2android.csproj /t:SignAndroidPackage "$@"

popd
