#!/bin/bash
set -e

echo '*****************************************'
echo '******* Building native libraries *******'
echo '*****************************************'
./build-native.sh

echo '*****************************************'
echo '********** Building Java parts **********'
echo '*****************************************'
./build-java.sh

echo '*****************************************'
echo '******** Building Xamarin parts *********'
echo '*****************************************'
./build-xamarin.sh

echo '*****************************************'
echo '************** Building APK *************'
echo '*****************************************'
./build-apk.sh

echo
echo 'Congratulations! You you can find the target APK in src/keepass2android/bin/Debug/.'
