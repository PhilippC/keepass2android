/*
 * Copyright (C) 2023 The Android Open Source Project
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
#pragma once

#include <android/binder_api_level_utils.h>
#include <android/binder_parcel.h>
#include <android/persistable_bundle.h>
#include <sys/cdefs.h>

#include <set>
#include <sstream>

namespace aidl::android::os {

/**
 * Wrapper class that enables interop with AIDL NDK generation
 * Takes ownership of the APersistableBundle* given to it in reset() and will automatically
 * destroy it in the destructor, similar to a smart pointer container
 */
class PersistableBundle {
   public:
    PersistableBundle() noexcept {
        if (API_LEVEL_AT_LEAST(__ANDROID_API_V__)) {
            mPBundle = APersistableBundle_new();
        }
    }
    // takes ownership of the APersistableBundle*
    PersistableBundle(APersistableBundle* _Nonnull bundle) noexcept : mPBundle(bundle) {}
    // takes ownership of the APersistableBundle*
    PersistableBundle(PersistableBundle&& other) noexcept : mPBundle(other.release()) {}
    // duplicates, does not take ownership of the APersistableBundle*
    PersistableBundle(const PersistableBundle& other) {
        if (API_LEVEL_AT_LEAST(__ANDROID_API_V__)) {
            mPBundle = APersistableBundle_dup(other.mPBundle);
        }
    }
    // duplicates, does not take ownership of the APersistableBundle*
    PersistableBundle& operator=(const PersistableBundle& other) {
        if (API_LEVEL_AT_LEAST(__ANDROID_API_V__)) {
            mPBundle = APersistableBundle_dup(other.mPBundle);
        }
        return *this;
    }

    ~PersistableBundle() { reset(); }

    binder_status_t readFromParcel(const AParcel* _Nonnull parcel) {
        reset();
        if (API_LEVEL_AT_LEAST(__ANDROID_API_V__)) {
            return APersistableBundle_readFromParcel(parcel, &mPBundle);
        } else {
            return STATUS_INVALID_OPERATION;
        }
    }

    binder_status_t writeToParcel(AParcel* _Nonnull parcel) const {
        if (!mPBundle) {
            return STATUS_BAD_VALUE;
        }
        if (API_LEVEL_AT_LEAST(__ANDROID_API_V__)) {
            return APersistableBundle_writeToParcel(mPBundle, parcel);
        } else {
            return STATUS_INVALID_OPERATION;
        }
    }

    /**
     * Destroys any currently owned APersistableBundle* and takes ownership of the given
     * APersistableBundle*
     *
     * @param pBundle The APersistableBundle to take ownership of
     */
    void reset(APersistableBundle* _Nullable pBundle = nullptr) noexcept {
        if (mPBundle) {
            if (API_LEVEL_AT_LEAST(__ANDROID_API_V__)) {
                APersistableBundle_delete(mPBundle);
            }
            mPBundle = nullptr;
        }
        mPBundle = pBundle;
    }

    /**
     * Check the actual contents of the bundle for equality. This is typically
     * what should be used to check for equality.
     */
    bool deepEquals(const PersistableBundle& rhs) const {
        if (API_LEVEL_AT_LEAST(__ANDROID_API_V__)) {
            return APersistableBundle_isEqual(get(), rhs.get());
        } else {
            return false;
        }
    }

    /**
     * NOTE: This does NOT check the contents of the PersistableBundle. This is
     * implemented for ordering. Use deepEquals() to check for equality between
     * two different PersistableBundle objects.
     */
    inline bool operator==(const PersistableBundle& rhs) const { return get() == rhs.get(); }
    inline bool operator!=(const PersistableBundle& rhs) const { return get() != rhs.get(); }

    inline bool operator<(const PersistableBundle& rhs) const { return get() < rhs.get(); }
    inline bool operator>(const PersistableBundle& rhs) const { return get() > rhs.get(); }
    inline bool operator>=(const PersistableBundle& rhs) const { return !(*this < rhs); }
    inline bool operator<=(const PersistableBundle& rhs) const { return !(*this > rhs); }

    PersistableBundle& operator=(PersistableBundle&& other) noexcept {
        reset(other.release());
        return *this;
    }

    /**
     * Stops managing any contained APersistableBundle*, returning it to the caller. Ownership
     * is released.
     * @return APersistableBundle* or null if this was empty
     */
    [[nodiscard]] APersistableBundle* _Nullable release() noexcept {
        APersistableBundle* _Nullable ret = mPBundle;
        mPBundle = nullptr;
        return ret;
    }

    inline std::string toString() const {
        if (!mPBundle) {
            return "<PersistableBundle: null>";
        } else if (API_LEVEL_AT_LEAST(__ANDROID_API_V__)) {
            std::ostringstream os;
            os << "<PersistableBundle: ";
            os << "size: " << std::to_string(APersistableBundle_size(mPBundle));
            os << " >";
            return os.str();
        }
        return "<PersistableBundle (unknown)>";
    }

    int32_t size() const {
        if (API_LEVEL_AT_LEAST(__ANDROID_API_V__)) {
            return APersistableBundle_size(mPBundle);
        } else {
            return 0;
        }
    }

    int32_t erase(const std::string& key) {
        if (API_LEVEL_AT_LEAST(__ANDROID_API_V__)) {
            return APersistableBundle_erase(mPBundle, key.c_str());
        } else {
            return 0;
        }
    }

    void putBoolean(const std::string& key, bool val) {
        if (API_LEVEL_AT_LEAST(__ANDROID_API_V__)) {
            APersistableBundle_putBoolean(mPBundle, key.c_str(), val);
        }
    }

    void putInt(const std::string& key, int32_t val) {
        if (API_LEVEL_AT_LEAST(__ANDROID_API_V__)) {
            APersistableBundle_putInt(mPBundle, key.c_str(), val);
        }
    }

    void putLong(const std::string& key, int64_t val) {
        if (API_LEVEL_AT_LEAST(__ANDROID_API_V__)) {
            APersistableBundle_putLong(mPBundle, key.c_str(), val);
        }
    }

    void putDouble(const std::string& key, double val) {
        if (API_LEVEL_AT_LEAST(__ANDROID_API_V__)) {
            APersistableBundle_putDouble(mPBundle, key.c_str(), val);
        }
    }

    void putString(const std::string& key, const std::string& val) {
        if (API_LEVEL_AT_LEAST(__ANDROID_API_V__)) {
            APersistableBundle_putString(mPBundle, key.c_str(), val.c_str());
        }
    }

    void putBooleanVector(const std::string& key, const std::vector<bool>& vec) {
        if (API_LEVEL_AT_LEAST(__ANDROID_API_V__)) {
            // std::vector<bool> has no ::data().
            int32_t num = vec.size();
            if (num > 0) {
                bool* newVec = (bool*)malloc(num * sizeof(bool));
                if (newVec) {
                    for (int32_t i = 0; i < num; i++) {
                        newVec[i] = vec[i];
                    }
                    APersistableBundle_putBooleanVector(mPBundle, key.c_str(), newVec, num);
                    free(newVec);
                }
            }
        }
    }

    void putIntVector(const std::string& key, const std::vector<int32_t>& vec) {
        if (API_LEVEL_AT_LEAST(__ANDROID_API_V__)) {
            int32_t num = vec.size();
            if (num > 0) {
                APersistableBundle_putIntVector(mPBundle, key.c_str(), vec.data(), num);
            }
        }
    }
    void putLongVector(const std::string& key, const std::vector<int64_t>& vec) {
        if (API_LEVEL_AT_LEAST(__ANDROID_API_V__)) {
            int32_t num = vec.size();
            if (num > 0) {
                APersistableBundle_putLongVector(mPBundle, key.c_str(), vec.data(), num);
            }
        }
    }
    void putDoubleVector(const std::string& key, const std::vector<double>& vec) {
        if (API_LEVEL_AT_LEAST(__ANDROID_API_V__)) {
            int32_t num = vec.size();
            if (num > 0) {
                APersistableBundle_putDoubleVector(mPBundle, key.c_str(), vec.data(), num);
            }
        }
    }
    void putStringVector(const std::string& key, const std::vector<std::string>& vec) {
        if (API_LEVEL_AT_LEAST(__ANDROID_API_V__)) {
            int32_t num = vec.size();
            if (num > 0) {
                std::vector<const char*> inVec(num);
                for (int32_t i = 0; i < num; i++) {
                    inVec[i] = vec[i].c_str();
                }
                APersistableBundle_putStringVector(mPBundle, key.c_str(), inVec.data(), num);
            }
        }
    }

    void putByteVector(const std::string& key, const std::vector<uint8_t>& vec) {
        if (API_LEVEL_AT_LEAST(37)) {
            int32_t num = vec.size();
            APersistableBundle_putByteVector(mPBundle, key.c_str(), vec.data(), num);
        }
    }

    void putPersistableBundle(const std::string& key, const PersistableBundle& pBundle) {
        if (API_LEVEL_AT_LEAST(__ANDROID_API_V__)) {
            APersistableBundle_putPersistableBundle(mPBundle, key.c_str(), pBundle.mPBundle);
        }
    }

    bool getBoolean(const std::string& key, bool* _Nonnull val) const {
        if (API_LEVEL_AT_LEAST(__ANDROID_API_V__)) {
            return APersistableBundle_getBoolean(mPBundle, key.c_str(), val);
        } else {
            return false;
        }
    }

    bool getInt(const std::string& key, int32_t* _Nonnull val) const {
        if (API_LEVEL_AT_LEAST(__ANDROID_API_V__)) {
            return APersistableBundle_getInt(mPBundle, key.c_str(), val);
        } else {
            return false;
        }
    }

    bool getLong(const std::string& key, int64_t* _Nonnull val) const {
        if (API_LEVEL_AT_LEAST(__ANDROID_API_V__)) {
            return APersistableBundle_getLong(mPBundle, key.c_str(), val);
        } else {
            return false;
        }
    }

    bool getDouble(const std::string& key, double* _Nonnull val) const {
        if (API_LEVEL_AT_LEAST(__ANDROID_API_V__)) {
            return APersistableBundle_getDouble(mPBundle, key.c_str(), val);
        } else {
            return false;
        }
    }

    static char* _Nullable stringAllocator(int32_t bufferSizeBytes, void* _Nullable) {
        return (char*)malloc(bufferSizeBytes);
    }

    bool getString(const std::string& key, std::string* _Nonnull val) const {
        if (API_LEVEL_AT_LEAST(__ANDROID_API_V__)) {
            char* outString = nullptr;
            bool ret = APersistableBundle_getString(mPBundle, key.c_str(), &outString,
                                                    &stringAllocator, nullptr);
            if (ret && outString) {
                *val = std::string(outString);
                free(outString);
            }
            return ret;
        } else {
            return false;
        }
    }

    template <typename T>
    bool getVecInternal(int32_t (*_Nonnull getVec)(const APersistableBundle* _Nonnull,
                                                   const char* _Nonnull, T* _Nullable, int32_t),
                        const APersistableBundle* _Nonnull pBundle, const char* _Nonnull key,
                        std::vector<T>* _Nonnull vec) const {
        if (API_LEVEL_AT_LEAST(__ANDROID_API_V__)) {
            int32_t bytes = 0;
            // call first with nullptr to get required size in bytes
            bytes = getVec(pBundle, key, nullptr, 0);
            if (bytes > 0) {
                T* newVec = (T*)malloc(bytes);
                if (newVec) {
                    bytes = getVec(pBundle, key, newVec, bytes);
                    int32_t elements = bytes / sizeof(T);
                    vec->clear();
                    for (int32_t i = 0; i < elements; i++) {
                        vec->push_back(newVec[i]);
                    }
                    free(newVec);
                    return true;
                }
            }
        }
        return false;
    }

    bool getBooleanVector(const std::string& key, std::vector<bool>* _Nonnull vec) const {
        if (API_LEVEL_AT_LEAST(__ANDROID_API_V__)) {
            return getVecInternal<bool>(&APersistableBundle_getBooleanVector, mPBundle, key.c_str(),
                                        vec);
        }
        return false;
    }

    bool getByteVector(const std::string& key, std::vector<uint8_t>* _Nonnull vec) const {
        if (API_LEVEL_AT_LEAST(37)) {
            return getVecInternal<uint8_t>(&APersistableBundle_getByteVector, mPBundle,
                                          key.c_str(), vec);
        }
        return false;
    }

    bool getIntVector(const std::string& key, std::vector<int32_t>* _Nonnull vec) const {
        if (API_LEVEL_AT_LEAST(__ANDROID_API_V__)) {
            return getVecInternal<int32_t>(&APersistableBundle_getIntVector, mPBundle, key.c_str(),
                                           vec);
        }
        return false;
    }
    bool getLongVector(const std::string& key, std::vector<int64_t>* _Nonnull vec) const {
        if (API_LEVEL_AT_LEAST(__ANDROID_API_V__)) {
            return getVecInternal<int64_t>(&APersistableBundle_getLongVector, mPBundle, key.c_str(),
                                           vec);
        }
        return false;
    }
    bool getDoubleVector(const std::string& key, std::vector<double>* _Nonnull vec) const {
        if (API_LEVEL_AT_LEAST(__ANDROID_API_V__)) {
            return getVecInternal<double>(&APersistableBundle_getDoubleVector, mPBundle,
                                          key.c_str(), vec);
        }
        return false;
    }

    // Takes ownership of and frees the char** and its elements.
    // Creates a new set or vector based on the array of char*.
    template <typename T>
    T moveStringsInternal(char* _Nullable* _Nonnull strings, int32_t bufferSizeBytes) const {
        if (strings && bufferSizeBytes > 0) {
            int32_t num = bufferSizeBytes / sizeof(char*);
            T ret;
            for (int32_t i = 0; i < num; i++) {
                ret.insert(ret.end(), std::string(strings[i]));
                free(strings[i]);
            }
            free(strings);
            return ret;
        }
        return T();
    }

    bool getStringVector(const std::string& key, std::vector<std::string>* _Nonnull vec) const {
        if (API_LEVEL_AT_LEAST(__ANDROID_API_V__)) {
            int32_t bytes = APersistableBundle_getStringVector(mPBundle, key.c_str(), nullptr, 0,
                                                               &stringAllocator, nullptr);
            if (bytes > 0) {
                char** strings = (char**)malloc(bytes);
                if (strings) {
                    bytes = APersistableBundle_getStringVector(mPBundle, key.c_str(), strings,
                                                               bytes, &stringAllocator, nullptr);
                    *vec = moveStringsInternal<std::vector<std::string>>(strings, bytes);
                    return true;
                }
            }
        }
        return false;
    }

    bool getPersistableBundle(const std::string& key, PersistableBundle* _Nonnull val) const {
        if (API_LEVEL_AT_LEAST(__ANDROID_API_V__)) {
            APersistableBundle* bundle = nullptr;
            bool ret = APersistableBundle_getPersistableBundle(mPBundle, key.c_str(), &bundle);
            if (ret) {
                *val = PersistableBundle(bundle);
            }
            return ret;
        } else {
            return false;
        }
    }

    std::set<std::string> getKeys(
            int32_t (*_Nonnull getTypedKeys)(const APersistableBundle* _Nonnull pBundle,
                                             char* _Nullable* _Nullable outKeys,
                                             int32_t bufferSizeBytes,
                                             APersistableBundle_stringAllocator stringAllocator,
                                             void* _Nullable),
            const APersistableBundle* _Nonnull pBundle) const {
        // call first with nullptr to get required size in bytes
        int32_t bytes = getTypedKeys(pBundle, nullptr, 0, &stringAllocator, nullptr);
        if (bytes > 0) {
            char** keys = (char**)malloc(bytes);
            if (keys) {
                bytes = getTypedKeys(pBundle, keys, bytes, &stringAllocator, nullptr);
                return moveStringsInternal<std::set<std::string>>(keys, bytes);
            }
        }
        return {};
    }

    std::set<std::string> getBooleanKeys() const {
        if (API_LEVEL_AT_LEAST(__ANDROID_API_V__)) {
            return getKeys(&APersistableBundle_getBooleanKeys, mPBundle);
        } else {
            return {};
        }
    }
    std::set<std::string> getIntKeys() const {
        if (API_LEVEL_AT_LEAST(__ANDROID_API_V__)) {
            return getKeys(&APersistableBundle_getIntKeys, mPBundle);
        } else {
            return {};
        }
    }
    std::set<std::string> getLongKeys() const {
        if (API_LEVEL_AT_LEAST(__ANDROID_API_V__)) {
            return getKeys(&APersistableBundle_getLongKeys, mPBundle);
        } else {
            return {};
        }
    }
    std::set<std::string> getDoubleKeys() const {
        if (API_LEVEL_AT_LEAST(__ANDROID_API_V__)) {
            return getKeys(&APersistableBundle_getDoubleKeys, mPBundle);
        } else {
            return {};
        }
    }
    std::set<std::string> getStringKeys() const {
        if (API_LEVEL_AT_LEAST(__ANDROID_API_V__)) {
            return getKeys(&APersistableBundle_getStringKeys, mPBundle);
        } else {
            return {};
        }
    }
    std::set<std::string> getBooleanVectorKeys() const {
        if (API_LEVEL_AT_LEAST(__ANDROID_API_V__)) {
            return getKeys(&APersistableBundle_getBooleanVectorKeys, mPBundle);
        } else {
            return {};
        }
    }
    std::set<std::string> getIntVectorKeys() const {
        if (API_LEVEL_AT_LEAST(__ANDROID_API_V__)) {
            return getKeys(&APersistableBundle_getIntVectorKeys, mPBundle);
        } else {
            return {};
        }
    }
    std::set<std::string> getLongVectorKeys() const {
        if (API_LEVEL_AT_LEAST(__ANDROID_API_V__)) {
            return getKeys(&APersistableBundle_getLongVectorKeys, mPBundle);
        } else {
            return {};
        }
    }
    std::set<std::string> getDoubleVectorKeys() const {
        if (API_LEVEL_AT_LEAST(__ANDROID_API_V__)) {
            return getKeys(&APersistableBundle_getDoubleVectorKeys, mPBundle);
        } else {
            return {};
        }
    }
    std::set<std::string> getStringVectorKeys() const {
        if (API_LEVEL_AT_LEAST(__ANDROID_API_V__)) {
            return getKeys(&APersistableBundle_getStringVectorKeys, mPBundle);
        } else {
            return {};
        }
    }
    std::set<std::string> getByteVectorKeys() const {
        if (API_LEVEL_AT_LEAST(37)) {
            return getKeys(&APersistableBundle_getByteVectorKeys, mPBundle);
        } else {
            return {};
        }
    }
    std::set<std::string> getPersistableBundleKeys() const {
        if (API_LEVEL_AT_LEAST(__ANDROID_API_V__)) {
            return getKeys(&APersistableBundle_getPersistableBundleKeys, mPBundle);
        } else {
            return {};
        }
    }
    std::set<std::string> getMonKeys() const {
        // :P
        return {"c(o,o)b", "c(o,o)b"};
    }

   private:
    inline APersistableBundle* _Nullable get() const { return mPBundle; }
    APersistableBundle* _Nullable mPBundle = nullptr;
};

}  // namespace aidl::android::os
