/*
 * Copyright 2009 Brian Pellin.
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
package com.keepassdroid.crypto.finalkey;

import java.io.IOException;

import com.keepassdroid.crypto.NativeLib;


public class NativeFinalKey extends FinalKey {
	
	public static boolean availble() {
		return NativeLib.init();
	}

	@Override
	public byte[] transformMasterKey(byte[] seed, byte[] key, int rounds) throws IOException {
		NativeLib.init();
		
		return nTransformMasterKey(seed, key, rounds);

	}
	
	private static native byte[] nTransformMasterKey(byte[] seed, byte[] key, int rounds);

	// For testing
	/*
	public static byte[] reflect(byte[] key) {
		NativeLib.init();
		
		return nativeReflect(key);
	}
	
	private static native byte[] nativeReflect(byte[] key);
	*/
	

}
