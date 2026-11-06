/*
 * Copyright (C) 2020 The Android Open Source Project
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
 * @file binder_internal_logging.h
 * @brief This provides the ability to use syslog from binder headers, since
 * other logging functionality might be inaccessable.
 */

#pragma once

// defined differently by liblog
#pragma push_macro("LOG_PRI")
#ifdef LOG_PRI
#undef LOG_PRI
#endif
#include <syslog.h>
#pragma pop_macro("LOG_PRI")

/** @} */
