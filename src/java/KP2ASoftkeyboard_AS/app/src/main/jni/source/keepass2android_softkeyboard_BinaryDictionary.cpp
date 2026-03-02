/*
**
** Copyright 2009, The Android Open Source Project
**
** Licensed under the Apache License, Version 2.0 (the "License");
** you may not use this file except in compliance with the License.
** You may obtain a copy of the License at
**
**     http://www.apache.org/licenses/LICENSE-2.0
**
** Unless required by applicable law or agreed to in writing, software
** distributed under the License is distributed on an "AS IS" BASIS,
** WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
** See the License for the specific language governing permissions and
** limitations under the License.
*/

#include <stdio.h>
#include <assert.h>
#include <unistd.h>
#include <fcntl.h>

#include <jni.h>
#include "dictionary.h"

// ----------------------------------------------------------------------------

using namespace latinime;

//
// helper function to throw an exception
//
static void throwException(JNIEnv *env, const char* ex, const char* fmt, int data)
{
    if (jclass cls = env->FindClass(ex)) {
        char msg[1000];
        sprintf(msg, fmt, data);
        env->ThrowNew(cls, msg);
        env->DeleteLocalRef(cls);
    }
}

extern "C"
JNIEXPORT jlong JNICALL Java_keepass2android_softkeyboard_BinaryDictionary_openNative
        (JNIEnv *env, jobject object, jobject dictDirectBuffer,
         jint typedLetterMultiplier, jint fullWordMultiplier, jint size)
{
    void *dict = env->GetDirectBufferAddress(dictDirectBuffer);
    if (dict == NULL) {
        fprintf(stderr, "DICT: Dictionary buffer is null\n");
        return 0;
    }
    Dictionary *dictionary = new Dictionary(dict, typedLetterMultiplier, fullWordMultiplier, size);
    return (jlong) dictionary;
}

extern "C"
JNIEXPORT int JNICALL Java_keepass2android_softkeyboard_BinaryDictionary_getSuggestionsNative(
        JNIEnv *env, jobject object, jlong dict, jintArray inputArray, jint arraySize,
        jcharArray outputArray, jintArray frequencyArray, jint maxWordLength, jint maxWords,
        jint maxAlternatives, jint skipPos, jintArray nextLettersArray, jint nextLettersSize)
{
    Dictionary *dictionary = (Dictionary*) dict;
    if (dictionary == NULL) return 0;

    int *frequencies = env->GetIntArrayElements(frequencyArray, NULL);
    int *inputCodes = env->GetIntArrayElements(inputArray, NULL);
    jchar *outputChars = env->GetCharArrayElements(outputArray, NULL);
    int *nextLetters = nextLettersArray != NULL ? env->GetIntArrayElements(nextLettersArray, NULL)
            : NULL;

    int count = dictionary->getSuggestions(inputCodes, arraySize, (unsigned short*) outputChars,
            frequencies, maxWordLength, maxWords, maxAlternatives, skipPos, nextLetters,
            nextLettersSize);

    env->ReleaseIntArrayElements(frequencyArray, frequencies, 0);
    env->ReleaseIntArrayElements(inputArray, inputCodes, JNI_ABORT);
    env->ReleaseCharArrayElements(outputArray, outputChars, 0);
    if (nextLetters) {
        env->ReleaseIntArrayElements(nextLettersArray, nextLetters, 0);
    }

    return count;
}

extern "C"
JNIEXPORT int JNICALL Java_keepass2android_softkeyboard_BinaryDictionary_getBigramsNative
        (JNIEnv *env, jobject object, jlong dict, jcharArray prevWordArray, jint prevWordLength,
         jintArray inputArray, jint inputArraySize, jcharArray outputArray,
         jintArray frequencyArray, jint maxWordLength, jint maxBigrams, jint maxAlternatives)
{
    Dictionary *dictionary = (Dictionary*) dict;
    if (dictionary == NULL) return 0;

    jchar *prevWord = env->GetCharArrayElements(prevWordArray, NULL);
    int *inputCodes = env->GetIntArrayElements(inputArray, NULL);
    jchar *outputChars = env->GetCharArrayElements(outputArray, NULL);
    int *frequencies = env->GetIntArrayElements(frequencyArray, NULL);

    int count = dictionary->getBigrams((unsigned short*) prevWord, prevWordLength, inputCodes,
            inputArraySize, (unsigned short*) outputChars, frequencies, maxWordLength, maxBigrams,
            maxAlternatives);

    env->ReleaseCharArrayElements(prevWordArray, prevWord, JNI_ABORT);
    env->ReleaseIntArrayElements(inputArray, inputCodes, JNI_ABORT);
    env->ReleaseCharArrayElements(outputArray, outputChars, 0);
    env->ReleaseIntArrayElements(frequencyArray, frequencies, 0);

    return count;
}


extern "C"
JNIEXPORT jboolean JNICALL Java_keepass2android_softkeyboard_isValidWordNative
        (JNIEnv *env, jobject object, jlong dict, jcharArray wordArray, jint wordLength)
{
    Dictionary *dictionary = (Dictionary*) dict;
    if (dictionary == NULL) return (jboolean) false;

    jchar *word = env->GetCharArrayElements(wordArray, NULL);
    jboolean result = dictionary->isValidWord((unsigned short*) word, wordLength);
    env->ReleaseCharArrayElements(wordArray, word, JNI_ABORT);

    return result;
}

extern "C"
JNIEXPORT void JNICALL Java_keepass2android_softkeyboard_BinaryDictionary_closeNative
        (JNIEnv *env, jobject object, jlong dict)
{
    Dictionary *dictionary = (Dictionary*) dict;
    delete (Dictionary*) dict;
}
extern "C"
JNIEXPORT jboolean JNICALL
Java_keepass2android_softkeyboard_BinaryDictionary_isValidWordNative(JNIEnv *env, jobject thiz,
                                                                     jlong dict,
                                                                     jcharArray wordArray,
                                                                     jint wordLength) {
    Dictionary *dictionary = (Dictionary*) dict;
    if (dictionary == NULL) return (jboolean) false;

    jchar *word = env->GetCharArrayElements(wordArray, NULL);
    jboolean result = dictionary->isValidWord((unsigned short*) word, wordLength);
    env->ReleaseCharArrayElements(wordArray, word, JNI_ABORT);

    return result;
}
