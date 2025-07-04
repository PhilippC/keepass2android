#!/usr/bin/make -f

#
# This Makefile can be used on both unix-like (use make) & windows (with GNU make)
#
# append the Configuration variable to 'make' call with value to use in '/p:Configuration='
# of dotnetbuild command.
#
# append the Flavor variable to 'make' call with value to use in '/p:Flavor='
# of dotnetbuild command.
#
# Example:
#    make Configuration=Release Flavor=NoNet
#
#
# Some targets:
#  - all: everything (including APK)
#  - native: build the native libs
#  - java: build the java libs
#  - nuget: restore NuGet packages
#  - dotnetbuild: build the project
#  - apk: same as all
#  - manifestlink: creates a symlink (to be used in building) to the AndroidManifest corresponding to the selected Flavor
#
#  - distclean: run a 'git clean -xdff'. Remove everyhing that is not in the git tree.
#  - clean: all clean_* targets below
#  - clean_native: clean native lib
#  - clean_java: call clean target of java libs
#  - clean_nuget: cleanup the 'nuget restore'
#  - clean_dotnet: call clean target of dotnetbuild
#
#
#

# Disable built-in rules to speed-up the Makefile processing.
# for example when running 'make java' on Windows it could take ~10 sec more than on linux to start building
# from what this option disables, the "clearing out the default list of suffixes for suffix rules"
# gives the most speed gain.
MAKEFLAGS += --no-builtin-rules

ifeq ($(OS),Windows_NT)     # is Windows_NT on XP, 2000, 7, Vista, 10...
    detected_OS := Windows
    WHICH := where
    RM := RMDIR /S /Q
    RMFILE := DEL
    CP := copy
    GRADLEW := gradlew.bat
    # Force use of cmd shell (don't use POSIX shell because the user may not have one installed)
    SHELL := cmd
else
    detected_OS := $(shell uname)
    WHICH := which
    RM := rm -rf
    RMFILE := $(RM)
    CP := cp
    GRADLEW := ./gradlew
endif

$(info MAKESHELL: $(MAKESHELL))
$(info SHELL: $(SHELL))
$(info )

ifeq ($(detected_OS),Linux)
  DOTNET_binary := dotnet
  DOTNET := $(shell $(WHICH) $(DOTNET_binary))
else ifeq ($(detected_OS),Windows)
  DOTNET_binary := dotnet
  DOTNET := $(shell $(WHICH) $(DOTNET_binary) 2> nul)
else
  DOTNET_binary := dotnet
  DOTNET := $(shell $(WHICH) $(DOTNET_binary))
endif

ifeq ($(DOTNET),)
  $(info )
  $(info '$(DOTNET_binary)' binary could not be found. Check it is in your PATH.)
  $(error )
endif
$(info DOTNET: $(DOTNET))
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
  DOTNET_PARAM = -p:Configuration="$(Configuration)"
else
  $(warning Configuration environment variable not set.)
endif

DELETE_MANIFEST_LINK := 
CREATE_MANIFEST_LINK := 

MANIFEST_FILE := 
ifneq ($(Flavor),)
  DOTNET_PARAM += -p:Flavor="$(Flavor)"
  ifneq ($(Flavor),)
		ifeq ($(Flavor),Debug)
			MANIFEST_FILE := AndroidManifest_debug.xml
		endif
		ifeq ($(Flavor),Net) 
			MANIFEST_FILE := AndroidManifest_net.xml
		endif
		ifeq ($(Flavor),NoNet)
			MANIFEST_FILE := AndroidManifest_nonet.xml
		endif
		ifeq ($(detected_OS),Windows)
			DELETE_MANIFEST_LINK := @cmd /c del src\keepass2android-app\AndroidManifest.xml
			CREATE_MANIFEST_LINK := @cmd /c mklink /h src\keepass2android-app\AndroidManifest.xml src\keepass2android-app\Manifests\$(MANIFEST_FILE)
		else
			DELETE_MANIFEST_LINK := rm -f src/keepass2android-app/AndroidManifest.xml
			CREATE_MANIFEST_LINK := ln -f src/keepass2android-app/Manifests/$(MANIFEST_FILE) src/keepass2android-app/AndroidManifest.xml
		endif

	endif
else
  $(warning Flavor environment variable not set.)
endif

ifneq ($(KeyStore),)
  DOTNET_PARAM += -p:AndroidKeyStore=True -p:AndroidSigningKeyStore="$(KeyStore)" -p:AndroidSigningStorePass=env:MyAndroidSigningStorePass -p:AndroidSigningKeyPass=env:MyAndroidSigningKeyPass -p:AndroidSigningKeyAlias="kp2a"
endif

ifeq ($(detected_OS),Windows)
  to_win_path=$(subst /,\,$(1))
  to_posix_path=$(subst \,/,$(1))
  define remove_dir
    if exist $(1) ( $(RM) $(1) )
  endef
  define remove_files
    $(foreach file,$(call to_win_path,$(1)), IF EXIST $(file) ( $(RMFILE) $(file) ) & )
  endef
else
  define remove_dir
    $(RM) $(1)
  endef
  define remove_files
    $(RMFILE) $(1)
  endef
endif

# Recursive wildcard: https://stackoverflow.com/a/18258352
rwildcard=$(foreach d,$(wildcard $(1:=/*)),$(call rwildcard,$d,$2) $(filter $(subst *,%,$2),$d))

$(info DOTNET_PARAM: $(DOTNET_PARAM))
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

INPUT_android-filechooser-AS := $(filter-out $(filter %/app,$(wildcard src/java/android-filechooser-AS/*)),$(wildcard src/java/android-filechooser-AS/*)) \
	$(filter-out $(filter %/build,$(wildcard src/java/android-filechooser-AS/app/*)),$(wildcard src/java/android-filechooser-AS/app/*)) \
	$(call rwildcard,src/java/android-filechooser-AS/app/src,*)

INPUT_JavaFileStorage := $(filter-out $(filter %/app,$(wildcard src/java/JavaFileStorage/*)),$(wildcard src/java/JavaFileStorage/*)) \
	$(filter-out $(filter %/build,$(wildcard src/java/JavaFileStorage/app/*)),$(wildcard src/java/JavaFileStorage/app/*)) \
	$(wildcard src/java/JavaFileStorage/libs/*) \
	$(call rwildcard,src/java/JavaFileStorage/app/src,*) \

INPUT_JavaFileStorageTest-AS := $(filter-out $(filter %/app,$(wildcard src/java/JavaFileStorageTest-AS/*)),$(wildcard src/java/JavaFileStorageTest-AS/*)) \
	$(filter-out $(filter %/build,$(wildcard src/java/JavaFileStorageTest-AS/app/*)),$(wildcard src/java/JavaFileStorageTest-AS/app/*)) \
	$(call rwildcard,src/java/JavaFileStorageTest-AS/app/src,*) \
	$(INPUT_android-filechooser-AS) \
	$(INPUT_JavaFileStorage)
OUTPUT_JavaFileStorageTest-AS = src/java/android-filechooser-AS/app/build/outputs/aar/android-filechooser-debug.aar \
	src/java/android-filechooser-AS/app/build/outputs/aar/android-filechooser-release.aar \
	src/java/JavaFileStorage/app/build/outputs/aar/JavaFileStorage-debug.aar \
	src/java/JavaFileStorage/app/build/outputs/aar/JavaFileStorage-release.aar \
	src/java/JavaFileStorageTest-AS/app/build/outputs/apk/debug/app-debug.apk \
	src/java/JavaFileStorageTest-AS/app/build/outputs/apk/release/app-release-unsigned.apk

INPUT_KP2ASoftkeyboard_AS := $(wildcard src/java/KP2ASoftkeyboard_AS/*) \
	$(wildcard src/java/KP2ASoftkeyboard_AS/app/*) \
	$(call rwildcard,src/java/KP2ASoftkeyboard_AS/app/src,*)
OUTPUT_KP2ASoftkeyboard_AS = src/java/KP2ASoftkeyboard_AS/app/build/outputs/aar/app-debug.aar \
	src/java/KP2ASoftkeyboard_AS/app/build/outputs/aar/app-release.aar

INPUT_Keepass2AndroidPluginSDK2 := $(wildcard src/java/Keepass2AndroidPluginSDK2/*) \
	$(wildcard src/java/Keepass2AndroidPluginSDK2/app/*) \
	$(call rwildcard,src/java/Keepass2AndroidPluginSDK2/app/src,*)
OUTPUT_Keepass2AndroidPluginSDK2 = src/java/Keepass2AndroidPluginSDK2/app/build/outputs/aar/app-debug.aar \
	src/java/Keepass2AndroidPluginSDK2/app/build/outputs/aar/app-release.aar

INPUT_KP2AKdbLibrary := $(wildcard src/java/KP2AKdbLibrary/*) \
	$(wildcard src/java/KP2AKdbLibrary/app/*) \
	$(call rwildcard,src/java/KP2AKdbLibrary/app/src,*)
OUTPUT_KP2AKdbLibrary = src/java/KP2AKdbLibrary/app/build/outputs/aar/app-debug.aar \
	src/java/KP2AKdbLibrary/app/build/outputs/aar/app-release.aar

INPUT_PluginQR := $(wildcard src/java/PluginQR/*) \
	$(wildcard src/java/PluginQR/app/*) \
	$(call rwildcard,src/java/PluginQR/app/src,*) \
	$(INPUT_Keepass2AndroidPluginSDK2)
OUTPUT_PluginQR = src/java/Keepass2AndroidPluginSDK2/app/build/outputs/aar/Keepass2AndroidPluginSDK2-debug.aar \
	src/java/Keepass2AndroidPluginSDK2/app/build/outputs/aar/Keepass2AndroidPluginSDK2-release.aar \
	src/java/PluginQR/app/build/outputs/apk/debug/app-debug.apk \
	src/java/PluginQR/app/build/outputs/apk/debug/app-release-unsigned.apk

##### Targets definition

.PHONY: native $(NATIVE_COMPONENTS) clean_native $(NATIVE_CLEAN_TARGETS) \
	java $(JAVA_COMPONENTS) clean_java $(JAVA_CLEAN_TARGETS) \
	nuget clean_nuget \
	dotnetbuild clean_dotnet \
	apk all clean

all: apk

##### Native Dependencies

native: $(NATIVE_COMPONENTS)

argon2: $(OUTPUT_argon2)
$(OUTPUT_argon2): $(wildcard src/java/argon2/phc-winner-argon2/src/*)  $(wildcard src/java/argon2/phc-winner-argon2/src/blake2/*)
	cd src/java/argon2 && $(ANDROID_NDK_ROOT)/ndk-build

##### Java Dependencies

java: $(JAVA_COMPONENTS)

JavaFileStorageTest-AS: $(OUTPUT_JavaFileStorageTest-AS)
KP2ASoftkeyboard_AS: $(OUTPUT_KP2ASoftkeyboard_AS)
Keepass2AndroidPluginSDK2: $(OUTPUT_Keepass2AndroidPluginSDK2)
KP2AKdbLibrary: $(OUTPUT_KP2AKdbLibrary)
PluginQR: $(OUTPUT_PluginQR)

$(OUTPUT_JavaFileStorageTest-AS): $(INPUT_JavaFileStorageTest-AS)
	$(call remove_files,$(OUTPUT_JavaFileStorageTest-AS))
	cd src/java/JavaFileStorageTest-AS && $(GRADLEW) assemble
$(OUTPUT_KP2ASoftkeyboard_AS): $(INPUT_KP2ASoftkeyboard_AS)
	$(call remove_files,$(OUTPUT_KP2ASoftkeyboard_AS))
	cd src/java/KP2ASoftkeyboard_AS && $(GRADLEW) assemble
$(OUTPUT_Keepass2AndroidPluginSDK2): $(INPUT_Keepass2AndroidPluginSDK2)
	$(call remove_files,$(OUTPUT_Keepass2AndroidPluginSDK2))
	cd src/java/Keepass2AndroidPluginSDK2 && $(GRADLEW) assemble
$(OUTPUT_KP2AKdbLibrary): $(INPUT_KP2AKdbLibrary)
	$(call remove_files,$(OUTPUT_KP2AKdbLibrary))
	cd src/java/KP2AKdbLibrary && $(GRADLEW) assemble
$(OUTPUT_PluginQR): $(INPUT_PluginQR)
	$(call remove_files,$(OUTPUT_PluginQR))
	cd src/java/PluginQR && $(GRADLEW) assemble


##### Nuget Dependencies

nuget: stamp.nuget_$(Flavor)
stamp.nuget_$(Flavor): src/KeePass.sln $(wildcard src/*/*.csproj) $(wildcard src/*/packages.config)
ifeq ($(shell $(WHICH) nuget),)
	$(error "nuget" command not found. Check it is in your PATH)
endif
	$(RMFILE) stamp.nuget_*
	nuget restore src/KeePass.sln
	$(DOTNET) restore src/KeePass.sln $(DOTNET_PARAM) -p:RestorePackagesConfig=true
	@echo "" > stamp.nuget_$(Flavor)

manifestlink:
	$(info Creating hardlink for manifest of Flavor: $(Flavor))
	$(DELETE_MANIFEST_LINK)	
	$(CREATE_MANIFEST_LINK)	

#####

dotnetbuild: manifestlink native java nuget 
	$(DOTNET) build src/KeePass.sln -target:keepass2android-app -p:AndroidSdkDirectory="$(ANDROID_SDK_ROOT)" -p:BuildProjectReferences=true $(DOTNET_PARAM) -p:Platform="Any CPU" -m

apk: manifestlink native java nuget  
	$(DOTNET) publish src/keepass2android-app/keepass2android-app.csproj -p:AndroidSdkDirectory="$(ANDROID_SDK_ROOT)" -t:SignAndroidPackage $(DOTNET_PARAM) -p:Platform=AnyCPU -m 

apk_split: manifestlink native java nuget 
	$(DOTNET) publish src/keepass2android-app/keepass2android-app.csproj -p:AndroidSdkDirectory="$(ANDROID_SDK_ROOT)" -t:SignAndroidPackage $(DOTNET_PARAM) -p:Platform=AnyCPU -m -p:RuntimeIdentifier=android-arm
	$(DOTNET) publish src/keepass2android-app/keepass2android-app.csproj -p:AndroidSdkDirectory="$(ANDROID_SDK_ROOT)" -t:SignAndroidPackage $(DOTNET_PARAM) -p:Platform=AnyCPU -m -p:RuntimeIdentifier=android-arm64
	$(DOTNET) publish src/keepass2android-app/keepass2android-app.csproj -p:AndroidSdkDirectory="$(ANDROID_SDK_ROOT)" -t:SignAndroidPackage $(DOTNET_PARAM) -p:Platform=AnyCPU -m -p:RuntimeIdentifier=android-x86
	$(DOTNET) publish src/keepass2android-app/keepass2android-app.csproj -p:AndroidSdkDirectory="$(ANDROID_SDK_ROOT)" -t:SignAndroidPackage $(DOTNET_PARAM) -p:Platform=AnyCPU -m -p:RuntimeIdentifier=android-x64
	src/build-scripts/rename-output-apks.sh src/keepass2android-app/bin/Release/net8.0-android/

build_all: dotnetbuild

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
clean_rm:
	rm -rf src/*/obj
	rm -rf src/*/bin
	rm -rf src/java/*/app/build
	rm -rf src/java/argon2/obj
	rm -rf src/java/argon2/libs
	rm -rf src/packages
	rm -rf src/java/KP2AKdbLibrary/app/.cxx
	rm -rf src/java/KP2ASoftkeyboard_AS/app/.cxx
	rm -rf src/SamsungPass/Xamarin.SamsungPass/SamsungPass/bin
	rm -rf src/SamsungPass/Xamarin.SamsungPass/SamsungPass/obj
	

# https://learn.microsoft.com/en-us/nuget/consume-packages/package-restore-troubleshooting#other-potential-conditions
clean_nuget:
	cd src && $(call remove_dir,packages)
ifeq ($(detected_OS),Windows)
	DEL /S src\project.assets.json
	DEL /S src\*.nuget.*
else
	$(RM) src/*/obj/project.assets.json
	$(RM) src/*/obj/*.nuget.*
endif
	$(RMFILE) stamp.nuget_*

clean_dotnet:
	$(DOTNET) clean src/KeePass.sln $(DOTNET_PARAM)

clean: clean_native clean_java clean_nuget clean_dotnet

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
