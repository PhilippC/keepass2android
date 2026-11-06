/*
 * Copyright (C) 2025 The Android Open Source Project
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

#include <sys/cdefs.h>

#if defined(__BIONIC__)
#define API_LEVEL_AT_LEAST(sdk_api_level) __builtin_available(android sdk_api_level, *)
#elif defined(TRUSTY_USERSPACE)
// TODO(b/349936395): set to true for Trusty
#define API_LEVEL_AT_LEAST(sdk_api_level) (false)
#else
#define API_LEVEL_AT_LEAST(sdk_api_level) (true)
#endif  // __BIONIC__
