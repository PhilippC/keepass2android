# How to build Keepass2Android

## Overview

Keepass2Android is a Mono for Android app. This means that you need Xamarin's Mono for Android to build it. However, it also uses several components written in Java, so there are also Android-Studio projects involved. To make things even worse, parts of the keyboard and kdb-library are written in native code.

To build KP2A from scratch, you need:
- Xamarin's Mono for Android (also included in Visual Studio)
- Android SDK & NDK

Prior to building Keepass2Android, you need to build some of its components (from command line). Then you can build the full project either through Visual Studio, or through command line.

By using the command line, you can build on Windows, macOS or Linux.

## Prerequisites

### Common to all architectures
- Install Android SDK & NDK (either manually with Google's [sdkmanager](https://developer.android.com/studio/command-line/sdkmanager), or through Android Studio). Visual Studio also installs a version of it, but in the end the directory must be writable and in a path without spaces (see below) so as to be able to build the components.
- Fetch the main repository of Keepass2Android and all submodules
    - Note that VisualStudio can do this for you, otherwise run:
    - `git submodule init && git submodule update`

### On Windows or macOS
- Install Visual Studio (for example 2019) with Xamarin.Android (ie. with capability to build Android apps). This should provide the needed tools like
    - Xamarin.Android
    - MSBuild
    - Java JDK
- If you plan to build also from the command line:
    - Install the MSVC build tools of visual studio. They provide the `vcvarsall.bat` file which among other things adds MSBuild to the PATH.
    - Install [NuGet](https://www.nuget.org/downloads) to build also with "make". Alternatively, on Windows, if you use [chocolatey](https://chocolatey.org), run as administrator:
      - `choco install nuget.commandline`
    - Check that you have access to 'GNU make'.
      - On Windows, it is usually not available by default. But the Android NDK provides it. You can find it in `%ANDROID_NDK_ROOT%\prebuilt\windows-x86_64\bin\make.exe`.  Alternatively, on Windows, if you use [chocolatey](https://chocolatey.org), run as administrator:
        - `choco install make`
      - On macOS, it is usually only installed if you have developer command line tools installed or if you use [homebrew](https://brew.sh) or [macports](https://www.macports.org/). As an alternative it may be available in the Android NDK at `%ANDROID_NDK_ROOT%/prebuilt/darwin-x86_64/bin/make`.

### On Linux
- Install Java's JDK
    - On Debian, for example: `apt install default-jdk-headless`.

- Install [Mono](https://www.mono-project.com/)
    - This should provide `msbuild` & `xabuild` binary
    - On Debian, after having added the repo from above, install with `apt install -t <repo_name> mono-devel msbuild`. A value for `<repo_name>` could be `stable-buster` for example, depending on which one you chose. You could also install the `mono-complete` package if you prefer.

- Install Xamarin.Android
  - ~~Option 1: Use the mono-project [CI builds](https://dev.azure.com/xamarin/public/_build/latest?definitionId=48&branchName=main&stageName=Linux)~~ **NOTE:** KP2A now requires Xamarin.Android v13, which is newer than the current CI build; until a more recent CI build is available, this option is unfortunately no longer viable.
  - Option 2: [Build it from source](https://github.com/xamarin/xamarin-android/blob/master/Documentation/README.md#building-from-source)

- Install NuGet package of your distribution
    - On Debian/Ubuntu: `apt install nuget`

- Install [libzip](https://libzip.org/) for your distribution for some Xamarin.Android versions
  - This may not be relevant anymore: for example, with Xamarin.Android 11.4.99. this is not needed.
  - Some versions of Xamarin may require `libzip4`. If you are in this case:
    - On Debian/Ubuntu, install it with `apt install libzip4`.
    - Other distributions ship only `libzip5`. As a dirty workaround, it's possible to symlink `libzip.so.5` to `libzip.so.4`. Luckily, it appears to be working. For example:
      - `sudo ln -s /usr/lib/libzip.so.5 /usr/lib/libzip.so.4`
      - or `sudo ln -s /usr/lib64/libzip.so.5 /usr/lib/libzip.so.4`

## Building the required components:

This is done on the command line and requires the Android SDK & NDK and Java JDK.

### On Windows
- Setup your environment:
  - Set these environment variables for Android's SDK & NDK
    - `ANDROID_HOME` (for example `set ANDROID_HOME=C:\PATH\TO\android-sdk`)
    - `ANDROID_SDK_ROOT` (for example `set ANDROID_SDK_ROOT=C:\PATH\TO\android-sdk`)
    - `ANDROID_NDK_ROOT` (for example `set ANDROID_NDK_ROOT=C:\PATH\TO\android-sdk\ndk\version`)
    
    **Note:** Care must be taken when setting the above variables to **not** include a trailing backslash in the path. A trailing backslash may cause `make` to fail.

    **Note**: If the path to the Android SDK contains spaces, you **must** do one of these:
    - either put the Android SDK into a path without spaces.
    - or create a symlink to that path which doesn't contain spaces. Attention: this requires **administrator** priveleges. For example:

        ```
        IF NOT EXIST C:\Android ( MKDIR C:\Android ) && 
        MKLINK /D C:\Android\android-sdk "C:\Program Files (x86)\Android\android-sdk" 
        ```
    This is because [Android NDK doesn't support being installed in a path with spaces](https://github.com/android/ndk/issues/1400).

    **Note**: The Android SDK path will require to be writeable because during the build, some missing components might be downloaded & installed.

- If you have "GNU make" available on your windows system, you may build by using the Makefile. You can also find a `make` executable in `%ANDROID_NDK_ROOT%\prebuilt\windows-x86_64\bin\make.exe`. To use it, see the instructions for Linux/macOS. Basically, just run `make` or `mingw32-make` depending on which distribution of GNU make for windows you have installed.

- Otherwise proceed as below:

    1. Build argon2

        ```
        cd src/java/argon2
        %ANDROID_NDK_ROOT%/ndk-build.cmd
        ```
    1. Build the other java components

        ```
        cd src/build-scripts
        build-java.bat
        ```

        `build-java.bat` will call `gradlew` for several Java modules.

**Notes:**

 - For building the java parts, it is suggested to keep a short name (e.g. "c:\projects\keepass2android") for the root project directory. Otherwise the Windows path length limit might be hit when building.
 - Before building the java parts, make sure you have set the ANDROID_HOME variable or create a local.properties file inside the directories with a gradlew file. It is recommended to use the same SDK location as that of the Xamarin build.
 - On some environments, `make` can fail to properly use the detected `MSBUILD` tools. This seems to be due to long pathnames and/or spaces in pathnames. It may be required to explicitly set the `MSBUILD` path using 8.3 "short" path notation:
 	- Determine the location of `MSBUILD` (e.g. `C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe`)
 	- [Generate the "short" path](https://superuser.com/a/728792) of that location (e.g.: `C:\PROGRA~1\MICROS~2\2022\COMMUN~1\MSBuild\Current\Bin\MSBuild.exe`)
 	- When running `make` specify the location of ``MSBUILD` explicitly (e.g.: `make MSBUILD="C:\PROGRA~1\MICROS~2\2022\COMMUN~1\MSBuild\Current\Bin\MSBuild.exe` 


### On Linux/macOS

- Setup your environment:
  - Set these environment variables for Android's SDK & NDK
    - `ANDROID_HOME` (for example `export ANDROID_HOME=/path/to/android-sdk/`)
    - `ANDROID_SDK_ROOT` (for example `export ANDROID_SDK_ROOT=/path/to/android-sdk/`)
    - `ANDROID_NDK_ROOT` (for example `export ANDROID_NDK_ROOT=/path/to/android-sdk/ndk/version`)

- Update your PATH environment variable so that it can access `nuget`, `msbuild` or `xabuild` (for linux):
    - On Linux:
        - add `xabuild` to your path: `export PATH=/path/to/xamarin.android-oss/bin/Release/bin/:$PATH`
    - On macOS:
        - you may similarly need to add `msbuild` & `nuget` to your PATH.

- Start the build:
    - This will use the Makefile at the root of the project (requires GNU make). To build everything (components & Keepass2Android APK) in a single command simply run:

        ```
        make
        ```

    - Otherwise, if you prefer to do step by step

        1. Build argon2

            ```
            make native
            ```

        1. Build the other java components

            ```
            make java
            ```

## Building Keepass2Android:

These are the basic steps to build Keepass2Android. You can also build Keepass2Android Offline. For this, configure the build by using the [Flavors](#Flavors).

### With Visual Studio

- On windows or on macOS open the src/KeePass.sln file with visual studio, and choose to build the project named 'keepass2android-app'

### Command Line

#### Windows, Macos & Linux
to build the APK, simply run:

```
    make
```

or to skip building the APK:

```
    make msbuild
```

## Where is the APK ?
The Apk can be installed on a device.
It is located in `src/keepass2android/bin/*/*-Signed.apk`

If you build with Visual Studio, the APK is not produced automatically. You need to perform some extra step. See the documentation of Visual Studio on how to proceed.

## Flavors

Keepass2Android is distributed in two flavors.
  - Keepass2Android (aka `net`)
  - Keepass2Android Offline (aka `nonet`)

The flavor is set through a MSBuild Property named "`Flavor`". The possible values are '`Net`' and '`NoNet`'.

The value of the Flavor property is used in 2 projects:
  - `keepass2android-app` (in `src/keepass2android`)
  - `Kp2aBusinessLogic` (in `src/keepass2android`)

Its value is set inside the `*.csproj` file (XML format) of each project in the `Project`/`PropertyGroup`/`Flavor` node.
By default its value is set to an empty string so that development is made with `AndroidManifest_debug.xml` on the '`net`' flavor.

This is the behaviour of the build system depending on the value of Flavor:
| Flavor                                     | What is built           | `AndroidManifest.xml` used  |
| -----                                      | -----                   | -----                       |
| `` (empty string): This is the default value. | Keepass2Android      | `AndroidManifest_debug.xml` |
| `Net`                                      | Keepass2Android         | `AndroidManifest_net.xml`   |
| `NoNet`                                    | Keepass2Android Offline | `AndroidManifest_nonet.xml` |

### Select/Change flavor:

When building, by default, the flavor is not set. So the value used is the value of the Flavor property in *.csproj file. This should result on doing a build of the 'net' flavor.

You can force the Flavor by setting the Flavor property.

Proceed this way:

#### Command line

##### Windows, Macos & Linux

To force building 'net' with `make`, run:

```
    make Flavor=Net
```

To build 'nonet' with `make`, run:

```
    make Flavor=NoNet
```

##### MSBuild

To build with MSBuild directly on the command line, set the flavor with `-p:Flavor=value` argument. For example:

```
    MSBuild src/KeePass.sln ... -p:Flavor=NoNet
```

#### Visual Studio
When building with Visual Studio, edit the `*.csproj` file (XML format) and set the value in the `Project`/`PropertyGroup`/`Flavor` node. This is needed only for the projects that use the flavors.

**Note:** When switching between flavors, be sure to clean the previous build before.

## Makefile

It is possible to override the project's default 'Flavor' (Net, NoNet) and 'Configuration' (Release, Debug) by passing it as argument to `make`. See the header of the Makefile to see what can be done.
