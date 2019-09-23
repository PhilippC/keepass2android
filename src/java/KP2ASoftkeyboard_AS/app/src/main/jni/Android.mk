LOCAL_PATH := $(call my-dir)
include $(CLEAR_VARS)

LOCAL_MODULE := jni_latinime
LOCAL_LDFLAGS := -Wl,--build-id
LOCAL_SRC_FILES := \
	source/keepass2android_softkeyboard_BinaryDictionary.cpp \
	source/char_utils.cpp \
	source/dictionary.cpp \

LOCAL_C_INCLUDES += $(LOCAL_PATH)//include/

include $(BUILD_SHARED_LIBRARY)
