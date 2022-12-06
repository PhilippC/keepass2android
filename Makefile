#!/usr/bin/make -f

#
# This Makefile can be used on both unix-like (use make) & windows (with GNU make)
#
# append the Configuration variable to 'make' call with value to use in '/p:Configuration='
# of msbuild command.
#
# Example:
#    make Configuration=Release
#
#
# Some targets:
#  - all: everything (including APK)
#  - native: build the native libs
#  - java: build the java libs
#  - nuget: restore NuGet packages
#  - msbuild: build the project
#  - apk: same as all
#
#  - distclean: run a 'git clean -xdff'. Remove everyhing that is not in the git tree.
#  - clean: all clean_* targets below
#  - clean_native: clean native lib
#  - clean_java: call clean target of java libs
#  - msbuild_clean: call clean target of java libs
#
#
#

ifeq ($(OS),Windows_NT)     # is Windows_NT on XP, 2000, 7, Vista, 10...
    detected_OS := Windows
    WHICH := where
    RM := RMDIR /S /Q
    CP := copy
    GRADLEW := gradlew.bat
    # Force use of cmd shell (don't use POSIX shell because the user may not have one installed)
    SHELL := cmd
else
    detected_OS := $(shell uname)
    WHICH := which
    RM := rm -rf
    CP := cp
    GRADLEW := ./gradlew
endif

$(info MAKESHELL: $(MAKESHELL))
$(info SHELL: $(SHELL))
$(info )

# On linux use xabuild, on Windows use MSBuild.exe, otherwise (macos?) use msbuild.
ifeq ($(detected_OS),Linux)
  MSBUILD_binary := xabuild
  MSBUILD := $(shell $(WHICH) $(MSBUILD_binary))
else ifeq ($(detected_OS),Windows)
  MSBUILD_binary := MSBuild.exe
  MSBUILD := $(shell $(WHICH) $(MSBUILD_binary) 2> nul)
  ifeq ($(MSBUILD),)
    # Additional heuristic to find MSBUILD_BINARY on Windows
    VSWHERE := "%ProgramFiles(x86)%\Microsoft Visual Studio\Installer\vswhere.exe"
    VSWHERE_CHECK := $(shell @echo off & $(VSWHERE) 2> nul || echo VSWHERE_NOT_FOUND)
    ifneq ($(VSWHERE_CHECK),VSWHERE_NOT_FOUND)
      MSBUILD := $(shell @echo off & $(VSWHERE) -latest -prerelease -products * -requires Microsoft.Component.MSBuild -find MSBuild\**\Bin\MSBuild.exe)
      VS_INSTALL_PATH := $(shell @echo off & $(VSWHERE) -property installationPath)
    endif
  endif
else
  MSBUILD_binary := msbuild
  MSBUILD := $(shell $(WHICH) $(MSBUILD_binary))
endif

ifeq ($(MSBUILD),)
  $(info )
  $(info '$(MSBUILD_binary)' binary could not be found. Check it is in your PATH.)
  ifeq ($(detected_OS),Windows)
  ifneq ($(VSWHERE_CHECK),VSWHERE_NOT_FOUND)
    $(info )
    $(info You may retry after running in the command prompt:)
    $(info )
    $(info "$(VS_INSTALL_PATH)\VC\Auxiliary\Build\vcvarsall.bat" x86_amd64)
    $(info )
    $(info If this doesn't work, install/find the location of vcvarsall.bat)
    $(info or install and add msbuild.exe to your PATH)
    $(info )
  endif
  endif
  $(error )
endif
$(info MSBUILD: $(MSBUILD))
$(info )

ifeq ($(ANDROID_SDK_ROOT),)
  $(error set ANDROID_SDK_ROOT environment variable)
endif
$(info ANDROID_SDK_ROOT: $(ANDROID_SDK_ROOT))

ifeq ($(ANDROID_HOME),)
  $(error set ANDROID_HOME environment variable)
endif
$(info ANDROID_HOME: $(ANDROID_SDK_ROOT))

ifeq ($(ANDROID_NDK_ROOT),)
  $(error set ANDROID_NDK_ROOT environment variable)
endif
$(info ANDROID_NDK_ROOT: $(ANDROID_NDK_ROOT))

ifneq ($(Configuration),)
  MSBUILD_PARAM = -p:Configuration="$(Configuration)"
else
  $(warning Configuration environment variable not set.)
endif

ifeq ($(detected_OS),Windows)
  define remove
    if exist $(1) ( $(RM) $(1) )
  endef
else
  define remove
    $(RM) $(1)
  endef
endif

$(info MSBUILD_PARAM: $(MSBUILD_PARAM))
$(info nuget path: $(shell $(WHICH) nuget))
$(info )

NATIVE_COMPONENTS := argon2
NATIVE_CLEAN_TARGETS := clean_argon2

OUTPUT_argon2 = src/java/argon2/libs/armeabi-v7a/libargon2.so \
	src/java/argon2/libs/arm64-v8a/libargon2.so \
	src/java/argon2/libs/x86/libargon2.so \
	src/java/argon2/libs/x86_64/libargon2.so

JAVA_COMPONENTS := \
	JavaFileStorageTest-AS \
	KP2ASoftkeyboard_AS \
	Keepass2AndroidPluginSDK2 \
	KP2AKdbLibrary
	#PluginQR # Doesn't seem required
JAVA_CLEAN_TARGETS := \
	clean_JavaFileStorageTest-AS \
	clean_KP2ASoftkeyboard_AS \
	clean_Keepass2AndroidPluginSDK2 \
	clean_KP2AKdbLibrary \
	clean_PluginQR

OUTPUT_JavaFileStorageTest-AS = src/java/JavaFileStorage/app/build/outputs/aar/JavaFileStorage-debug.aar src/java/android-filechooser-AS/app/build/outputs/aar/android-filechooser-release.aar
OUTPUT_KP2ASoftkeyboard_AS =src/java/KP2ASoftkeyboard_AS/app/build/outputs/aar/app-debug.aar
OUTPUT_Keepass2AndroidPluginSDK2 = src/java/Keepass2AndroidPluginSDK2/app/build/outputs/aar/app-release.aar
OUTPUT_KP2AKdbLibrary = src/java/KP2AKdbLibrary/app/build/outputs/aar/app-debug.aar
OUTPUT_PluginQR = src/java/Keepass2AndroidPluginSDK2/app/build/outputs/aar/Keepass2AndroidPluginSDK2-release.aar src/java/PluginQR/app/build/outputs/apk/debug/app-debug.apk

##### Targets definition

.PHONY: native $(NATIVE_COMPONENTS) clean_native $(NATIVE_CLEAN_TARGETS)  \
	java $(JAVA_COMPONENTS) clean_java $(JAVA_CLEAN_TARGETS) \
	nuget \
	msbuild clean_msbuild \
	apk all clean

all: apk

##### Native Dependencies

native: $(NATIVE_COMPONENTS)

argon2: $(OUTPUT_argon2)
$(OUTPUT_argon2):
	cd src/java/argon2 && $(ANDROID_NDK_ROOT)/ndk-build

##### Java Dependencies

java: $(JAVA_COMPONENTS)

JavaFileStorageTest-AS: $(OUTPUT_JavaFileStorageTest-AS)
KP2ASoftkeyboard_AS: $(OUTPUT_KP2ASoftkeyboard_AS)
Keepass2AndroidPluginSDK2: $(OUTPUT_Keepass2AndroidPluginSDK2)
KP2AKdbLibrary: $(OUTPUT_KP2AKdbLibrary)
PluginQR: $(OUTPUT_PluginQR)

$(OUTPUT_JavaFileStorageTest-AS):
	cd src/java/JavaFileStorageTest-AS && $(GRADLEW) assemble
$(OUTPUT_KP2ASoftkeyboard_AS):
	cd src/java/KP2ASoftkeyboard_AS && $(GRADLEW) assemble
$(OUTPUT_Keepass2AndroidPluginSDK2):
	cd src/java/Keepass2AndroidPluginSDK2 && $(GRADLEW) assemble
$(OUTPUT_KP2AKdbLibrary):
	cd src/java/KP2AKdbLibrary && $(GRADLEW) assemble
$(OUTPUT_PluginQR):
	cd src/java/PluginQR && $(GRADLEW) assemble


##### Nuget Dependencies

nuget:
ifeq ($(shell $(WHICH) nuget),)
	$(error "nuget" command not found. Check it is in your PATH)
endif
	nuget restore src/KeePass.sln

#####
src/Kp2aBusinessLogic/Io/DropboxFileStorageKeys.cs:
ifeq ($(detected_OS),Windows)
	$(CP) src\Kp2aBusinessLogic\Io\DropboxFileStorageKeysDummy.cs src\Kp2aBusinessLogic\Io\DropboxFileStorageKeys.cs
else
	$(CP) src/Kp2aBusinessLogic/Io/DropboxFileStorageKeysDummy.cs $@
endif

msbuild: native java nuget src/Kp2aBusinessLogic/Io/DropboxFileStorageKeys.cs
	$(MSBUILD) src/KeePass.sln -target:keepass2android-app -p:AndroidSdkDirectory="$(ANDROID_SDK_ROOT)" -p:BuildProjectReferences=true $(MSBUILD_PARAM) -p:Platform="Any CPU" -m

apk: msbuild
	$(MSBUILD) src/keepass2android/keepass2android-app.csproj -p:AndroidSdkDirectory="$(ANDROID_SDK_ROOT)" -t:SignAndroidPackage $(MSBUILD_PARAM) -p:Platform=AnyCPU -m

build_all: msbuild

##### Cleanup targets

clean_native: $(NATIVE_CLEAN_TARGETS)
clean_argon2:
	cd src/java/argon2 && $(ANDROID_NDK_ROOT)/ndk-build clean

clean_java: $(JAVA_CLEAN_TARGETS)
clean_JavaFileStorageTest-AS:
	cd src/java/JavaFileStorageTest-AS && $(GRADLEW) clean
clean_KP2ASoftkeyboard_AS:
	cd src/java/KP2ASoftkeyboard_AS && $(GRADLEW) clean
clean_Keepass2AndroidPluginSDK2:
	cd src/java/Keepass2AndroidPluginSDK2 && $(GRADLEW) clean
clean_KP2AKdbLibrary:
	cd src/java/KP2AKdbLibrary && $(GRADLEW) clean
clean_PluginQR:
	cd src/java/PluginQR && $(GRADLEW) clean

clean_msbuild:
	$(MSBUILD) src/KeePass.sln -target:clean $(MSBUILD_PARAM)

clean: clean_native clean_java clean_msbuild

distclean: clean
ifneq ("$(wildcard ./allow_git_clean)","")
ifeq ($(shell $(WHICH) git),)
	$(error "git" command not found. Check it is in your PATH)
endif
	git clean -xdff src
else
	$(warning 'git clean' skipped for safety reasons. See hint below:)
	$(info )
	$(info 'git clean' would delete all untracked files, those in '.gitignore' and those in '.git/info/exclude'.)
	$(info )
	$(info Check which files would be deleted by running: "git clean -n -xdff src")
	$(info If listed files are acceptable, you can enable the call to "git clean" by creating an empty file named 'allow_git_clean' next to the Makefile.)
	$(info )
endif
