#!/usr/bin/make -f

# This Makefile is to be run on unix-like systems (MacOS, Linux...)

CONFIG_DEFAULT = Debug

ifeq ($(shell uname), Linux)
  MSBUILD = xabuild
else
  MSBUILD = msbuild
endif

ifeq ($(ANDROID_NDK_ROOT),)
  $(error set ANDROID_NDK_ROOT environment variable)
endif
ifeq ($(ANDROID_SDK_ROOT),)
  $(error set ANDROID_SDK_ROOT environment variable)
endif
ifeq ($(ANDROID_HOME),)
  $(error set ANDROID_HOME environment variable)
endif
ifeq ($(CONFIG),)
  CONFIG = $(CONFIG_DEFAULT)
  $(warning CONFIG environment variable not set. Fallback to 'Release')
endif

$(info MSBUILD binary: $(MSBUILD))
$(info MSBUILD path: $(shell which $(MSBUILD)))
$(info ANDROID_SDK_ROOT: $(ANDROID_SDK_ROOT))
$(info ANDROID_HOME: $(ANDROID_SDK_ROOT))
$(info ANDROID_NDK_ROOT: $(ANDROID_NDK_ROOT))
$(info CONFIG: $(CONFIG))

ANDROID_ABIS = armeabi-v7a arm64-v8a x86 x86_64

JAVA_COMPONENTS := \
	JavaFileStorageTest-AS \
	KP2ASoftkeyboard_AS \
	Keepass2AndroidPluginSDK2 \
	KP2AKdbLibrary \
	PluginQR

NATIVE_COMPONENTS := argon2

OUTPUT_ARGON2 = $(addsuffix /libargon2.so, $(addprefix src/java/argon2/libs/,${ANDROID_ABIS}))

OUTPUT_JavaFileStorageTest-AS = src/java/JavaFileStorage/app/build/outputs/aar/JavaFileStorage-debug.aar
OUTPUT_KP2ASoftkeyboard_AS =src/java/KP2ASoftkeyboard_AS/app/build/outputs/aar/app-debug.aar
#OUTPUT_Keepass2AndroidPluginSDK2 = src/java/Keepass2AndroidPluginSDK2/app/build/outputs/aar/Keepass2AndroidPluginSDK2-release.aar
OUTPUT_Keepass2AndroidPluginSDK2 = src/java/Keepass2AndroidPluginSDK2/app/build/outputs/aar/app-release.aar
OUTPUT_KP2AKdbLibrary = src/java/KP2AKdbLibrary/app/build/outputs/aar/app-debug.aar
OUTPUT_PluginQR = src/java/android-filechooser-AS/app/build/outputs/aar/android-filechooser-release.aar


#### Targets definition

.PHONY: native $(NATIVE_COMPONENTS) clean_native clean_native_argon2  \
	java $(JAVA_COMPONENTS) clean_java $(addprefix clean_java_,$(JAVA_COMPONENTS)) \
	nuget msbuild apk all clean

all: apk

#### Native Dependencies

native: $(NATIVE_COMPONENTS)

argon2: $(OUTPUT_ARGON2)
$(OUTPUT_ARGON2):
	cd src/java/argon2 && $$ANDROID_NDK_ROOT/ndk-build

##### Java Dependencies

JavaFileStorageTest-AS: $(OUTPUT_JavaFileStorageTest-AS)
KP2ASoftkeyboard_AS: $(OUTPUT_KP2ASoftkeyboard_AS)
Keepass2AndroidPluginSDK2: $(OUTPUT_Keepass2AndroidPluginSDK2)
KP2AKdbLibrary: $(OUTPUT_KP2AKdbLibrary)
PluginQR: $(OUTPUT_PluginQR)

$(OUTPUT_JavaFileStorageTest-AS):
	cd src/java/JavaFileStorageTest-AS && ./gradlew assemble
$(OUTPUT_KP2ASoftkeyboard_AS):
	cd src/java/KP2ASoftkeyboard_AS && ./gradlew assemble
$(OUTPUT_Keepass2AndroidPluginSDK2):
	cd src/java/Keepass2AndroidPluginSDK2 && ./gradlew assemble
$(OUTPUT_KP2AKdbLibrary):
	cd src/java/KP2AKdbLibrary && ./gradlew assemble
$(OUTPUT_PluginQR):
	cd src/java/PluginQR && ./gradlew assemble

java: $(JAVA_COMPONENTS)

##### Nuget Dependencies

nuget:
	nuget restore src/KeePass.sln

#####
src/Kp2aBusinessLogic/Io/DropboxFileStorageKeys.cs:
	cp src/Kp2aBusinessLogic/Io/DropboxFileStorageKeysDummy.cs $@

msbuild: native java nuget src/Kp2aBusinessLogic/Io/DropboxFileStorageKeys.cs
	cd src/keepass2android && ./UseManifestDebug.sh
	$(MSBUILD) src/KeePass.sln /target:keepass2android-app /p:AndroidSdkDirectory=$(ANDROID_SDK_ROOT) /p:BuildProjectReferences=true /p:Configuration="$(CONFIG)" /p:Platform="Any CPU"

apk: msbuild
	$(MSBUILD) src/keepass2android-app.csproj /p:AndroidSdkDirectory=$(ANDROID_SDK_ROOT) /t:SignAndroidPackage /p:Configuration="$(CONFIG)" /p:Platform=AnyCPU

build_all: msbuild

##### Cleanup targets

clean_native: $(addprefix clean_,$(NATIVE_TARGET))
clean_native_argon2:
	rm -rf src/java/argon2/libs/
	rm -rf src/java/argon2/obj/

clean_java: $(addprefix clean_java_,$(JAVA_COMPONENTS))
clean_java_JavaFileStorageTest-AS:
	cd src/java/JavaFileStorageTest-AS && ./gradlew clean
clean_java_KP2ASoftkeyboard_AS:
	cd src/java/KP2ASoftkeyboard_AS && ./gradlew clean
clean_java_Keepass2AndroidPluginSDK2:
	cd src/java/Keepass2AndroidPluginSDK2 && ./gradlew clean
clean_java_KP2AKdbLibrary:
	cd src/java/KP2AKdbLibrary && ./gradlew clean
clean_java_PluginQR:
	cd src/java/PluginQR && ./gradlew clean

clean_msbuild:
	$(MSBUILD) src/KeePass.sln /target:clean
	rm -fr src/keepass2android/Properties/AndroidManifest.xml

clean: clean_native clean_java clean_msbuild

distclean: clean
	git clean -xdff src
