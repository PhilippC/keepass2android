/*
` * Copyright 2009-2011 Brian Pellin.
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
package com.keepassdroid.database.save;

import java.io.BufferedOutputStream;
import java.io.IOException;
import java.io.OutputStream;
import java.security.DigestOutputStream;
import java.security.InvalidAlgorithmParameterException;
import java.security.InvalidKeyException;
import java.security.MessageDigest;
import java.security.NoSuchAlgorithmException;
import java.util.ArrayList;
import java.util.List;

import javax.crypto.Cipher;
import javax.crypto.CipherOutputStream;
import javax.crypto.spec.IvParameterSpec;
import javax.crypto.spec.SecretKeySpec;

import com.keepassdroid.crypto.CipherFactory;
import com.keepassdroid.database.PwDatabaseV3;
import com.keepassdroid.database.PwDbHeader;
import com.keepassdroid.database.PwDbHeaderV3;
import com.keepassdroid.database.PwEncryptionAlgorithm;
import com.keepassdroid.database.PwEntryV3;
import com.keepassdroid.database.PwGroupV3;
import com.keepassdroid.database.exception.PwDbOutputException;
import com.keepassdroid.stream.NullOutputStream;

public class PwDbV3Output extends PwDbOutput {
	private PwDatabaseV3 mPM;
	
	public PwDbV3Output(PwDatabaseV3 pm, OutputStream os) {
		super(os);
		
		mPM = pm;
	}

	public byte[] getFinalKey(PwDbHeader header) throws PwDbOutputException {
		try {
			mPM.makeFinalKey(header.masterSeed, header.transformSeed, mPM.numKeyEncRounds);
			return mPM.finalKey;
		} catch (IOException e) {
			throw new PwDbOutputException("Key creation failed: " + e.getMessage());
		}
	}
	
	@Override
	public void output() throws PwDbOutputException {
		prepForOutput();
		
		PwDbHeader header = outputHeader(mOS);
		
		byte[] finalKey = getFinalKey(header);
		
		Cipher cipher;
		try {
			if (mPM.algorithm == PwEncryptionAlgorithm.Rjindal) {
				cipher = CipherFactory.getInstance("AES/CBC/PKCS5Padding");
			} else if (mPM.algorithm == PwEncryptionAlgorithm.Twofish){
				cipher = CipherFactory.getInstance("TWOFISH/CBC/PKCS7PADDING");
			} else {
				throw new Exception();
			}
		} catch (Exception e) {
			throw new PwDbOutputException("Algorithm not supported.");
		}

		try {
			cipher.init( Cipher.ENCRYPT_MODE, new SecretKeySpec(finalKey, "AES" ), new IvParameterSpec(header.encryptionIV) );
			CipherOutputStream cos = new CipherOutputStream(mOS, cipher);
			BufferedOutputStream bos = new BufferedOutputStream(cos);
			outputPlanGroupAndEntries(bos);
			bos.flush();
			bos.close();

		} catch (InvalidKeyException e) {
			throw new PwDbOutputException("Invalid key");
		} catch (InvalidAlgorithmParameterException e) {
			throw new PwDbOutputException("Invalid algorithm parameter.");
		} catch (IOException e) {
			throw new PwDbOutputException("Failed to output final encrypted part.");
		}
	}
	
	private void prepForOutput() {
		// Before we output the header, we should sort our list of groups and remove any orphaned nodes that are no longer part of the group hierarchy
		sortGroupsForOutput();
	}

	public PwDbHeader outputHeader(OutputStream os) throws PwDbOutputException {
		// Build header
		PwDbHeaderV3 header = new PwDbHeaderV3();
		header.signature1 = PwDbHeader.PWM_DBSIG_1;
		header.signature2 = PwDbHeaderV3.DBSIG_2;
		header.flags = PwDbHeaderV3.FLAG_SHA2;
		
		if ( mPM.getEncAlgorithm() == PwEncryptionAlgorithm.Rjindal ) {
			header.flags |= PwDbHeaderV3.FLAG_RIJNDAEL;
		} else if ( mPM.getEncAlgorithm() == PwEncryptionAlgorithm.Twofish ) {
			header.flags |= PwDbHeaderV3.FLAG_TWOFISH;
		} else {
			throw new PwDbOutputException("Unsupported algorithm.");
		}
		
		header.version = PwDbHeaderV3.DBVER_DW;
		header.numGroups = mPM.getGroups().size();
		header.numEntries = mPM.entries.size();
		header.numKeyEncRounds = mPM.getNumKeyEncRecords();
		
		setIVs(header);
		
		// Write checksum Checksum
		MessageDigest md = null;
		try {
			md = MessageDigest.getInstance("SHA-256");
		} catch (NoSuchAlgorithmException e) {
			throw new PwDbOutputException("SHA-256 not implemented here.");
		}
		
		NullOutputStream nos;
		nos = new NullOutputStream();
		DigestOutputStream dos = new DigestOutputStream(nos, md);
		BufferedOutputStream bos = new BufferedOutputStream(dos);
		try {
			outputPlanGroupAndEntries(bos);
			bos.flush();
			bos.close();
		} catch (IOException e) {
			throw new PwDbOutputException("Failed to generate checksum.");
		}

		header.contentsHash = md.digest();
		
		// Output header
		PwDbHeaderOutputV3 pho = new PwDbHeaderOutputV3(header, os);
		try {
			pho.output();
		} catch (IOException e) {
			throw new PwDbOutputException("Failed to output the header.");
		}

		return header;
	}
	
	public void outputPlanGroupAndEntries(OutputStream os) throws PwDbOutputException  {
		//long size = 0;
		
		// Groups
		List<PwGroupV3> groups = mPM.getGroups();
		for ( int i = 0; i < groups.size(); i++ ) {
			PwGroupV3 pg = (PwGroupV3) groups.get(i);
			PwGroupOutputV3 pgo = new PwGroupOutputV3(pg, os);
			try {
				pgo.output();
			} catch (IOException e) {
				throw new PwDbOutputException("Failed to output a group: " + e.getMessage());
			}
		}
		
		// Entries
		for (int i = 0; i < mPM.entries.size(); i++ ) {
			PwEntryV3 pe = (PwEntryV3) mPM.entries.get(i);
			PwEntryOutputV3 peo = new PwEntryOutputV3(pe, os);
			try {
				peo.output();
			} catch (IOException e) {
				throw new PwDbOutputException("Failed to output an entry.");
			}
		}
	}
	
	private void sortGroupsForOutput() {
		List<PwGroupV3> groupList = new ArrayList<PwGroupV3>();
		
		// Rebuild list according to coalation sorting order removing any orphaned groups
		List<PwGroupV3> roots = mPM.getGrpRoots();
		for ( int i = 0; i < roots.size(); i++ ) {
			sortGroup((PwGroupV3) roots.get(i), groupList);
		}
		
		mPM.setGroups(groupList);
	}
	
	private void sortGroup(PwGroupV3 group, List<PwGroupV3> groupList) {
		// Add current group
		groupList.add(group);
		
		// Recurse over children
		for ( int i = 0; i < group.childGroups.size(); i++ ) {
			sortGroup((PwGroupV3) group.childGroups.get(i), groupList);
		}
	}
}
