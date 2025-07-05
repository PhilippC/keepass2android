## Setup build environment
* install Android SDK
* install Android NDK
* install dotnet8

```

#from https://dotnet.microsoft.com/en-us/download/dotnet/thank-you/sdk-8.0.407-linux-x64-binaries
wget https://download.visualstudio.microsoft.com/download/pr/9d07577e-f7bc-4d60-838d-f79c50b5c11a/459ef339396783db369e0432d6dc3d7e/dotnet-sdk-8.0.407-linux-x64.tar.gz
mkdir -p $HOME/dotnet && tar zxf dotnet-sdk-8.0.407-linux-x64.tar.gz -C $HOME/dotnet
export DOTNET_ROOT=$HOME/dotnet
export PATH=$PATH:$HOME/dotnet

```

## Build Keepass2Android

```
git clone --recurse-submodules https://github.com/PhilippC/keepass2android.git
cd keepass2android/src/build-scripts
./build-java.sh && ./build-native.sh
cd ..
cd keepass2android-app
ln -s Manifests/AndroidManifest_debug.xml AndroidManifest.xml
dotnet workload restore
dotnet restore
dotnet build
```
