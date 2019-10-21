LOCAL_PATH := $(call my-dir)

include $(CLEAR_VARS)

LOCAL_MODULE := argon2

LOCAL_SRC_FILES := \
	../phc-winner-argon2/src/argon2.c \
	../phc-winner-argon2/src/core.c \
	../phc-winner-argon2/src/blake2/blake2b.c \
	../phc-winner-argon2/src/thread.c \
	../phc-winner-argon2/src/encoding.c \
	../phc-winner-argon2/src/ref.c

LOCAL_CFLAGS += -I $(LOCAL_PATH)/../phc-winner-argon2/include

include $(BUILD_SHARED_LIBRARY)

