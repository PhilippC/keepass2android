/*
 * Copyright (C) 2008 ZXing authors
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

package com.google.zxing.client.android.result;

import keepass2android.plugin.qr.R;
import com.google.zxing.client.result.AddressBookParsedResult;
import com.google.zxing.client.result.ParsedResult;

import android.app.Activity;
import android.telephony.PhoneNumberUtils;
import android.text.Spannable;
import android.text.SpannableString;
import android.text.style.StyleSpan;

import java.text.DateFormat;
import java.text.ParseException;
import java.text.SimpleDateFormat;
import java.util.Date;
import java.util.Locale;

/**
 * Handles address book entries.
 *
 * @author dswitkin@google.com (Daniel Switkin)
 */
public final class AddressBookResultHandler extends ResultHandler {

  private static final DateFormat[] DATE_FORMATS = {
    new SimpleDateFormat("yyyyMMdd", Locale.ENGLISH),
    new SimpleDateFormat("yyyyMMdd'T'HHmmss", Locale.ENGLISH),
    new SimpleDateFormat("yyyy-MM-dd", Locale.ENGLISH),
    new SimpleDateFormat("yyyy-MM-dd'T'HH:mm:ss", Locale.ENGLISH),
  };
  static {
    for (DateFormat format : DATE_FORMATS) {
      format.setLenient(false);
    }
  }

  private static final int[] BUTTON_TEXTS = {
    R.string.button_add_contact,
    R.string.button_show_map,
    R.string.button_dial,
    R.string.button_email,
  };

  private final boolean[] fields;
  private int buttonCount;

  // This takes all the work out of figuring out which buttons/actions should be in which
  // positions, based on which fields are present in this barcode.
  private int mapIndexToAction(int index) {
    if (index < buttonCount) {
      int count = -1;
      for (int x = 0; x < MAX_BUTTON_COUNT; x++) {
        if (fields[x]) {
          count++;
        }
        if (count == index) {
          return x;
        }
      }
    }
    return -1;
  }

  public AddressBookResultHandler(Activity activity, ParsedResult result) {
    super(activity, result);
    AddressBookParsedResult addressResult = (AddressBookParsedResult) result;
    String[] addresses = addressResult.getAddresses();
    boolean hasAddress = addresses != null && addresses.length > 0 && addresses[0] != null && !addresses[0].isEmpty();
    String[] phoneNumbers = addressResult.getPhoneNumbers();
    boolean hasPhoneNumber = phoneNumbers != null && phoneNumbers.length > 0;
    String[] emails = addressResult.getEmails();
    boolean hasEmailAddress = emails != null && emails.length > 0;

    fields = new boolean[MAX_BUTTON_COUNT];
    fields[0] = true; // Add contact is always available
    fields[1] = hasAddress;
    fields[2] = hasPhoneNumber;
    fields[3] = hasEmailAddress;

    buttonCount = 0;
    for (int x = 0; x < MAX_BUTTON_COUNT; x++) {
      if (fields[x]) {
        buttonCount++;
      }
    }
  }

  @Override
  public int getButtonCount() {
    return buttonCount;
  }

  @Override
  public int getButtonText(int index) {
    return BUTTON_TEXTS[mapIndexToAction(index)];
  }

  @Override
  public void handleButtonPress(int index) {
    AddressBookParsedResult addressResult = (AddressBookParsedResult) getResult();
    String[] addresses = addressResult.getAddresses();
    String address1 = addresses == null || addresses.length < 1 ? null : addresses[0];
    String[] addressTypes = addressResult.getAddressTypes();
    String address1Type = addressTypes == null || addressTypes.length < 1 ? null : addressTypes[0];
    int action = mapIndexToAction(index);
    switch (action) {
      case 0:
        addContact(addressResult.getNames(),
                   addressResult.getNicknames(),
                   addressResult.getPronunciation(),
                   addressResult.getPhoneNumbers(),
                   addressResult.getPhoneTypes(),
                   addressResult.getEmails(),
                   addressResult.getEmailTypes(),
                   addressResult.getNote(),
                   addressResult.getInstantMessenger(),
                   address1,
                   address1Type,
                   addressResult.getOrg(),
                   addressResult.getTitle(),
                   addressResult.getURLs(),
                   addressResult.getBirthday(),
                   addressResult.getGeo());
        break;
      case 1:
        String[] names = addressResult.getNames();
        String title = names != null ? names[0] : null;
        searchMap(address1, title);
        break;
      case 2:
        dialPhone(addressResult.getPhoneNumbers()[0]);
        break;
      case 3:
        sendEmail(addressResult.getEmails()[0], null, null);
        break;
      default:
        break;
    }
  }

  private static Date parseDate(String s) {
    for (DateFormat currentFormat : DATE_FORMATS) {
      try {
        return currentFormat.parse(s);
      } catch (ParseException e) {
        // continue
      }
    }
    return null;
  }

  // Overriden so we can hyphenate phone numbers, format birthdays, and bold the name.
  @Override
  public CharSequence getDisplayContents() {
    AddressBookParsedResult result = (AddressBookParsedResult) getResult();
    StringBuilder contents = new StringBuilder(100);
    ParsedResult.maybeAppend(result.getNames(), contents);
    int namesLength = contents.length();

    String pronunciation = result.getPronunciation();
    if (pronunciation != null && !pronunciation.isEmpty()) {
      contents.append("\n(");
      contents.append(pronunciation);
      contents.append(')');
    }

    ParsedResult.maybeAppend(result.getTitle(), contents);
    ParsedResult.maybeAppend(result.getOrg(), contents);
    ParsedResult.maybeAppend(result.getAddresses(), contents);
    String[] numbers = result.getPhoneNumbers();
    if (numbers != null) {
      for (String number : numbers) {
        if (number != null) {
          ParsedResult.maybeAppend(PhoneNumberUtils.formatNumber(number), contents);
        }
      }
    }
    ParsedResult.maybeAppend(result.getEmails(), contents);
    ParsedResult.maybeAppend(result.getURLs(), contents);

    String birthday = result.getBirthday();
    if (birthday != null && !birthday.isEmpty()) {
      Date date = parseDate(birthday);
      if (date != null) {
        ParsedResult.maybeAppend(DateFormat.getDateInstance(DateFormat.MEDIUM).format(date.getTime()), contents);
      }
    }
    ParsedResult.maybeAppend(result.getNote(), contents);

    if (namesLength > 0) {
      // Bold the full name to make it stand out a bit.
      Spannable styled = new SpannableString(contents.toString());
      styled.setSpan(new StyleSpan(android.graphics.Typeface.BOLD), 0, namesLength, 0);
      return styled;
    } else {
      return contents.toString();
    }
  }

  @Override
  public int getDisplayTitle() {
    return R.string.result_address_book;
  }
}
