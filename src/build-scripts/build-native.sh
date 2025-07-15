#!/bin/bash
set -e

pushd ../java/argon2
ndk-build
popd
