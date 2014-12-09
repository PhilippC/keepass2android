package com.inputstick.api;

import java.security.MessageDigest;
import javax.crypto.Cipher;
import javax.crypto.spec.IvParameterSpec;
import javax.crypto.spec.SecretKeySpec;

public class AES {
	
	private Cipher mCipherEncr;
	private Cipher mCipherDecr;
	private SecretKeySpec mKey;	
	private boolean ready;
	
	public AES() {
		ready = false;
	}
	
	public static byte[] getMD5(String s) {
		try {
			MessageDigest md = MessageDigest.getInstance("MD5");
			return md.digest(s.getBytes("UTF-8"));
		} catch (Exception e) {
			e.printStackTrace();
		} 		
		return null;
	}
	
	public byte[] init(byte[] key) {
		byte[] iv = null;
		try {
			mKey = new SecretKeySpec(key, "AES");		
			mCipherEncr = Cipher.getInstance("AES/CBC/NoPadding");					
			mCipherEncr.init(Cipher.ENCRYPT_MODE, mKey);
			iv = mCipherEncr.getIV();
			Util.printHex(iv, "AES IV: ");
			mCipherDecr = Cipher.getInstance("AES/CBC/NoPadding");
			mCipherDecr.init(Cipher.DECRYPT_MODE, mKey, new IvParameterSpec(iv));			
			ready = true;
		} catch (Exception e) {
			e.printStackTrace();
		}
		return iv;
	}		
	
	public byte[] encrypt(byte[] data) {
		return mCipherEncr.update(data);
	}
	
	public byte[] decrypt(byte[] data) {
		return mCipherDecr.update(data);
	}

	public boolean isReady() {
		return ready;
	}
}
