cd ..\java\JavaFileStorageTest-AS
./gradlew -q :app:dependencies > ../../build-scripts/dependencies-JavaFileStorageTest-AS.txt

cd ..\KP2ASoftkeyboard_AS
./gradlew -q :app:dependencies > ../../build-scripts/dependencies-KP2ASoftkeyboard_AS.txt

cd ..\Keepass2AndroidPluginSDK2
./gradlew -q :app:dependencies > ../../build-scripts/dependencies-Keepass2AndroidPluginSDK2.txt


cd ..\KP2AKdbLibrary
./gradlew -q :app:dependencies > ../../build-scripts/dependencies-KP2AKdbLibrary.txt

cd ..\PluginQR
./gradlew -q :app:dependencies > ../../build-scripts/dependencies-PluginQR.txt

cd ..\..\build-scripts
