//
// Created by Philipp on 21.09.2019.
//

#include <jni.h>

extern "C"
JNIEXPORT jstring JNICALL Java_keepass2android_softkeyboard_BinaryDictionary_getNativeString(
        JNIEnv *env, jobject obj) {
    return env->NewStringUTF("Hello World! From native code!");
}