/*
 * Copyright (C) 2011 ZXing authors
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

package com.google.zxing.client.android.wifi;

enum NetworkType {

  WEP,
  WPA,
  NO_PASSWORD;

  static NetworkType forIntentValue(String networkTypeString) {
    if (networkTypeString == null) {
      return NO_PASSWORD;
    }
    if ("WPA".equals(networkTypeString)) {
      return WPA;
    }
    if ("WEP".equals(networkTypeString)) {
      return WEP;
    }
    if ("nopass".equals(networkTypeString)) {
      return NO_PASSWORD;
    }
    throw new IllegalArgumentException(networkTypeString);
  }

}
