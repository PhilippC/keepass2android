# How to build Keepass2Android

## Overview

Keepass2Android is a Mono for Android app. This means that you need Xamarin's Mono for Android to build it. However, it also uses several components written in Java, so there are also Android-Studio projects involved. To make things even worse, parts of the keyboard and kdb-library are written in native code.
The current build-scripts assume that the native libraries are already built (they are included in the repo). 

To build KP2A from scratch, make sure that you have Xamarin's Mono for Android installed and also install Android Studio. Make sure that both point to the same Android SDK location.

## Prerequisites

- Install Xamarin.Android
- Fetch all submodules (`git submodule init && git submodule update`)

## Build

### On Windows

```bat
cd build-scripts
build-java.bat
build-xamarin.bat
```

build-java.bat will call gradlew for several Java modules. build-xamarin.bat will first make sure that you have all files at their place. (There is a "secret" file for Dropbox SDK keys which is not in the repo, this is replaced with a dummy file. There are also different Android Manifest files depending on the configuration which is selected by calling the appropriate script.)

**Notes:**

 - For building the java parts, it is suggested to keep a short name (e.g. "c:\projects\keepass2android") for the root project directory. Otherwise the Windows path length limit might be hit when building.
 - Before building the java parts, make sure you have set the ANDROID_HOME variable or create a local.properties file inside the directories with a gradlew file. It is recommended to use the same SDK location as that of the Xamarin build.

### On Linux

- Install [Mono](https://www.mono-project.com/)
- Install Xamarin.Android
  - Option 1: Use the mono-project [CI builds](https://dev.azure.com/xamarin/public/_build/latest?definitionId=48&branchName=main&stageName=Linux)
  - Option 2: [Build it from source](https://github.com/xamarin/xamarin-android/blob/master/Documentation/README.md#building-from-source)
- Setup your environment:
  - Add `xabuild` to your path: `export PATH=/path/to/xamarin.android-oss/bin/Release/bin/:$PATH`
  - Setup your `ANDROID_HOME` if it's not already: `export ANDROID_HOME=/path/to/android/`
  - Alternatively, you can set your `ANDROID_SDK_PATH` and `ANDROID_NDK_PATH`.
- Build [jar2xml](https://github.com/xamarin/jar2xml) and copy `jar2xml.jar` to `/path/to/xamarin.android-oss/bin/Release/lib/xamarin.android/xbuild/Xamarin/Android/`
- Install [libzip](https://libzip.org/) for your distribution.
  - Note: Xamarin seems to require `libzip4`, yet most distributions only ships `libzip5`. As a dirty workaround, it's possible to symlink `libzip.so.5` to `libzip.so.4`. Luckily, it appears to be working.
  - `sudo ln -s /usr/lib/libzip.so.5 /usr/lib/libzip.so.4`
  - or `sudo ln -s /usr/lib64/libzip.so.5 /usr/lib/libzip.so.4`
- Install NuGet dependencies:
  - `cd src/ && nuget restore KeePass.sln`
- Build:
  - Option 1: `cd build-scripts && ./build-all.sh`
  - Option 2:
    - Build the Java parts: `cd build-scripts/ && ./build-java.sh`
    - Build the Xamarin parts: `./build-xamarin.sh`
    - Build the signed APK: `./build-apk.sh`
- Enjoy:
  - `adb install ../keepass2android/bin/Debug/keepass2android.keepass2android_debug-Signed.apk`
