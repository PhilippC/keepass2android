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
 * @file binder_parcel_utils.h
 * @brief A collection of helper wrappers for AParcel.
 */

#pragma once

#include <android/binder_auto_utils.h>
#include <android/binder_interface_utils.h>
#include <android/binder_internal_logging.h>
#include <android/binder_parcel.h>

#include <array>
#include <optional>
#include <string>
#include <type_traits>
#include <vector>

namespace ndk {

namespace {
template <typename Test, template <typename...> class Ref>
struct is_specialization : std::false_type {};

template <template <typename...> class Ref, typename... Args>
struct is_specialization<Ref<Args...>, Ref> : std::true_type {};

template <typename Test, template <typename...> class Ref>
static inline constexpr bool is_specialization_v = is_specialization<Test, Ref>::value;

// Get the first template type from a container, the T from MyClass<T, ...>.
template <typename T>
struct first_template_type {
    using type = void;
};

template <template <typename...> class V, typename T, typename... Args>
struct first_template_type<V<T, Args...>> {
    using type = T;
};

template <typename T>
using first_template_type_t = typename first_template_type<T>::type;

// Tells if T represents NDK interface (shared_ptr<ICInterface-derived>)
template <typename T>
static inline constexpr bool is_interface_v = is_specialization_v<T, std::shared_ptr>&&
        std::is_base_of_v<::ndk::ICInterface, first_template_type_t<T>>;

// Tells if T represents NDK parcelable with readFromParcel/writeToParcel methods defined
template <typename T, typename = void>
struct is_parcelable : std::false_type {};

template <typename T>
struct is_parcelable<
        T, std::void_t<decltype(std::declval<T>().readFromParcel(std::declval<const AParcel*>())),
                       decltype(std::declval<T>().writeToParcel(std::declval<AParcel*>()))>>
    : std::true_type {};

template <typename T>
static inline constexpr bool is_parcelable_v = is_parcelable<T>::value;

// Tells if T represents nullable NDK parcelable (optional<parcelable> or unique_ptr<parcelable>)
template <typename T>
static inline constexpr bool is_nullable_parcelable_v = is_parcelable_v<first_template_type_t<T>> &&
                                                        (is_specialization_v<T, std::optional> ||
                                                         is_specialization_v<T, std::unique_ptr>);

// Tells if T is a fixed-size array.
template <typename T>
struct is_fixed_array : std::false_type {};

template <typename T, size_t N>
struct is_fixed_array<std::array<T, N>> : std::true_type {};

template <typename T>
static inline constexpr bool is_fixed_array_v = is_fixed_array<T>::value;

template <typename T>
static inline constexpr bool dependent_false_v = false;
}  // namespace

/**
 * This checks the length against the array size and retrieves the buffer. No allocation required.
 */
template <typename T, size_t N>
static inline bool AParcel_stdArrayAllocator(void* arrayData, int32_t length, T** outBuffer) {
    if (length < 0) return false;

    if (length != static_cast<int32_t>(N)) {
        return false;
    }

    std::array<T, N>* arr = static_cast<std::array<T, N>*>(arrayData);
    *outBuffer = arr->data();
    return true;
}

/**
 * This checks the length against the array size and retrieves the buffer. No allocation required.
 */
template <typename T, size_t N>
static inline bool AParcel_nullableStdArrayAllocator(void* arrayData, int32_t length,
                                                     T** outBuffer) {
    std::optional<std::array<T, N>>* arr = static_cast<std::optional<std::array<T, N>>*>(arrayData);
    if (length < 0) {
        *arr = std::nullopt;
        return true;
    }

    if (length != static_cast<int32_t>(N)) {
        return false;
    }

    arr->emplace();
    *outBuffer = (*arr)->data();
    return true;
}

/**
 * This checks the length against the array size. No allocation required.
 */
template <size_t N>
static inline bool AParcel_stdArrayExternalAllocator(void* arrayData, int32_t length) {
    (void)arrayData;
    return length == static_cast<int32_t>(N);
}

/**
 * This checks the length against the array size. No allocation required.
 */
template <typename T, size_t N>
static inline bool AParcel_nullableStdArrayExternalAllocator(void* arrayData, int32_t length) {
    std::optional<std::array<T, N>>* arr = static_cast<std::optional<std::array<T, N>>*>(arrayData);

    if (length < 0) {
        *arr = std::nullopt;
        return true;
    }

    if (length != static_cast<int32_t>(N)) {
        return false;
    }

    arr->emplace();
    return true;
}

/**
 * This retrieves and allocates a vector to size 'length' and returns the underlying buffer.
 */
template <typename T>
static inline bool AParcel_stdVectorAllocator(void* vectorData, int32_t length, T** outBuffer) {
    if (length < 0) return false;

    std::vector<T>* vec = static_cast<std::vector<T>*>(vectorData);
    if (static_cast<size_t>(length) > vec->max_size()) return false;

    vec->resize(static_cast<size_t>(length));
    *outBuffer = vec->data();
    return true;
}

/**
 * This retrieves and allocates a vector to size 'length' and returns the underlying buffer.
 */
template <typename T>
static inline bool AParcel_nullableStdVectorAllocator(void* vectorData, int32_t length,
                                                      T** outBuffer) {
    std::optional<std::vector<T>>* vec = static_cast<std::optional<std::vector<T>>*>(vectorData);

    if (length < 0) {
        *vec = std::nullopt;
        return true;
    }

    *vec = std::optional<std::vector<T>>(std::vector<T>{});

    if (static_cast<size_t>(length) > (*vec)->max_size()) return false;
    (*vec)->resize(static_cast<size_t>(length));

    *outBuffer = (*vec)->data();
    return true;
}

/**
 * This allocates a vector to size 'length' and returns whether the allocation is successful.
 *
 * See also AParcel_stdVectorAllocator. Types used with this allocator have their sizes defined
 * externally with respect to the NDK, and that size information is not passed into the NDK.
 * Instead, it is used in cases where callbacks are used. Note that when this allocator is used,
 * null arrays are not supported.
 *
 * See AParcel_readVector(const AParcel* parcel, std::vector<bool>)
 * See AParcel_readVector(const AParcel* parcel, std::vector<std::string>)
 */
template <typename T>
static inline bool AParcel_stdVectorExternalAllocator(void* vectorData, int32_t length) {
    if (length < 0) return false;

    std::vector<T>* vec = static_cast<std::vector<T>*>(vectorData);
    if (static_cast<size_t>(length) > vec->max_size()) return false;

    vec->resize(static_cast<size_t>(length));
    return true;
}

/**
 * This allocates a vector to size 'length' and returns whether the allocation is successful.
 *
 * See also AParcel_stdVectorAllocator. Types used with this allocator have their sizes defined
 * externally with respect to the NDK, and that size information is not passed into the NDK.
 * Instead, it is used in cases where callbacks are used. Note, when this allocator is used,
 * the vector itself can be nullable.
 *
 * See AParcel_readVector(const AParcel* parcel,
 * std::optional<std::vector<std::optional<std::string>>>)
 */
template <typename T>
static inline bool AParcel_nullableStdVectorExternalAllocator(void* vectorData, int32_t length) {
    std::optional<std::vector<T>>* vec = static_cast<std::optional<std::vector<T>>*>(vectorData);

    if (length < 0) {
        *vec = std::nullopt;
        return true;
    }

    *vec = std::optional<std::vector<T>>(std::vector<T>{});

    if (static_cast<size_t>(length) > (*vec)->max_size()) return false;
    (*vec)->resize(static_cast<size_t>(length));

    return true;
}

/**
 * This retrieves the underlying value in a vector which may not be contiguous at index from a
 * corresponding vectorData.
 */
template <typename T>
static inline T AParcel_stdVectorGetter(const void* vectorData, size_t index) {
    const std::vector<T>* vec = static_cast<const std::vector<T>*>(vectorData);
    return (*vec)[index];
}

/**
 * This sets the underlying value in a corresponding vectorData which may not be contiguous at
 * index.
 */
template <typename T>
static inline void AParcel_stdVectorSetter(void* vectorData, size_t index, T value) {
    std::vector<T>* vec = static_cast<std::vector<T>*>(vectorData);
    (*vec)[index] = value;
}

/**
 * This sets the underlying value in a corresponding vectorData which may not be contiguous at
 * index.
 */
template <typename T>
static inline void AParcel_nullableStdVectorSetter(void* vectorData, size_t index, T value) {
    std::optional<std::vector<T>>* vec = static_cast<std::optional<std::vector<T>>*>(vectorData);
    vec->value()[index] = value;
}

/**
 * Convenience method to write a nullable strong binder.
 */
static inline binder_status_t AParcel_writeNullableStrongBinder(AParcel* parcel,
                                                                const SpAIBinder& binder) {
    return AParcel_writeStrongBinder(parcel, binder.get());
}

/**
 * Convenience method to read a nullable strong binder.
 */
static inline binder_status_t AParcel_readNullableStrongBinder(const AParcel* parcel,
                                                               SpAIBinder* binder) {
    AIBinder* readBinder;
    binder_status_t status = AParcel_readStrongBinder(parcel, &readBinder);
    if (status == STATUS_OK) {
        binder->set(readBinder);
    }
    return status;
}

/**
 * Convenience method to write a strong binder but return an error if it is null.
 */
static inline binder_status_t AParcel_writeRequiredStrongBinder(AParcel* parcel,
                                                                const SpAIBinder& binder) {
    if (binder.get() == nullptr) {
        syslog(LOG_ERR, "Passing null binder object as non-@nullable AIDL IBinder");
        return STATUS_UNEXPECTED_NULL;
    }
    return AParcel_writeStrongBinder(parcel, binder.get());
}

/**
 * Convenience method to read a strong binder but return an error if it is null.
 */
static inline binder_status_t AParcel_readRequiredStrongBinder(const AParcel* parcel,
                                                               SpAIBinder* binder) {
    AIBinder* readBinder;
    binder_status_t ret = AParcel_readStrongBinder(parcel, &readBinder);
    if (ret == STATUS_OK) {
        if (readBinder == nullptr) {
            return STATUS_UNEXPECTED_NULL;
        }

        binder->set(readBinder);
    }
    return ret;
}

/**
 * Convenience method to write a ParcelFileDescriptor where -1 represents a null value.
 */
static inline binder_status_t AParcel_writeNullableParcelFileDescriptor(
        AParcel* parcel, const ScopedFileDescriptor& fd) {
    return AParcel_writeParcelFileDescriptor(parcel, fd.get());
}

/**
 * Convenience method to read a ParcelFileDescriptor where -1 represents a null value.
 */
static inline binder_status_t AParcel_readNullableParcelFileDescriptor(const AParcel* parcel,
                                                                       ScopedFileDescriptor* fd) {
    int readFd;
    binder_status_t status = AParcel_readParcelFileDescriptor(parcel, &readFd);
    if (status == STATUS_OK) {
        fd->set(readFd);
    }
    return status;
}

/**
 * Convenience method to write a valid ParcelFileDescriptor.
 */
static inline binder_status_t AParcel_writeRequiredParcelFileDescriptor(
        AParcel* parcel, const ScopedFileDescriptor& fd) {
    if (fd.get() < 0) {
        syslog(LOG_ERR, "Passing -1 file descriptor as non-@nullable AIDL ParcelFileDescriptor");
        return STATUS_UNEXPECTED_NULL;
    }
    return AParcel_writeParcelFileDescriptor(parcel, fd.get());
}

/**
 * Convenience method to read a valid ParcelFileDescriptor.
 */
static inline binder_status_t AParcel_readRequiredParcelFileDescriptor(const AParcel* parcel,
                                                                       ScopedFileDescriptor* fd) {
    int readFd;
    binder_status_t status = AParcel_readParcelFileDescriptor(parcel, &readFd);
    if (status == STATUS_OK) {
        if (readFd < 0) {
            return STATUS_UNEXPECTED_NULL;
        }
        fd->set(readFd);
    }
    return status;
}

/**
 * Allocates a std::string to length and returns the underlying buffer. For use with
 * AParcel_readString. See use below in AParcel_readString(const AParcel*, std::string*).
 */
static inline bool AParcel_stdStringAllocator(void* stringData, int32_t length, char** buffer) {
    if (length <= 0) return false;

    std::string* str = static_cast<std::string*>(stringData);
    str->resize(static_cast<size_t>(length) - 1);
    *buffer = &(*str)[0];
    return true;
}

/**
 * Allocates a string in a std::optional<std::string> to size 'length' (or to std::nullopt when
 * length is -1) and returns the underlying buffer. For use with AParcel_readString. See use below
 * in AParcel_readString(const AParcel*, std::optional<std::string>*).
 */
static inline bool AParcel_nullableStdStringAllocator(void* stringData, int32_t length,
                                                      char** buffer) {
    if (length == 0) return false;

    std::optional<std::string>* str = static_cast<std::optional<std::string>*>(stringData);

    if (length < 0) {
        *str = std::nullopt;
        return true;
    }

    *str = std::optional<std::string>(std::string{});
    (*str)->resize(static_cast<size_t>(length) - 1);
    *buffer = &(**str)[0];
    return true;
}

/**
 * Allocates a std::string inside of a std::vector<std::string> at index 'index' to size 'length'.
 */
static inline bool AParcel_stdVectorStringElementAllocator(void* vectorData, size_t index,
                                                           int32_t length, char** buffer) {
    std::vector<std::string>* vec = static_cast<std::vector<std::string>*>(vectorData);
    std::string& element = vec->at(index);
    return AParcel_stdStringAllocator(static_cast<void*>(&element), length, buffer);
}

/**
 * This gets the length and buffer of a std::string inside of a std::vector<std::string> at index
 * index.
 */
static inline const char* AParcel_stdVectorStringElementGetter(const void* vectorData, size_t index,
                                                               int32_t* outLength) {
    const std::vector<std::string>* vec = static_cast<const std::vector<std::string>*>(vectorData);
    const std::string& element = vec->at(index);

    *outLength = static_cast<int32_t>(element.size());
    return element.c_str();
}

/**
 * Allocates a string in a std::optional<std::string> inside of a
 * std::optional<std::vector<std::optional<std::string>>> at index 'index' to size 'length' (or to
 * std::nullopt when length is -1).
 */
static inline bool AParcel_nullableStdVectorStringElementAllocator(void* vectorData, size_t index,
                                                                   int32_t length, char** buffer) {
    std::optional<std::vector<std::optional<std::string>>>* vec =
            static_cast<std::optional<std::vector<std::optional<std::string>>>*>(vectorData);
    std::optional<std::string>& element = vec->value().at(index);
    return AParcel_nullableStdStringAllocator(static_cast<void*>(&element), length, buffer);
}

/**
 * This gets the length and buffer of a std::optional<std::string> inside of a
 * std::vector<std::string> at index index. If the string is null, then it returns null and a length
 * of -1.
 */
static inline const char* AParcel_nullableStdVectorStringElementGetter(const void* vectorData,
                                                                       size_t index,
                                                                       int32_t* outLength) {
    const std::optional<std::vector<std::optional<std::string>>>* vec =
            static_cast<const std::optional<std::vector<std::optional<std::string>>>*>(vectorData);
    const std::optional<std::string>& element = vec->value().at(index);

    if (!element) {
        *outLength = -1;
        return nullptr;
    }

    *outLength = static_cast<int32_t>(element->size());
    return element->c_str();
}

/**
 * This retrieves the underlying value in a std::array which may not be contiguous at index from a
 * corresponding arrData.
 */
template <typename T, size_t N>
static inline T AParcel_stdArrayGetter(const void* arrData, size_t index) {
    const std::array<T, N>* arr = static_cast<const std::array<T, N>*>(arrData);
    return (*arr)[index];
}

/**
 * This sets the underlying value in a corresponding arrData which may not be contiguous at
 * index.
 */
template <typename T, size_t N>
static inline void AParcel_stdArraySetter(void* arrData, size_t index, T value) {
    std::array<T, N>* arr = static_cast<std::array<T, N>*>(arrData);
    (*arr)[index] = value;
}

/**
 * This retrieves the underlying value in a std::array which may not be contiguous at index from a
 * corresponding arrData.
 */
template <typename T, size_t N>
static inline T AParcel_nullableStdArrayGetter(const void* arrData, size_t index) {
    const std::optional<std::array<T, N>>* arr =
            static_cast<const std::optional<std::array<T, N>>*>(arrData);
    return (*arr)[index];
}

/**
 * This sets the underlying value in a corresponding arrData which may not be contiguous at
 * index.
 */
template <typename T, size_t N>
static inline void AParcel_nullableStdArraySetter(void* arrData, size_t index, T value) {
    std::optional<std::array<T, N>>* arr = static_cast<std::optional<std::array<T, N>>*>(arrData);
    (*arr)->at(index) = value;
}

/**
 * Allocates a std::string inside of std::array<std::string, N> at index 'index' to size 'length'.
 */
template <size_t N>
static inline bool AParcel_stdArrayStringElementAllocator(void* arrData, size_t index,
                                                          int32_t length, char** buffer) {
    std::array<std::string, N>* arr = static_cast<std::array<std::string, N>*>(arrData);
    std::string& element = arr->at(index);
    return AParcel_stdStringAllocator(static_cast<void*>(&element), length, buffer);
}

/**
 * This gets the length and buffer of a std::string inside of a std::array<std::string, N> at index
 * 'index'.
 */
template <size_t N>
static const char* AParcel_stdArrayStringElementGetter(const void* arrData, size_t index,
                                                       int32_t* outLength) {
    const std::array<std::string, N>* arr = static_cast<const std::array<std::string, N>*>(arrData);
    const std::string& element = arr->at(index);

    *outLength = static_cast<int32_t>(element.size());
    return element.c_str();
}

/**
 * Allocates a std::string inside of std::array<std::optional<std::string>, N> at index 'index' to
 * size 'length'.
 */
template <size_t N>
static inline bool AParcel_stdArrayNullableStringElementAllocator(void* arrData, size_t index,
                                                                  int32_t length, char** buffer) {
    std::array<std::optional<std::string>, N>* arr =
            static_cast<std::array<std::optional<std::string>, N>*>(arrData);
    std::optional<std::string>& element = arr->at(index);
    return AParcel_nullableStdStringAllocator(static_cast<void*>(&element), length, buffer);
}

/**
 * This gets the length and buffer of a std::string inside of a
 * std::array<std::optional<std::string>, N> at index 'index'.
 */
template <size_t N>
static const char* AParcel_stdArrayNullableStringElementGetter(const void* arrData, size_t index,
                                                               int32_t* outLength) {
    const std::array<std::optional<std::string>, N>* arr =
            static_cast<const std::array<std::optional<std::string>, N>*>(arrData);
    const std::optional<std::string>& element = arr->at(index);

    if (!element) {
        *outLength = -1;
        return nullptr;
    }

    *outLength = static_cast<int32_t>(element->size());
    return element->c_str();
}

/**
 * Allocates a std::string inside of std::optional<std::array<std::optional<std::string>, N>> at
 * index 'index' to size 'length'.
 */
template <size_t N>
static inline bool AParcel_nullableStdArrayStringElementAllocator(void* arrData, size_t index,
                                                                  int32_t length, char** buffer) {
    std::optional<std::array<std::optional<std::string>, N>>* arr =
            static_cast<std::optional<std::array<std::optional<std::string>, N>>*>(arrData);
    std::optional<std::string>& element = (*arr)->at(index);
    return AParcel_nullableStdStringAllocator(static_cast<void*>(&element), length, buffer);
}

/**
 * Convenience API for writing a std::string.
 */
static inline binder_status_t AParcel_writeString(AParcel* parcel, const std::string& str) {
    return AParcel_writeString(parcel, str.c_str(), static_cast<int32_t>(str.size()));
}

/**
 * Convenience API for reading a std::string.
 */
static inline binder_status_t AParcel_readString(const AParcel* parcel, std::string* str) {
    void* stringData = static_cast<void*>(str);
    return AParcel_readString(parcel, stringData, AParcel_stdStringAllocator);
}

/**
 * Convenience API for writing a std::optional<std::string>.
 */
static inline binder_status_t AParcel_writeString(AParcel* parcel,
                                                  const std::optional<std::string>& str) {
    if (!str) {
        return AParcel_writeString(parcel, nullptr, -1);
    }

    return AParcel_writeString(parcel, str->c_str(), static_cast<int32_t>(str->size()));
}

/**
 * Convenience API for reading a std::optional<std::string>.
 */
static inline binder_status_t AParcel_readString(const AParcel* parcel,
                                                 std::optional<std::string>* str) {
    void* stringData = static_cast<void*>(str);
    return AParcel_readString(parcel, stringData, AParcel_nullableStdStringAllocator);
}

/**
 * Convenience API for writing a std::vector<std::string>
 */
static inline binder_status_t AParcel_writeVector(AParcel* parcel,
                                                  const std::vector<std::string>& vec) {
    const void* vectorData = static_cast<const void*>(&vec);
    return AParcel_writeStringArray(parcel, vectorData, static_cast<int32_t>(vec.size()),
                                    AParcel_stdVectorStringElementGetter);
}

/**
 * Convenience API for reading a std::vector<std::string>
 */
static inline binder_status_t AParcel_readVector(const AParcel* parcel,
                                                 std::vector<std::string>* vec) {
    void* vectorData = static_cast<void*>(vec);
    return AParcel_readStringArray(parcel, vectorData,
                                   AParcel_stdVectorExternalAllocator<std::string>,
                                   AParcel_stdVectorStringElementAllocator);
}

/**
 * Convenience API for writing a std::optional<std::vector<std::optional<std::string>>>
 */
static inline binder_status_t AParcel_writeVector(
        AParcel* parcel, const std::optional<std::vector<std::optional<std::string>>>& vec) {
    const void* vectorData = static_cast<const void*>(&vec);
    return AParcel_writeStringArray(parcel, vectorData,
                                    (vec ? static_cast<int32_t>(vec->size()) : -1),
                                    AParcel_nullableStdVectorStringElementGetter);
}

/**
 * Convenience API for reading a std::optional<std::vector<std::optional<std::string>>>
 */
static inline binder_status_t AParcel_readVector(
        const AParcel* parcel, std::optional<std::vector<std::optional<std::string>>>* vec) {
    void* vectorData = static_cast<void*>(vec);
    return AParcel_readStringArray(
            parcel, vectorData,
            AParcel_nullableStdVectorExternalAllocator<std::optional<std::string>>,
            AParcel_nullableStdVectorStringElementAllocator);
}

/**
 * Convenience API for writing a non-null parcelable.
 */
template <typename P>
static inline binder_status_t AParcel_writeParcelable(AParcel* parcel, const P& p) {
    if constexpr (is_interface_v<P>) {
        // Legacy behavior: allow null
        return first_template_type_t<P>::writeToParcel(parcel, p);
    } else {
        static_assert(is_parcelable_v<P>);
        binder_status_t status = AParcel_writeInt32(parcel, 1);  // non-null
        if (status != STATUS_OK) {
            return status;
        }
        return p.writeToParcel(parcel);
    }
}

/**
 * Convenience API for reading a non-null parcelable.
 */
template <typename P>
static inline binder_status_t AParcel_readParcelable(const AParcel* parcel, P* p) {
    if constexpr (is_interface_v<P>) {
        // Legacy behavior: allow null
        return first_template_type_t<P>::readFromParcel(parcel, p);
    } else {
        static_assert(is_parcelable_v<P>);
        int32_t null;
        binder_status_t status = AParcel_readInt32(parcel, &null);
        if (status != STATUS_OK) {
            return status;
        }
        if (null == 0) {
            return STATUS_UNEXPECTED_NULL;
        }
        return p->readFromParcel(parcel);
    }
}

/**
 * Convenience API for writing a nullable parcelable.
 */
template <typename P>
static inline binder_status_t AParcel_writeNullableParcelable(AParcel* parcel, const P& p) {
    if constexpr (is_interface_v<P>) {
        return first_template_type_t<P>::writeToParcel(parcel, p);
    } else {
        static_assert(is_nullable_parcelable_v<P>);
        if (!p) {
            return AParcel_writeInt32(parcel, 0);  // null
        }
        binder_status_t status = AParcel_writeInt32(parcel, 1);  // non-null
        if (status != STATUS_OK) {
            return status;
        }
        return p->writeToParcel(parcel);
    }
}

/**
 * Convenience API for reading a nullable parcelable.
 */
template <typename P>
static inline binder_status_t AParcel_readNullableParcelable(const AParcel* parcel, P* p) {
    if constexpr (is_interface_v<P>) {
        return first_template_type_t<P>::readFromParcel(parcel, p);
    } else if constexpr (is_specialization_v<P, std::optional>) {
        int32_t null;
        binder_status_t status = AParcel_readInt32(parcel, &null);
        if (status != STATUS_OK) {
            return status;
        }
        if (null == 0) {
            *p = std::nullopt;
            return STATUS_OK;
        }
        p->emplace(first_template_type_t<P>());
        return (*p)->readFromParcel(parcel);
    } else {
        static_assert(is_specialization_v<P, std::unique_ptr>);
        int32_t null;
        binder_status_t status = AParcel_readInt32(parcel, &null);
        if (status != STATUS_OK) {
            return status;
        }
        if (null == 0) {
            p->reset();
            return STATUS_OK;
        }
        *p = std::make_unique<first_template_type_t<P>>();
        return (*p)->readFromParcel(parcel);
    }
}

// Forward decls
template <typename T>
static inline binder_status_t AParcel_writeData(AParcel* parcel, const T& value);
template <typename T>
static inline binder_status_t AParcel_writeNullableData(AParcel* parcel, const T& value);
template <typename T>
static inline binder_status_t AParcel_readData(const AParcel* parcel, T* value);
template <typename T>
static inline binder_status_t AParcel_readNullableData(const AParcel* parcel, T* value);

/**
 * Reads an object of type T inside a std::array<T, N> at index 'index' from 'parcel'.
 */
template <typename T, size_t N>
binder_status_t AParcel_readStdArrayData(const AParcel* parcel, void* arrayData, size_t index) {
    std::array<T, N>* arr = static_cast<std::array<T, N>*>(arrayData);
    return AParcel_readData(parcel, &arr->at(index));
}

/**
 * Reads a nullable object of type T inside a std::array<T, N> at index 'index' from 'parcel'.
 */
template <typename T, size_t N>
binder_status_t AParcel_readStdArrayNullableData(const AParcel* parcel, void* arrayData,
                                                 size_t index) {
    std::array<T, N>* arr = static_cast<std::array<T, N>*>(arrayData);
    return AParcel_readNullableData(parcel, &arr->at(index));
}

/**
 * Reads a nullable object of type T inside a std::array<T, N> at index 'index' from 'parcel'.
 */
template <typename T, size_t N>
binder_status_t AParcel_readNullableStdArrayNullableData(const AParcel* parcel, void* arrayData,
                                                         size_t index) {
    std::optional<std::array<T, N>>* arr = static_cast<std::optional<std::array<T, N>>*>(arrayData);
    return AParcel_readNullableData(parcel, &(*arr)->at(index));
}

/**
 * Writes an object of type T inside a std::array<T, N> at index 'index' to 'parcel'.
 */
template <typename T, size_t N>
binder_status_t AParcel_writeStdArrayData(AParcel* parcel, const void* arrayData, size_t index) {
    const std::array<T, N>* arr = static_cast<const std::array<T, N>*>(arrayData);
    return AParcel_writeData(parcel, arr->at(index));
}

/**
 * Writes a nullable object of type T inside a std::array<T, N> at index 'index' to 'parcel'.
 */
template <typename T, size_t N>
binder_status_t AParcel_writeStdArrayNullableData(AParcel* parcel, const void* arrayData,
                                                  size_t index) {
    const std::array<T, N>* arr = static_cast<const std::array<T, N>*>(arrayData);
    return AParcel_writeNullableData(parcel, arr->at(index));
}

/**
 * Writes a parcelable object of type P inside a std::vector<P> at index 'index' to 'parcel'.
 */
template <typename P>
binder_status_t AParcel_writeStdVectorParcelableElement(AParcel* parcel, const void* vectorData,
                                                        size_t index) {
    const std::vector<P>* vector = static_cast<const std::vector<P>*>(vectorData);
    return AParcel_writeParcelable(parcel, vector->at(index));
}

/**
 * Reads a parcelable object of type P inside a std::vector<P> at index 'index' from 'parcel'.
 */
template <typename P>
binder_status_t AParcel_readStdVectorParcelableElement(const AParcel* parcel, void* vectorData,
                                                       size_t index) {
    std::vector<P>* vector = static_cast<std::vector<P>*>(vectorData);
    return AParcel_readParcelable(parcel, &vector->at(index));
}

/**
 * Writes a parcelable object of type P inside a std::vector<P> at index 'index' to 'parcel'.
 */
template <typename P>
binder_status_t AParcel_writeNullableStdVectorParcelableElement(AParcel* parcel,
                                                                const void* vectorData,
                                                                size_t index) {
    const std::optional<std::vector<P>>* vector =
            static_cast<const std::optional<std::vector<P>>*>(vectorData);
    return AParcel_writeNullableParcelable(parcel, (*vector)->at(index));
}

/**
 * Reads a parcelable object of type P inside a std::vector<P> at index 'index' from 'parcel'.
 */
template <typename P>
binder_status_t AParcel_readNullableStdVectorParcelableElement(const AParcel* parcel,
                                                               void* vectorData, size_t index) {
    std::optional<std::vector<P>>* vector = static_cast<std::optional<std::vector<P>>*>(vectorData);
    return AParcel_readNullableParcelable(parcel, &(*vector)->at(index));
}

/**
 * Writes a ScopedFileDescriptor object inside a std::vector<ScopedFileDescriptor> at index 'index'
 * to 'parcel'.
 */
template <>
inline binder_status_t AParcel_writeStdVectorParcelableElement<ScopedFileDescriptor>(
        AParcel* parcel, const void* vectorData, size_t index) {
    const std::vector<ScopedFileDescriptor>* vector =
            static_cast<const std::vector<ScopedFileDescriptor>*>(vectorData);
    return AParcel_writeRequiredParcelFileDescriptor(parcel, vector->at(index));
}

/**
 * Reads a ScopedFileDescriptor object inside a std::vector<ScopedFileDescriptor> at index 'index'
 * from 'parcel'.
 */
template <>
inline binder_status_t AParcel_readStdVectorParcelableElement<ScopedFileDescriptor>(
        const AParcel* parcel, void* vectorData, size_t index) {
    std::vector<ScopedFileDescriptor>* vector =
            static_cast<std::vector<ScopedFileDescriptor>*>(vectorData);
    return AParcel_readRequiredParcelFileDescriptor(parcel, &vector->at(index));
}

/**
 * Writes a ScopedFileDescriptor object inside a std::optional<std::vector<ScopedFileDescriptor>> at
 * index 'index' to 'parcel'.
 */
template <>
inline binder_status_t AParcel_writeNullableStdVectorParcelableElement<ScopedFileDescriptor>(
        AParcel* parcel, const void* vectorData, size_t index) {
    const std::optional<std::vector<ScopedFileDescriptor>>* vector =
            static_cast<const std::optional<std::vector<ScopedFileDescriptor>>*>(vectorData);
    return AParcel_writeNullableParcelFileDescriptor(parcel, (*vector)->at(index));
}

/**
 * Reads a ScopedFileDescriptor object inside a std::optional<std::vector<ScopedFileDescriptor>> at
 * index 'index' from 'parcel'.
 */
template <>
inline binder_status_t AParcel_readNullableStdVectorParcelableElement<ScopedFileDescriptor>(
        const AParcel* parcel, void* vectorData, size_t index) {
    std::optional<std::vector<ScopedFileDescriptor>>* vector =
            static_cast<std::optional<std::vector<ScopedFileDescriptor>>*>(vectorData);
    return AParcel_readNullableParcelFileDescriptor(parcel, &(*vector)->at(index));
}

/**
 * Writes an SpAIBinder object inside a std::vector<SpAIBinder> at index 'index'
 * to 'parcel'.
 */
template <>
inline binder_status_t AParcel_writeStdVectorParcelableElement<SpAIBinder>(AParcel* parcel,
                                                                           const void* vectorData,
                                                                           size_t index) {
    const std::vector<SpAIBinder>* vector = static_cast<const std::vector<SpAIBinder>*>(vectorData);
    return AParcel_writeRequiredStrongBinder(parcel, vector->at(index));
}

/**
 * Reads an SpAIBinder object inside a std::vector<SpAIBinder> at index 'index'
 * from 'parcel'.
 */
template <>
inline binder_status_t AParcel_readStdVectorParcelableElement<SpAIBinder>(const AParcel* parcel,
                                                                          void* vectorData,
                                                                          size_t index) {
    std::vector<SpAIBinder>* vector = static_cast<std::vector<SpAIBinder>*>(vectorData);
    return AParcel_readRequiredStrongBinder(parcel, &vector->at(index));
}

/**
 * Writes an SpAIBinder object inside a std::optional<std::vector<SpAIBinder>> at index 'index'
 * to 'parcel'.
 */
template <>
inline binder_status_t AParcel_writeNullableStdVectorParcelableElement<SpAIBinder>(
        AParcel* parcel, const void* vectorData, size_t index) {
    const std::optional<std::vector<SpAIBinder>>* vector =
            static_cast<const std::optional<std::vector<SpAIBinder>>*>(vectorData);
    return AParcel_writeNullableStrongBinder(parcel, (*vector)->at(index));
}

/**
 * Reads an SpAIBinder object inside a std::optional<std::vector<SpAIBinder>> at index 'index'
 * from 'parcel'.
 */
template <>
inline binder_status_t AParcel_readNullableStdVectorParcelableElement<SpAIBinder>(
        const AParcel* parcel, void* vectorData, size_t index) {
    std::optional<std::vector<SpAIBinder>>* vector =
            static_cast<std::optional<std::vector<SpAIBinder>>*>(vectorData);
    return AParcel_readNullableStrongBinder(parcel, &(*vector)->at(index));
}

/**
 * Convenience API for writing a std::vector<P>
 */
template <typename P>
static inline binder_status_t AParcel_writeVector(AParcel* parcel, const std::vector<P>& vec) {
    if constexpr (std::is_enum_v<P>) {
        if constexpr (std::is_same_v<std::underlying_type_t<P>, int8_t>) {
            return AParcel_writeByteArray(parcel, reinterpret_cast<const int8_t*>(vec.data()),
                                          static_cast<int32_t>(vec.size()));
        } else if constexpr (std::is_same_v<std::underlying_type_t<P>, int32_t>) {
            return AParcel_writeInt32Array(parcel, reinterpret_cast<const int32_t*>(vec.data()),
                                           static_cast<int32_t>(vec.size()));
        } else if constexpr (std::is_same_v<std::underlying_type_t<P>, int64_t>) {
            return AParcel_writeInt64Array(parcel, reinterpret_cast<const int64_t*>(vec.data()),
                                           static_cast<int32_t>(vec.size()));
        } else {
            static_assert(dependent_false_v<P>, "unrecognized type");
        }
    } else {
        static_assert(!std::is_same_v<P, std::string>, "specialization should be used");
        const void* vectorData = static_cast<const void*>(&vec);
        return AParcel_writeParcelableArray(parcel, vectorData, static_cast<int32_t>(vec.size()),
                                            AParcel_writeStdVectorParcelableElement<P>);
    }
}

/**
 * Convenience API for reading a std::vector<P>
 */
template <typename P>
static inline binder_status_t AParcel_readVector(const AParcel* parcel, std::vector<P>* vec) {
    if constexpr (std::is_enum_v<P>) {
        void* vectorData = static_cast<void*>(vec);
        if constexpr (std::is_same_v<std::underlying_type_t<P>, int8_t>) {
            return AParcel_readByteArray(parcel, vectorData, AParcel_stdVectorAllocator<int8_t>);
        } else if constexpr (std::is_same_v<std::underlying_type_t<P>, int32_t>) {
            return AParcel_readInt32Array(parcel, vectorData, AParcel_stdVectorAllocator<int32_t>);
        } else if constexpr (std::is_same_v<std::underlying_type_t<P>, int64_t>) {
            return AParcel_readInt64Array(parcel, vectorData, AParcel_stdVectorAllocator<int64_t>);
        } else {
            static_assert(dependent_false_v<P>, "unrecognized type");
        }
    } else {
        static_assert(!std::is_same_v<P, std::string>, "specialization should be used");
        void* vectorData = static_cast<void*>(vec);
        return AParcel_readParcelableArray(parcel, vectorData,
                                           AParcel_stdVectorExternalAllocator<P>,
                                           AParcel_readStdVectorParcelableElement<P>);
    }
}

/**
 * Convenience API for writing a std::optional<std::vector<P>>
 */
template <typename P>
static inline binder_status_t AParcel_writeVector(AParcel* parcel,
                                                  const std::optional<std::vector<P>>& vec) {
    if constexpr (std::is_enum_v<P>) {
        if constexpr (std::is_same_v<std::underlying_type_t<P>, int8_t>) {
            return AParcel_writeByteArray(
                    parcel, vec ? reinterpret_cast<const int8_t*>(vec->data()) : nullptr,
                    vec ? static_cast<int32_t>(vec->size()) : -1);
        } else if constexpr (std::is_same_v<std::underlying_type_t<P>, int32_t>) {
            return AParcel_writeInt32Array(
                    parcel, vec ? reinterpret_cast<const int32_t*>(vec->data()) : nullptr,
                    vec ? static_cast<int32_t>(vec->size()) : -1);
        } else if constexpr (std::is_same_v<std::underlying_type_t<P>, int64_t>) {
            return AParcel_writeInt64Array(
                    parcel, vec ? reinterpret_cast<const int64_t*>(vec->data()) : nullptr,
                    vec ? static_cast<int32_t>(vec->size()) : -1);
        } else {
            static_assert(dependent_false_v<P>, "unrecognized type");
        }
    } else {
        static_assert(!std::is_same_v<P, std::optional<std::string>>,
                      "specialization should be used");
        if (!vec) return AParcel_writeInt32(parcel, -1);
        const void* vectorData = static_cast<const void*>(&vec);
        return AParcel_writeParcelableArray(parcel, vectorData, static_cast<int32_t>(vec->size()),
                                            AParcel_writeNullableStdVectorParcelableElement<P>);
    }
}

/**
 * Convenience API for reading a std::optional<std::vector<P>>
 */
template <typename P>
static inline binder_status_t AParcel_readVector(const AParcel* parcel,
                                                 std::optional<std::vector<P>>* vec) {
    if constexpr (std::is_enum_v<P>) {
        void* vectorData = static_cast<void*>(vec);
        if constexpr (std::is_same_v<std::underlying_type_t<P>, int8_t>) {
            return AParcel_readByteArray(parcel, vectorData,
                                         AParcel_nullableStdVectorAllocator<int8_t>);
        } else if constexpr (std::is_same_v<std::underlying_type_t<P>, int32_t>) {
            return AParcel_readInt32Array(parcel, vectorData,
                                          AParcel_nullableStdVectorAllocator<int32_t>);
        } else if constexpr (std::is_same_v<std::underlying_type_t<P>, int64_t>) {
            return AParcel_readInt64Array(parcel, vectorData,
                                          AParcel_nullableStdVectorAllocator<int64_t>);
        } else {
            static_assert(dependent_false_v<P>, "unrecognized type");
        }
    } else {
        static_assert(!std::is_same_v<P, std::optional<std::string>>,
                      "specialization should be used");
        void* vectorData = static_cast<void*>(vec);
        return AParcel_readParcelableArray(parcel, vectorData,
                                           AParcel_nullableStdVectorExternalAllocator<P>,
                                           AParcel_readNullableStdVectorParcelableElement<P>);
    }
}

// @START
/**
 * Writes a vector of int32_t to the next location in a non-null parcel.
 */
inline binder_status_t AParcel_writeVector(AParcel* parcel, const std::vector<int32_t>& vec) {
    return AParcel_writeInt32Array(parcel, vec.data(), static_cast<int32_t>(vec.size()));
}

/**
 * Writes an optional vector of int32_t to the next location in a non-null parcel.
 */
inline binder_status_t AParcel_writeVector(AParcel* parcel,
                                           const std::optional<std::vector<int32_t>>& vec) {
    if (!vec) return AParcel_writeInt32Array(parcel, nullptr, -1);
    return AParcel_writeVector(parcel, *vec);
}

/**
 * Reads a vector of int32_t from the next location in a non-null parcel.
 */
inline binder_status_t AParcel_readVector(const AParcel* parcel, std::vector<int32_t>* vec) {
    void* vectorData = static_cast<void*>(vec);
    return AParcel_readInt32Array(parcel, vectorData, AParcel_stdVectorAllocator<int32_t>);
}

/**
 * Reads an optional vector of int32_t from the next location in a non-null parcel.
 */
inline binder_status_t AParcel_readVector(const AParcel* parcel,
                                          std::optional<std::vector<int32_t>>* vec) {
    void* vectorData = static_cast<void*>(vec);
    return AParcel_readInt32Array(parcel, vectorData, AParcel_nullableStdVectorAllocator<int32_t>);
}

/**
 * Writes a vector of uint32_t to the next location in a non-null parcel.
 */
inline binder_status_t AParcel_writeVector(AParcel* parcel, const std::vector<uint32_t>& vec) {
    return AParcel_writeUint32Array(parcel, vec.data(), static_cast<int32_t>(vec.size()));
}

/**
 * Writes an optional vector of uint32_t to the next location in a non-null parcel.
 */
inline binder_status_t AParcel_writeVector(AParcel* parcel,
                                           const std::optional<std::vector<uint32_t>>& vec) {
    if (!vec) return AParcel_writeUint32Array(parcel, nullptr, -1);
    return AParcel_writeVector(parcel, *vec);
}

/**
 * Reads a vector of uint32_t from the next location in a non-null parcel.
 */
inline binder_status_t AParcel_readVector(const AParcel* parcel, std::vector<uint32_t>* vec) {
    void* vectorData = static_cast<void*>(vec);
    return AParcel_readUint32Array(parcel, vectorData, AParcel_stdVectorAllocator<uint32_t>);
}

/**
 * Reads an optional vector of uint32_t from the next location in a non-null parcel.
 */
inline binder_status_t AParcel_readVector(const AParcel* parcel,
                                          std::optional<std::vector<uint32_t>>* vec) {
    void* vectorData = static_cast<void*>(vec);
    return AParcel_readUint32Array(parcel, vectorData,
                                   AParcel_nullableStdVectorAllocator<uint32_t>);
}

/**
 * Writes a vector of int64_t to the next location in a non-null parcel.
 */
inline binder_status_t AParcel_writeVector(AParcel* parcel, const std::vector<int64_t>& vec) {
    return AParcel_writeInt64Array(parcel, vec.data(), static_cast<int32_t>(vec.size()));
}

/**
 * Writes an optional vector of int64_t to the next location in a non-null parcel.
 */
inline binder_status_t AParcel_writeVector(AParcel* parcel,
                                           const std::optional<std::vector<int64_t>>& vec) {
    if (!vec) return AParcel_writeInt64Array(parcel, nullptr, -1);
    return AParcel_writeVector(parcel, *vec);
}

/**
 * Reads a vector of int64_t from the next location in a non-null parcel.
 */
inline binder_status_t AParcel_readVector(const AParcel* parcel, std::vector<int64_t>* vec) {
    void* vectorData = static_cast<void*>(vec);
    return AParcel_readInt64Array(parcel, vectorData, AParcel_stdVectorAllocator<int64_t>);
}

/**
 * Reads an optional vector of int64_t from the next location in a non-null parcel.
 */
inline binder_status_t AParcel_readVector(const AParcel* parcel,
                                          std::optional<std::vector<int64_t>>* vec) {
    void* vectorData = static_cast<void*>(vec);
    return AParcel_readInt64Array(parcel, vectorData, AParcel_nullableStdVectorAllocator<int64_t>);
}

/**
 * Writes a vector of uint64_t to the next location in a non-null parcel.
 */
inline binder_status_t AParcel_writeVector(AParcel* parcel, const std::vector<uint64_t>& vec) {
    return AParcel_writeUint64Array(parcel, vec.data(), static_cast<int32_t>(vec.size()));
}

/**
 * Writes an optional vector of uint64_t to the next location in a non-null parcel.
 */
inline binder_status_t AParcel_writeVector(AParcel* parcel,
                                           const std::optional<std::vector<uint64_t>>& vec) {
    if (!vec) return AParcel_writeUint64Array(parcel, nullptr, -1);
    return AParcel_writeVector(parcel, *vec);
}

/**
 * Reads a vector of uint64_t from the next location in a non-null parcel.
 */
inline binder_status_t AParcel_readVector(const AParcel* parcel, std::vector<uint64_t>* vec) {
    void* vectorData = static_cast<void*>(vec);
    return AParcel_readUint64Array(parcel, vectorData, AParcel_stdVectorAllocator<uint64_t>);
}

/**
 * Reads an optional vector of uint64_t from the next location in a non-null parcel.
 */
inline binder_status_t AParcel_readVector(const AParcel* parcel,
                                          std::optional<std::vector<uint64_t>>* vec) {
    void* vectorData = static_cast<void*>(vec);
    return AParcel_readUint64Array(parcel, vectorData,
                                   AParcel_nullableStdVectorAllocator<uint64_t>);
}

/**
 * Writes a vector of float to the next location in a non-null parcel.
 */
inline binder_status_t AParcel_writeVector(AParcel* parcel, const std::vector<float>& vec) {
    return AParcel_writeFloatArray(parcel, vec.data(), static_cast<int32_t>(vec.size()));
}

/**
 * Writes an optional vector of float to the next location in a non-null parcel.
 */
inline binder_status_t AParcel_writeVector(AParcel* parcel,
                                           const std::optional<std::vector<float>>& vec) {
    if (!vec) return AParcel_writeFloatArray(parcel, nullptr, -1);
    return AParcel_writeVector(parcel, *vec);
}

/**
 * Reads a vector of float from the next location in a non-null parcel.
 */
inline binder_status_t AParcel_readVector(const AParcel* parcel, std::vector<float>* vec) {
    void* vectorData = static_cast<void*>(vec);
    return AParcel_readFloatArray(parcel, vectorData, AParcel_stdVectorAllocator<float>);
}

/**
 * Reads an optional vector of float from the next location in a non-null parcel.
 */
inline binder_status_t AParcel_readVector(const AParcel* parcel,
                                          std::optional<std::vector<float>>* vec) {
    void* vectorData = static_cast<void*>(vec);
    return AParcel_readFloatArray(parcel, vectorData, AParcel_nullableStdVectorAllocator<float>);
}

/**
 * Writes a vector of double to the next location in a non-null parcel.
 */
inline binder_status_t AParcel_writeVector(AParcel* parcel, const std::vector<double>& vec) {
    return AParcel_writeDoubleArray(parcel, vec.data(), static_cast<int32_t>(vec.size()));
}

/**
 * Writes an optional vector of double to the next location in a non-null parcel.
 */
inline binder_status_t AParcel_writeVector(AParcel* parcel,
                                           const std::optional<std::vector<double>>& vec) {
    if (!vec) return AParcel_writeDoubleArray(parcel, nullptr, -1);
    return AParcel_writeVector(parcel, *vec);
}

/**
 * Reads a vector of double from the next location in a non-null parcel.
 */
inline binder_status_t AParcel_readVector(const AParcel* parcel, std::vector<double>* vec) {
    void* vectorData = static_cast<void*>(vec);
    return AParcel_readDoubleArray(parcel, vectorData, AParcel_stdVectorAllocator<double>);
}

/**
 * Reads an optional vector of double from the next location in a non-null parcel.
 */
inline binder_status_t AParcel_readVector(const AParcel* parcel,
                                          std::optional<std::vector<double>>* vec) {
    void* vectorData = static_cast<void*>(vec);
    return AParcel_readDoubleArray(parcel, vectorData, AParcel_nullableStdVectorAllocator<double>);
}

/**
 * Writes a vector of bool to the next location in a non-null parcel.
 */
inline binder_status_t AParcel_writeVector(AParcel* parcel, const std::vector<bool>& vec) {
    return AParcel_writeBoolArray(parcel, static_cast<const void*>(&vec),
                                  static_cast<int32_t>(vec.size()), AParcel_stdVectorGetter<bool>);
}

/**
 * Writes an optional vector of bool to the next location in a non-null parcel.
 */
inline binder_status_t AParcel_writeVector(AParcel* parcel,
                                           const std::optional<std::vector<bool>>& vec) {
    if (!vec) return AParcel_writeBoolArray(parcel, nullptr, -1, AParcel_stdVectorGetter<bool>);
    return AParcel_writeVector(parcel, *vec);
}

/**
 * Reads a vector of bool from the next location in a non-null parcel.
 */
inline binder_status_t AParcel_readVector(const AParcel* parcel, std::vector<bool>* vec) {
    void* vectorData = static_cast<void*>(vec);
    return AParcel_readBoolArray(parcel, vectorData, AParcel_stdVectorExternalAllocator<bool>,
                                 AParcel_stdVectorSetter<bool>);
}

/**
 * Reads an optional vector of bool from the next location in a non-null parcel.
 */
inline binder_status_t AParcel_readVector(const AParcel* parcel,
                                          std::optional<std::vector<bool>>* vec) {
    void* vectorData = static_cast<void*>(vec);
    return AParcel_readBoolArray(parcel, vectorData,
                                 AParcel_nullableStdVectorExternalAllocator<bool>,
                                 AParcel_nullableStdVectorSetter<bool>);
}

/**
 * Writes a vector of char16_t to the next location in a non-null parcel.
 */
inline binder_status_t AParcel_writeVector(AParcel* parcel, const std::vector<char16_t>& vec) {
    return AParcel_writeCharArray(parcel, vec.data(), static_cast<int32_t>(vec.size()));
}

/**
 * Writes an optional vector of char16_t to the next location in a non-null parcel.
 */
inline binder_status_t AParcel_writeVector(AParcel* parcel,
                                           const std::optional<std::vector<char16_t>>& vec) {
    if (!vec) return AParcel_writeCharArray(parcel, nullptr, -1);
    return AParcel_writeVector(parcel, *vec);
}

/**
 * Reads a vector of char16_t from the next location in a non-null parcel.
 */
inline binder_status_t AParcel_readVector(const AParcel* parcel, std::vector<char16_t>* vec) {
    void* vectorData = static_cast<void*>(vec);
    return AParcel_readCharArray(parcel, vectorData, AParcel_stdVectorAllocator<char16_t>);
}

/**
 * Reads an optional vector of char16_t from the next location in a non-null parcel.
 */
inline binder_status_t AParcel_readVector(const AParcel* parcel,
                                          std::optional<std::vector<char16_t>>* vec) {
    void* vectorData = static_cast<void*>(vec);
    return AParcel_readCharArray(parcel, vectorData, AParcel_nullableStdVectorAllocator<char16_t>);
}

/**
 * Writes a vector of uint8_t to the next location in a non-null parcel.
 */
inline binder_status_t AParcel_writeVector(AParcel* parcel, const std::vector<uint8_t>& vec) {
    return AParcel_writeByteArray(parcel, reinterpret_cast<const int8_t*>(vec.data()),
                                  static_cast<int32_t>(vec.size()));
}

/**
 * Writes an optional vector of uint8_t to the next location in a non-null parcel.
 */
inline binder_status_t AParcel_writeVector(AParcel* parcel,
                                           const std::optional<std::vector<uint8_t>>& vec) {
    if (!vec) return AParcel_writeByteArray(parcel, nullptr, -1);
    return AParcel_writeVector(parcel, *vec);
}

/**
 * Reads a vector of uint8_t from the next location in a non-null parcel.
 */
inline binder_status_t AParcel_readVector(const AParcel* parcel, std::vector<uint8_t>* vec) {
    void* vectorData = static_cast<void*>(vec);
    return AParcel_readByteArray(parcel, vectorData, AParcel_stdVectorAllocator<int8_t>);
}

/**
 * Reads an optional vector of uint8_t from the next location in a non-null parcel.
 */
inline binder_status_t AParcel_readVector(const AParcel* parcel,
                                          std::optional<std::vector<uint8_t>>* vec) {
    void* vectorData = static_cast<void*>(vec);
    return AParcel_readByteArray(parcel, vectorData, AParcel_nullableStdVectorAllocator<int8_t>);
}

// @END

/**
 * Convenience API for writing the size of a vector.
 */
template <typename T>
static inline binder_status_t AParcel_writeVectorSize(AParcel* parcel, const std::vector<T>& vec) {
    if (vec.size() > INT32_MAX) {
        return STATUS_BAD_VALUE;
    }

    return AParcel_writeInt32(parcel, static_cast<int32_t>(vec.size()));
}

/**
 * Convenience API for writing the size of a vector.
 */
template <typename T>
static inline binder_status_t AParcel_writeVectorSize(AParcel* parcel,
                                                      const std::optional<std::vector<T>>& vec) {
    if (!vec) {
        return AParcel_writeInt32(parcel, -1);
    }

    if (vec->size() > INT32_MAX) {
        return STATUS_BAD_VALUE;
    }

    return AParcel_writeInt32(parcel, static_cast<int32_t>(vec->size()));
}

/**
 * Convenience API for resizing a vector.
 */
template <typename T>
static inline binder_status_t AParcel_resizeVector(const AParcel* parcel, std::vector<T>* vec) {
    int32_t size;
    binder_status_t err = AParcel_readInt32(parcel, &size);

    if (err != STATUS_OK) return err;
    if (size < 0) return STATUS_UNEXPECTED_NULL;

    // TODO(b/188215728): delegate to libbinder_ndk
    if (size > 1000000) {
        syslog(LOG_ERR, "Parcel vector size too large");
        return STATUS_NO_MEMORY;
    }

    vec->resize(static_cast<size_t>(size));
    return STATUS_OK;
}

/**
 * Convenience API for resizing a vector.
 */
template <typename T>
static inline binder_status_t AParcel_resizeVector(const AParcel* parcel,
                                                   std::optional<std::vector<T>>* vec) {
    int32_t size;
    binder_status_t err = AParcel_readInt32(parcel, &size);

    if (err != STATUS_OK) return err;
    if (size < -1) return STATUS_UNEXPECTED_NULL;

    if (size == -1) {
        *vec = std::nullopt;
        return STATUS_OK;
    }

    // TODO(b/188215728): delegate to libbinder_ndk
    if (size > 1000000) {
        syslog(LOG_ERR, "Parcel vector size too large");
        return STATUS_NO_MEMORY;
    }

    *vec = std::optional<std::vector<T>>(std::vector<T>{});
    (*vec)->resize(static_cast<size_t>(size));
    return STATUS_OK;
}

/**
 * Writes a fixed-size array of T.
 */
template <typename T, size_t N>
static inline binder_status_t AParcel_writeFixedArray(AParcel* parcel,
                                                      const std::array<T, N>& arr) {
    if constexpr (std::is_same_v<T, bool>) {
        const void* arrayData = static_cast<const void*>(&arr);
        return AParcel_writeBoolArray(parcel, arrayData, static_cast<int32_t>(N),
                                      &AParcel_stdArrayGetter<T, N>);
    } else if constexpr (std::is_same_v<T, uint8_t>) {
        return AParcel_writeByteArray(parcel, reinterpret_cast<const int8_t*>(arr.data()),
                                      static_cast<int32_t>(arr.size()));
    } else if constexpr (std::is_same_v<T, char16_t>) {
        return AParcel_writeCharArray(parcel, arr.data(), static_cast<int32_t>(arr.size()));
    } else if constexpr (std::is_same_v<T, int32_t>) {
        return AParcel_writeInt32Array(parcel, arr.data(), static_cast<int32_t>(arr.size()));
    } else if constexpr (std::is_same_v<T, int64_t>) {
        return AParcel_writeInt64Array(parcel, arr.data(), static_cast<int32_t>(arr.size()));
    } else if constexpr (std::is_same_v<T, float>) {
        return AParcel_writeFloatArray(parcel, arr.data(), static_cast<int32_t>(arr.size()));
    } else if constexpr (std::is_same_v<T, double>) {
        return AParcel_writeDoubleArray(parcel, arr.data(), static_cast<int32_t>(arr.size()));
    } else if constexpr (std::is_same_v<T, std::string>) {
        const void* arrayData = static_cast<const void*>(&arr);
        return AParcel_writeStringArray(parcel, arrayData, static_cast<int32_t>(N),
                                        &AParcel_stdArrayStringElementGetter<N>);
    } else {
        const void* arrayData = static_cast<const void*>(&arr);
        return AParcel_writeParcelableArray(parcel, arrayData, static_cast<int32_t>(N),
                                            &AParcel_writeStdArrayData<T, N>);
    }
}

/**
 * Writes a fixed-size array of T.
 */
template <typename T, size_t N>
static inline binder_status_t AParcel_writeFixedArrayWithNullableData(AParcel* parcel,
                                                                      const std::array<T, N>& arr) {
    if constexpr (std::is_same_v<T, bool> || std::is_same_v<T, uint8_t> ||
                  std::is_same_v<T, char16_t> || std::is_same_v<T, int32_t> ||
                  std::is_same_v<T, int64_t> || std::is_same_v<T, float> ||
                  std::is_same_v<T, double> || std::is_same_v<T, std::string>) {
        return AParcel_writeFixedArray(parcel, arr);
    } else if constexpr (std::is_same_v<T, std::optional<std::string>>) {
        const void* arrayData = static_cast<const void*>(&arr);
        return AParcel_writeStringArray(parcel, arrayData, static_cast<int32_t>(N),
                                        &AParcel_stdArrayNullableStringElementGetter<N>);
    } else {
        const void* arrayData = static_cast<const void*>(&arr);
        return AParcel_writeParcelableArray(parcel, arrayData, static_cast<int32_t>(N),
                                            &AParcel_writeStdArrayNullableData<T, N>);
    }
}

/**
 * Writes a fixed-size array of T.
 */
template <typename T, size_t N>
static inline binder_status_t AParcel_writeNullableFixedArrayWithNullableData(
        AParcel* parcel, const std::optional<std::array<T, N>>& arr) {
    if (!arr) return AParcel_writeInt32(parcel, -1);
    return AParcel_writeFixedArrayWithNullableData(parcel, arr.value());
}

/**
 * Reads a fixed-size array of T.
 */
template <typename T, size_t N>
static inline binder_status_t AParcel_readFixedArray(const AParcel* parcel, std::array<T, N>* arr) {
    void* arrayData = static_cast<void*>(arr);
    if constexpr (std::is_same_v<T, bool>) {
        return AParcel_readBoolArray(parcel, arrayData, &AParcel_stdArrayExternalAllocator<N>,
                                     &AParcel_stdArraySetter<T, N>);
    } else if constexpr (std::is_same_v<T, uint8_t>) {
        return AParcel_readByteArray(parcel, arrayData, &AParcel_stdArrayAllocator<int8_t, N>);
    } else if constexpr (std::is_same_v<T, char16_t>) {
        return AParcel_readCharArray(parcel, arrayData, &AParcel_stdArrayAllocator<T, N>);
    } else if constexpr (std::is_same_v<T, int32_t>) {
        return AParcel_readInt32Array(parcel, arrayData, &AParcel_stdArrayAllocator<T, N>);
    } else if constexpr (std::is_same_v<T, int64_t>) {
        return AParcel_readInt64Array(parcel, arrayData, &AParcel_stdArrayAllocator<T, N>);
    } else if constexpr (std::is_same_v<T, float>) {
        return AParcel_readFloatArray(parcel, arrayData, &AParcel_stdArrayAllocator<T, N>);
    } else if constexpr (std::is_same_v<T, double>) {
        return AParcel_readDoubleArray(parcel, arrayData, &AParcel_stdArrayAllocator<T, N>);
    } else if constexpr (std::is_same_v<T, std::string>) {
        return AParcel_readStringArray(parcel, arrayData, &AParcel_stdArrayExternalAllocator<N>,
                                       &AParcel_stdArrayStringElementAllocator<N>);
    } else {
        return AParcel_readParcelableArray(parcel, arrayData, &AParcel_stdArrayExternalAllocator<N>,
                                           &AParcel_readStdArrayData<T, N>);
    }
}

/**
 * Reads a fixed-size array of T.
 */
template <typename T, size_t N>
static inline binder_status_t AParcel_readFixedArrayWithNullableData(const AParcel* parcel,
                                                                     std::array<T, N>* arr) {
    void* arrayData = static_cast<void*>(arr);
    if constexpr (std::is_same_v<T, bool> || std::is_same_v<T, uint8_t> ||
                  std::is_same_v<T, char16_t> || std::is_same_v<T, int32_t> ||
                  std::is_same_v<T, int64_t> || std::is_same_v<T, float> ||
                  std::is_same_v<T, double> || std::is_same_v<T, std::string>) {
        return AParcel_readFixedArray(parcel, arr);
    } else if constexpr (std::is_same_v<T, std::optional<std::string>>) {
        return AParcel_readStringArray(parcel, arrayData, &AParcel_stdArrayExternalAllocator<N>,
                                       &AParcel_stdArrayNullableStringElementAllocator<N>);
    } else {
        return AParcel_readParcelableArray(parcel, arrayData, &AParcel_stdArrayExternalAllocator<N>,
                                           &AParcel_readStdArrayNullableData<T, N>);
    }
}

/**
 * Reads a fixed-size array of T.
 */
template <typename T, size_t N>
static inline binder_status_t AParcel_readNullableFixedArrayWithNullableData(
        const AParcel* parcel, std::optional<std::array<T, N>>* arr) {
    void* arrayData = static_cast<void*>(arr);
    if constexpr (std::is_same_v<T, bool>) {
        return AParcel_readBoolArray(parcel, arrayData,
                                     &AParcel_nullableStdArrayExternalAllocator<T, N>,
                                     &AParcel_nullableStdArraySetter<T, N>);
    } else if constexpr (std::is_same_v<T, uint8_t>) {
        return AParcel_readByteArray(parcel, arrayData,
                                     &AParcel_nullableStdArrayAllocator<int8_t, N>);
    } else if constexpr (std::is_same_v<T, char16_t>) {
        return AParcel_readCharArray(parcel, arrayData, &AParcel_nullableStdArrayAllocator<T, N>);
    } else if constexpr (std::is_same_v<T, int32_t>) {
        return AParcel_readInt32Array(parcel, arrayData, &AParcel_nullableStdArrayAllocator<T, N>);
    } else if constexpr (std::is_same_v<T, int64_t>) {
        return AParcel_readInt64Array(parcel, arrayData, &AParcel_nullableStdArrayAllocator<T, N>);
    } else if constexpr (std::is_same_v<T, float>) {
        return AParcel_readFloatArray(parcel, arrayData, &AParcel_nullableStdArrayAllocator<T, N>);
    } else if constexpr (std::is_same_v<T, double>) {
        return AParcel_readDoubleArray(parcel, arrayData, &AParcel_nullableStdArrayAllocator<T, N>);
    } else if constexpr (std::is_same_v<T, std::string>) {
        return AParcel_readStringArray(parcel, arrayData,
                                       &AParcel_nullableStdArrayExternalAllocator<N>,
                                       &AParcel_nullableStdArrayStringElementAllocator<N>);
    } else {
        return AParcel_readParcelableArray(parcel, arrayData,
                                           &AParcel_nullableStdArrayExternalAllocator<T, N>,
                                           &AParcel_readStdArrayNullableData<T, N>);
    }
}

/**
 * Convenience API for writing a value of any type.
 */
template <typename T>
static inline binder_status_t AParcel_writeData(AParcel* parcel, const T& value) {
    if constexpr (is_specialization_v<T, std::vector>) {
        return AParcel_writeVector(parcel, value);
    } else if constexpr (is_fixed_array_v<T>) {
        return AParcel_writeFixedArray(parcel, value);
    } else if constexpr (std::is_same_v<std::string, T>) {
        return AParcel_writeString(parcel, value);
    } else if constexpr (std::is_same_v<bool, T>) {
        return AParcel_writeBool(parcel, value);
    } else if constexpr (std::is_same_v<int8_t, T> || std::is_same_v<uint8_t, T>) {
        return AParcel_writeByte(parcel, value);
    } else if constexpr (std::is_same_v<char16_t, T>) {
        return AParcel_writeChar(parcel, value);
    } else if constexpr (std::is_same_v<int32_t, T>) {
        return AParcel_writeInt32(parcel, value);
    } else if constexpr (std::is_same_v<int64_t, T>) {
        return AParcel_writeInt64(parcel, value);
    } else if constexpr (std::is_same_v<float, T>) {
        return AParcel_writeFloat(parcel, value);
    } else if constexpr (std::is_same_v<double, T>) {
        return AParcel_writeDouble(parcel, value);
    } else if constexpr (std::is_same_v<ScopedFileDescriptor, T>) {
        return AParcel_writeRequiredParcelFileDescriptor(parcel, value);
    } else if constexpr (std::is_same_v<SpAIBinder, T>) {
        return AParcel_writeRequiredStrongBinder(parcel, value);
    } else if constexpr (std::is_enum_v<T>) {
        return AParcel_writeData(parcel, static_cast<std::underlying_type_t<T>>(value));
    } else if constexpr (is_interface_v<T>) {
        return AParcel_writeParcelable(parcel, value);
    } else if constexpr (is_parcelable_v<T>) {
        return AParcel_writeParcelable(parcel, value);
    } else {
        static_assert(dependent_false_v<T>, "unrecognized type");
    }
}

/**
 * Convenience API for writing a nullable value of any type.
 */
template <typename T>
static inline binder_status_t AParcel_writeNullableData(AParcel* parcel, const T& value) {
    if constexpr (is_specialization_v<T, std::optional> &&
                  is_specialization_v<first_template_type_t<T>, std::vector>) {
        return AParcel_writeVector(parcel, value);
    } else if constexpr (is_specialization_v<T, std::optional> &&
                         is_fixed_array_v<first_template_type_t<T>>) {
        return AParcel_writeNullableFixedArrayWithNullableData(parcel, value);
    } else if constexpr (is_fixed_array_v<T>) {  // happens with a nullable multi-dimensional array.
        return AParcel_writeFixedArrayWithNullableData(parcel, value);
    } else if constexpr (is_specialization_v<T, std::optional> &&
                         std::is_same_v<first_template_type_t<T>, std::string>) {
        return AParcel_writeString(parcel, value);
    } else if constexpr (is_nullable_parcelable_v<T> || is_interface_v<T>) {
        return AParcel_writeNullableParcelable(parcel, value);
    } else if constexpr (std::is_same_v<ScopedFileDescriptor, T>) {
        return AParcel_writeNullableParcelFileDescriptor(parcel, value);
    } else if constexpr (std::is_same_v<SpAIBinder, T>) {
        return AParcel_writeNullableStrongBinder(parcel, value);
    } else {
        return AParcel_writeData(parcel, value);
    }
}

/**
 * Convenience API for reading a value of any type.
 */
template <typename T>
static inline binder_status_t AParcel_readData(const AParcel* parcel, T* value) {
    if constexpr (is_specialization_v<T, std::vector>) {
        return AParcel_readVector(parcel, value);
    } else if constexpr (is_fixed_array_v<T>) {
        return AParcel_readFixedArray(parcel, value);
    } else if constexpr (std::is_same_v<std::string, T>) {
        return AParcel_readString(parcel, value);
    } else if constexpr (std::is_same_v<bool, T>) {
        return AParcel_readBool(parcel, value);
    } else if constexpr (std::is_same_v<int8_t, T> || std::is_same_v<uint8_t, T>) {
        return AParcel_readByte(parcel, value);
    } else if constexpr (std::is_same_v<char16_t, T>) {
        return AParcel_readChar(parcel, value);
    } else if constexpr (std::is_same_v<int32_t, T>) {
        return AParcel_readInt32(parcel, value);
    } else if constexpr (std::is_same_v<int64_t, T>) {
        return AParcel_readInt64(parcel, value);
    } else if constexpr (std::is_same_v<float, T>) {
        return AParcel_readFloat(parcel, value);
    } else if constexpr (std::is_same_v<double, T>) {
        return AParcel_readDouble(parcel, value);
    } else if constexpr (std::is_same_v<ScopedFileDescriptor, T>) {
        return AParcel_readRequiredParcelFileDescriptor(parcel, value);
    } else if constexpr (std::is_same_v<SpAIBinder, T>) {
        return AParcel_readRequiredStrongBinder(parcel, value);
    } else if constexpr (std::is_enum_v<T>) {
        return AParcel_readData(parcel, reinterpret_cast<std::underlying_type_t<T>*>(value));
    } else if constexpr (is_interface_v<T>) {
        return AParcel_readParcelable(parcel, value);
    } else if constexpr (is_parcelable_v<T>) {
        return AParcel_readParcelable(parcel, value);
    } else {
        static_assert(dependent_false_v<T>, "unrecognized type");
    }
}

/**
 * Convenience API for reading a nullable value of any type.
 */
template <typename T>
static inline binder_status_t AParcel_readNullableData(const AParcel* parcel, T* value) {
    if constexpr (is_specialization_v<T, std::optional> &&
                  is_specialization_v<first_template_type_t<T>, std::vector>) {
        return AParcel_readVector(parcel, value);
    } else if constexpr (is_specialization_v<T, std::optional> &&
                         is_fixed_array_v<first_template_type_t<T>>) {
        return AParcel_readNullableFixedArrayWithNullableData(parcel, value);
    } else if constexpr (is_fixed_array_v<T>) {  // happens with a nullable multi-dimensional array.
        return AParcel_readFixedArrayWithNullableData(parcel, value);
    } else if constexpr (is_specialization_v<T, std::optional> &&
                         std::is_same_v<first_template_type_t<T>, std::string>) {
        return AParcel_readString(parcel, value);
    } else if constexpr (is_nullable_parcelable_v<T> || is_interface_v<T>) {
        return AParcel_readNullableParcelable(parcel, value);
    } else if constexpr (std::is_same_v<ScopedFileDescriptor, T>) {
        return AParcel_readNullableParcelFileDescriptor(parcel, value);
    } else if constexpr (std::is_same_v<SpAIBinder, T>) {
        return AParcel_readNullableStrongBinder(parcel, value);
    } else {
        return AParcel_readData(parcel, value);
    }
}

}  // namespace ndk

/** @} */
