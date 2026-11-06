/*
 * Copyright (C) 2019 The Android Open Source Project
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
 * @file binder_enums.h
 * @brief Helpers for AIDL enum types.
 */

#pragma once

#include <iterator>
#include <type_traits>

namespace ndk {

namespace internal {
/**
 * Never instantiated. Used as a placeholder for template variables.
 */
template <typename T>
struct invalid_type;

/**
 * AIDL generates specializations of this for enums.
 */
template <typename EnumType, typename = std::enable_if_t<std::is_enum<EnumType>::value>>
constexpr invalid_type<EnumType> enum_values;
}  // namespace internal

/**
 * Iterable interface to enumerate all values of AIDL enum types.
 */
template <typename EnumType, typename = std::enable_if_t<std::is_enum<EnumType>::value>>
struct enum_range {
    /**
     * Return an iterator pointing to the first enum value.
     */
    constexpr auto begin() const { return std::begin(internal::enum_values<EnumType>); }
    /**
     * Return an iterator pointing to one past the last enum value.
     */
    constexpr auto end() const { return std::end(internal::enum_values<EnumType>); }
};

}  // namespace ndk

/** @} */