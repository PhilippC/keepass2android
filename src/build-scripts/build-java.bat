cd ..\java\JavaFileStorageTest-AS
call ./gradlew clean assemble || exit /b
cd ..\..\build-scripts

cd ..\java\KP2ASoftkeyboard_AS
call ./gradlew clean assemble || exit /b
cd ..\..\build-scripts

cd ..\java\Keepass2AndroidPluginSDK2
call ./gradlew clean assemble || exit /b
cd ..\..\build-scripts

cd ..\java\KP2AKdbLibrary
call ./gradlew clean assemble || exit /b
cd ..\..\build-scripts
