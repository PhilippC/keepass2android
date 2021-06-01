/*
 * Copyright 2010-2013 Brian Pellin.
 *     
 * This file is part of KeePassDroid.
 *
 *  KeePassDroid is free software: you can redistribute it and/or modify
 *  it under the terms of the GNU General Public License as published by
 *  the Free Software Foundation, either version 2 of the License, or
 *  (at your option) any later version.
 *
 *  KeePassDroid is distributed in the hope that it will be useful,
 *  but WITHOUT ANY WARRANTY; without even the implied warranty of
 *  MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 *  GNU General Public License for more details.
 *
 *  You should have received a copy of the GNU General Public License
 *  along with KeePassDroid.  If not, see <http://www.gnu.org/licenses/>.
 *
 */
package com.keepassdroid.crypto;

import java.security.InvalidAlgorithmParameterException;
import java.security.InvalidKeyException;
import java.security.NoSuchAlgorithmException;
import java.util.UUID;

import javax.crypto.Cipher;
import javax.crypto.NoSuchPaddingException;
import javax.crypto.spec.IvParameterSpec;
import javax.crypto.spec.SecretKeySpec;

import android.os.Build;

import com.keepassdroid.utils.Types;

public class CipherFactory {
	private static boolean blacklistInit = false;
	private static boolean blacklisted;
	
	public static Cipher getInstance(String transformation) throws NoSuchAlgorithmException, NoSuchPaddingException {
		return getInstance(transformation, false);
	}
	
	public static Cipher getInstance(String transformation, boolean androidOverride) throws NoSuchAlgorithmException, NoSuchPaddingException {
		// Return the native AES if it is possible
		if ( (!deviceBlacklisted()) && (!androidOverride) && hasNativeImplementation(transformation) && NativeLib.loaded() ) {
			return Cipher.getInstance(transformation, new AESProvider());
		} else {
		try {
				return Cipher.getInstance(transformation, new BouncyCastleProvider());
			} catch (NoSuchAlgorithmException e) {
				// Do nothing, fall through
			} catch (NoSuchPaddingException e) {
				// Do nothing, fall through
			}
			return Cipher.getInstance(transformation);
		}
	}
	
	public static boolean deviceBlacklisted() {
		if (!blacklistInit) {
			blacklistInit = true;
			
			// The Acer Iconia A500 is special and seems to always crash in the native crypto libraries
			blacklisted = Build.MODEL.equals("A500");
		}
		return blacklisted;
	}
	
	private static boolean hasNativeImplementation(String transformation) {
		return transformation.equals("AES/CBC/PKCS5Padding");
	}
	
	
	public static final UUID AES_CIPHER = Types.bytestoUUID(
			new byte[]{(byte)0x31, (byte)0xC1, (byte)0xF2, (byte)0xE6, (byte)0xBF, (byte)0x71, (byte)0x43, (byte)0x50,
					   (byte)0xBE, (byte)0x58, (byte)0x05, (byte)0x21, (byte)0x6A, (byte)0xFC, 0x5A, (byte)0xFF 
	});
	
	
	/** Generate appropriate cipher based on KeePass 2.x UUID's
	 * @param uuid
	 * @return
	 * @throws NoSuchPaddingException 
	 * @throws NoSuchAlgorithmException 
	 * @throws InvalidAlgorithmParameterException 
	 * @throws InvalidKeyException 
	 */
	public static Cipher getInstance(UUID uuid, int opmode, byte[] key, byte[] IV) throws NoSuchAlgorithmException, NoSuchPaddingException, InvalidKeyException, InvalidAlgorithmParameterException {
		return getInstance(uuid, opmode, key, IV, false);
	}
	
	public static Cipher getInstance(UUID uuid, int opmode, byte[] key, byte[] IV, boolean androidOverride) throws NoSuchAlgorithmException, NoSuchPaddingException, InvalidKeyException, InvalidAlgorithmParameterException {
		if ( uuid.equals(AES_CIPHER) ) {
			Cipher cipher = CipherFactory.getInstance("AES/CBC/PKCS5Padding", androidOverride); 
			
			cipher.init(opmode, new SecretKeySpec(key, "AES"), new IvParameterSpec(IV));
			
			return cipher;
		}
		
		throw new NoSuchAlgorithmException("UUID unrecognized.");
	}
}
