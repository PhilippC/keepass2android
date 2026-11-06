/*
 * Copyright (C) 2018 The Android Open Source Project
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 *
 *      http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */

/**
 * @addtogroup NdkBinder
 * @{
 */

/**
 * @file binder_auto_utils.h
 * @brief These objects provide a more C++-like thin interface to the binder.
 */

#pragma once

#include <android/binder_ibinder.h>
#include <android/binder_internal_logging.h>
#include <android/binder_parcel.h>
#include <android/binder_status.h>
#include <assert.h>
#include <string.h>
#include <unistd.h>

#include <cstddef>
#include <iostream>
#include <string>

// Old NDKs may not declare these, but forward declare (instead of compiling this class out)
// to allow code that uses ScopedAIBinder_FrozenStateChangeCallback to be more code compatible.
struct AIBinder_FrozenStateChangeCallback;

namespace ndk {

/**
 * Represents one strong pointer to an AIBinder object.
 */
class SpAIBinder {
   public:
    /**
     * Default constructor.
     */
    SpAIBinder() : mBinder(nullptr) {}

    /**
     * Takes ownership of one strong refcount of binder.
     */
    explicit SpAIBinder(AIBinder* binder) : mBinder(binder) {}

    /**
     * Convenience operator for implicitly constructing an SpAIBinder from nullptr. This is not
     * explicit because it is not taking ownership of anything.
     */
    SpAIBinder(std::nullptr_t) : SpAIBinder() {}  // NOLINT(google-explicit-constructor)

    /**
     * This will delete the underlying object if it exists. See operator=.
     */
    SpAIBinder(const SpAIBinder& other) { *this = other; }

    /**
     * This deletes the underlying object if it exists. See set.
     */
    ~SpAIBinder() { set(nullptr); }

    /**
     * This takes ownership of a binder from another AIBinder object but it does not affect the
     * ownership of that other object.
     */
    SpAIBinder& operator=(const SpAIBinder& other) {
        if (this == &other) {
            return *this;
        }
        AIBinder_incStrong(other.mBinder);
        set(other.mBinder);
        return *this;
    }

    /**
     * Takes ownership of one strong refcount of binder
     */
    void set(AIBinder* binder) {
        AIBinder* old = *const_cast<AIBinder* volatile*>(&mBinder);
        if (old != nullptr) AIBinder_decStrong(old);
        if (old != *const_cast<AIBinder* volatile*>(&mBinder)) {
            __assert(__FILE__, __LINE__, "Race detected.");
        }
        mBinder = binder;
    }

    /**
     * This returns the underlying binder object for transactions. If it is used to create another
     * SpAIBinder object, it should first be incremented.
     */
    AIBinder* get() const { return mBinder; }

    /**
     * This allows the value in this class to be set from beneath it. If you call this method and
     * then change the value of T*, you must take ownership of the value you are replacing and add
     * ownership to the object that is put in here.
     *
     * Recommended use is like this:
     *   SpAIBinder a;  // will be nullptr
     *   SomeInitFunction(a.getR());  // value is initialized with refcount
     *
     * Other usecases are discouraged.
     *
     */
    AIBinder** getR() { return &mBinder; }

   private:
    AIBinder* mBinder = nullptr;
};

#define SP_AIBINDER_COMPARE(_op_)                                                    \
    static inline bool operator _op_(const SpAIBinder& lhs, const SpAIBinder& rhs) { \
        return lhs.get() _op_ rhs.get();                                             \
    }                                                                                \
    static inline bool operator _op_(const SpAIBinder& lhs, const AIBinder* rhs) {   \
        return lhs.get() _op_ rhs;                                                   \
    }                                                                                \
    static inline bool operator _op_(const AIBinder* lhs, const SpAIBinder& rhs) {   \
        return lhs _op_ rhs.get();                                                   \
    }

SP_AIBINDER_COMPARE(!=)
SP_AIBINDER_COMPARE(<)
SP_AIBINDER_COMPARE(<=)
SP_AIBINDER_COMPARE(==)
SP_AIBINDER_COMPARE(>)
SP_AIBINDER_COMPARE(>=)
#undef SP_AIBINDER_COMPARE

namespace impl {

/**
 * This baseclass owns a single object, used to make various classes RAII.
 */
template <typename T, void (*Destroy)(T), T DEFAULT>
class ScopedAResource {
   public:
    /**
     * Takes ownership of t.
     */
    explicit ScopedAResource(T t = DEFAULT) : mT(t) {}

    /**
     * This deletes the underlying object if it exists. See set.
     */
    ~ScopedAResource() { set(DEFAULT); }

    /**
     * Takes ownership of t.
     */
    void set(T t) {
        Destroy(mT);
        mT = t;
    }

    /**
     * This returns the underlying object to be modified but does not affect ownership.
     */
    T get() { return mT; }

    /**
     * This returns the const underlying object but does not affect ownership.
     */
    const T get() const { return mT; }

    /**
     * Release the underlying resource.
     */
    [[nodiscard]] T release() {
        T a = mT;
        mT = DEFAULT;
        return a;
    }

    /**
     * This allows the value in this class to be set from beneath it. If you call this method and
     * then change the value of T*, you must take ownership of the value you are replacing and add
     * ownership to the object that is put in here.
     *
     * Recommended use is like this:
     *   ScopedAResource<T> a; // will be nullptr
     *   SomeInitFunction(a.getR()); // value is initialized with refcount
     *
     * Other usecases are discouraged.
     *
     */
    T* getR() { return &mT; }

    // copy-constructing/assignment is disallowed
    ScopedAResource(const ScopedAResource&) = delete;
    ScopedAResource& operator=(const ScopedAResource&) = delete;

    // move-constructing/assignment is okay
    ScopedAResource(ScopedAResource&& other) noexcept : mT(std::move(other.mT)) {
        other.mT = DEFAULT;
    }
    ScopedAResource& operator=(ScopedAResource&& other) noexcept {
        set(other.mT);
        other.mT = DEFAULT;
        return *this;
    }

   private:
    T mT;
};

}  // namespace impl

/**
 * Convenience wrapper. See AParcel.
 */
class ScopedAParcel : public impl::ScopedAResource<AParcel*, AParcel_delete, nullptr> {
   public:
    /**
     * Takes ownership of a.
     */
    explicit ScopedAParcel(AParcel* a = nullptr) : ScopedAResource(a) {}
    ~ScopedAParcel() {}
    ScopedAParcel(ScopedAParcel&&) = default;
    ScopedAParcel& operator=(ScopedAParcel&&) = default;

    bool operator!=(const ScopedAParcel& rhs) const { return get() != rhs.get(); }
    bool operator<(const ScopedAParcel& rhs) const { return get() < rhs.get(); }
    bool operator<=(const ScopedAParcel& rhs) const { return get() <= rhs.get(); }
    bool operator==(const ScopedAParcel& rhs) const { return get() == rhs.get(); }
    bool operator>(const ScopedAParcel& rhs) const { return get() > rhs.get(); }
    bool operator>=(const ScopedAParcel& rhs) const { return get() >= rhs.get(); }
};

/**
 * Convenience wrapper. See AStatus.
 */
class ScopedAStatus : public impl::ScopedAResource<AStatus*, AStatus_delete, nullptr> {
   public:
    /**
     * Takes ownership of a.
     *
     * WARNING: this constructor is only expected to be used when reading a
     *     status value. Use `ScopedAStatus::ok()` instead.
     */
    explicit ScopedAStatus(AStatus* a = nullptr) : ScopedAResource(a) {}
    ~ScopedAStatus() {}
    ScopedAStatus(ScopedAStatus&&) = default;
    ScopedAStatus& operator=(ScopedAStatus&&) = default;

    /**
     * See AStatus_isOk.
     */
    bool isOk() const { return get() != nullptr && AStatus_isOk(get()); }

    /**
     * See AStatus_getExceptionCode
     */
    binder_exception_t getExceptionCode() const { return AStatus_getExceptionCode(get()); }

    /**
     * See AStatus_getServiceSpecificError
     */
    int32_t getServiceSpecificError() const { return AStatus_getServiceSpecificError(get()); }

    /**
     * See AStatus_getStatus
     */
    binder_status_t getStatus() const { return AStatus_getStatus(get()); }

    /**
     * See AStatus_getMessage
     */
    const char* getMessage() const { return AStatus_getMessage(get()); }

    std::string getDescription() const {
#ifdef __ANDROID_UNAVAILABLE_SYMBOLS_ARE_WEAK__
        if (__builtin_available(android 30, *)) {
#endif

#if defined(__ANDROID_UNAVAILABLE_SYMBOLS_ARE_WEAK__) || __ANDROID_API__ >= 30
            const char* cStr = AStatus_getDescription(get());
            std::string ret = cStr;
            AStatus_deleteDescription(cStr);
            return ret;
#endif

#ifdef __ANDROID_UNAVAILABLE_SYMBOLS_ARE_WEAK__
        }
#endif

        binder_exception_t exception = getExceptionCode();
        std::string desc = std::to_string(exception);
        if (exception == EX_SERVICE_SPECIFIC) {
            desc += " (" + std::to_string(getServiceSpecificError()) + ")";
        } else if (exception == EX_TRANSACTION_FAILED) {
            desc += " (" + std::to_string(getStatus()) + ")";
        }
        if (const char* msg = getMessage(); msg != nullptr) {
            desc += ": ";
            desc += msg;
        }
        return desc;
    }

    /**
     * Convenience methods for creating scoped statuses.
     */
    static ScopedAStatus ok() { return ScopedAStatus(AStatus_newOk()); }
    static ScopedAStatus fromExceptionCode(binder_exception_t exception) {
        return ScopedAStatus(AStatus_fromExceptionCode(exception));
    }
    static ScopedAStatus fromExceptionCodeWithMessage(binder_exception_t exception,
                                                      const char* message) {
        return ScopedAStatus(AStatus_fromExceptionCodeWithMessage(exception, message));
    }
    static ScopedAStatus fromServiceSpecificError(int32_t serviceSpecific) {
        return ScopedAStatus(AStatus_fromServiceSpecificError(serviceSpecific));
    }
    static ScopedAStatus fromServiceSpecificErrorWithMessage(int32_t serviceSpecific,
                                                             const char* message) {
        return ScopedAStatus(AStatus_fromServiceSpecificErrorWithMessage(serviceSpecific, message));
    }
    static ScopedAStatus fromStatus(binder_status_t status) {
        return ScopedAStatus(AStatus_fromStatus(status));
    }
};

static inline std::ostream& operator<<(std::ostream& os, const ScopedAStatus& status) {
    return os << status.getDescription();
}

/**
 * Convenience wrapper. See AIBinder_DeathRecipient.
 */
class ScopedAIBinder_DeathRecipient
    : public impl::ScopedAResource<AIBinder_DeathRecipient*, AIBinder_DeathRecipient_delete,
                                   nullptr> {
   public:
    /**
     * Takes ownership of a.
     */
    explicit ScopedAIBinder_DeathRecipient(AIBinder_DeathRecipient* a = nullptr)
        : ScopedAResource(a) {}
    ~ScopedAIBinder_DeathRecipient() {}
    ScopedAIBinder_DeathRecipient(ScopedAIBinder_DeathRecipient&&) = default;
    ScopedAIBinder_DeathRecipient& operator=(ScopedAIBinder_DeathRecipient&&) = default;
};

namespace impl {
static void AIBinder_FrozenStateChangeCallback_delete_plumbing(
        AIBinder_FrozenStateChangeCallback* callback) {
    (void)callback;  // Suppress unused parameter warning if method body below is stripped.
#ifdef __ANDROID_UNAVAILABLE_SYMBOLS_ARE_WEAK__
    if (__builtin_available(android 37, *)) {
#endif  // __ANDROID_UNAVAILABLE_SYMBOLS_ARE_WEAK__

#if defined(__ANDROID_UNAVAILABLE_SYMBOLS_ARE_WEAK__) || __ANDROID_API__ >= 37
#if defined(__ANDROID_BINDER_HAS_FROZEN_CALLBACK__)
        AIBinder_FrozenStateChangeCallback_delete(callback);
#else
        if (callback != nullptr) __assert(__FILE__, __LINE__, "Should not be able to create callback if we can't delete it.");
#endif  // __ANDROID_BINDER_HAS_FROZEN_CALLBACK__
#endif  // __ANDROID_UNAVAILABLE_SYMBOLS_ARE_WEAK__ || __ANDROID_API__ >= 37

#ifdef __ANDROID_UNAVAILABLE_SYMBOLS_ARE_WEAK__
    }
#endif  // __ANDROID_UNAVAILABLE_SYMBOLS_ARE_WEAK__
}
}  // namespace impl

/**
 * Convenience wrapper. See AIBinder_FrozenStateChangeCallback.
 */
class ScopedAIBinder_FrozenStateChangeCallback
    : public impl::ScopedAResource<AIBinder_FrozenStateChangeCallback*,
                                   impl::AIBinder_FrozenStateChangeCallback_delete_plumbing,
                                   nullptr> {
   public:
    /**
     * Takes ownership of a.
     */
    explicit ScopedAIBinder_FrozenStateChangeCallback(AIBinder_FrozenStateChangeCallback* a =
                                                              nullptr)
        : ScopedAResource(a) {}
    ~ScopedAIBinder_FrozenStateChangeCallback() {}
    ScopedAIBinder_FrozenStateChangeCallback(ScopedAIBinder_FrozenStateChangeCallback&&) = default;
    ScopedAIBinder_FrozenStateChangeCallback& operator=(
            ScopedAIBinder_FrozenStateChangeCallback&&) = default;
};

/**
 * Convenience wrapper. See AIBinder_Weak.
 */
class ScopedAIBinder_Weak
    : public impl::ScopedAResource<AIBinder_Weak*, AIBinder_Weak_delete, nullptr> {
   public:
    /**
     * Takes ownership of a.
     */
    explicit ScopedAIBinder_Weak(AIBinder_Weak* a = nullptr) : ScopedAResource(a) {}
    ~ScopedAIBinder_Weak() {}
    ScopedAIBinder_Weak(ScopedAIBinder_Weak&&) = default;
    ScopedAIBinder_Weak& operator=(ScopedAIBinder_Weak&&) = default;

    /**
     * See AIBinder_Weak_promote.
     */
    SpAIBinder promote() const { return SpAIBinder(AIBinder_Weak_promote(get())); }
};

namespace internal {

inline void closeWithError(int fd) {
    if (fd == -1) return;
    int ret = close(fd);
    if (ret != 0) {
        syslog(LOG_ERR, "Could not close FD %d: %s", fd, strerror(errno));
    }
}

}  // namespace internal

/**
 * Convenience wrapper for a file descriptor.
 */
class ScopedFileDescriptor : public impl::ScopedAResource<int, internal::closeWithError, -1> {
   public:
    /**
     * Takes ownership of a.
     */
    ScopedFileDescriptor() : ScopedFileDescriptor(-1) {}
    explicit ScopedFileDescriptor(int a) : ScopedAResource(a) {}
    ~ScopedFileDescriptor() {}
    ScopedFileDescriptor(ScopedFileDescriptor&&) = default;
    ScopedFileDescriptor& operator=(ScopedFileDescriptor&&) = default;

    ScopedFileDescriptor dup() const { return ScopedFileDescriptor(::dup(get())); }

    bool operator!=(const ScopedFileDescriptor& rhs) const { return get() != rhs.get(); }
    bool operator<(const ScopedFileDescriptor& rhs) const { return get() < rhs.get(); }
    bool operator<=(const ScopedFileDescriptor& rhs) const { return get() <= rhs.get(); }
    bool operator==(const ScopedFileDescriptor& rhs) const { return get() == rhs.get(); }
    bool operator>(const ScopedFileDescriptor& rhs) const { return get() > rhs.get(); }
    bool operator>=(const ScopedFileDescriptor& rhs) const { return get() >= rhs.get(); }
};

}  // namespace ndk

/** @} */
