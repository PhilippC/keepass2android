/*
 * Copyright 2009-2011 Brian Pellin.
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
 *

Derived from

KeePass for J2ME

Copyright 2007 Naomaru Itoi <nao@phoneid.org>

This file was derived from 

Java clone of KeePass - A KeePass file viewer for Java
Copyright 2006 Bill Zwicky <billzwicky@users.sourceforge.net>

This program is free software; you can redistribute it and/or modify
it under the terms of the GNU General Public License as published by
the Free Software Foundation; either version 2 

This program is distributed in the hope that it will be useful,
but WITHOUT ANY WARRANTY; without even the implied warranty of
MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
GNU General Public License for more details.

You should have received a copy of the GNU General Public License
along with this program; if not, write to the Free Software
Foundation, Inc., 51 Franklin St, Fifth Floor, Boston, MA  02110-1301  USA
 */

package com.keepassdroid.database;

// Java
import java.io.BufferedInputStream;
import java.io.ByteArrayInputStream;
import java.io.ByteArrayOutputStream;
import java.io.File;
import java.io.FileInputStream;
import java.io.FileNotFoundException;
import java.io.IOException;
import java.io.InputStream;
import java.io.PushbackInputStream;
import java.io.UnsupportedEncodingException;
import java.security.DigestOutputStream;
import java.security.MessageDigest;
import java.security.NoSuchAlgorithmException;
import java.util.ArrayList;
import java.util.List;
import java.util.Random;
import com.keepassdroid.crypto.finalkey.FinalKey;
import com.keepassdroid.crypto.finalkey.FinalKeyFactory;
import com.keepassdroid.database.exception.InvalidKeyFileException;
import com.keepassdroid.database.exception.KeyFileEmptyException;
import com.keepassdroid.stream.NullOutputStream;

/**
 * @author Naomaru Itoi <nao@phoneid.org>
 * @author Bill Zwicky <wrzwicky@pobox.com>
 * @author Dominik Reichl <dominik.reichl@t-online.de>
 */
public class PwDatabaseV3 {
	// Constants
	// private static final int PWM_SESSION_KEY_SIZE = 12;
	

	public byte masterKey[] = new byte[32];
	public byte[] finalKey;
	public String name = "KeePass database";
	public PwGroupV3 rootGroup;
	public PwIconFactory iconFactory = new PwIconFactory();
	//public Map<PwGroupId, PwGroupV3> groupList = new HashMap<PwGroupId, PwGroupV3>();
	//public Map<UUID, PwEntryV3> entryList = new HashMap<UUID, PwEntryV3>();
	

	// all entries
	public List<PwEntryV3> entries = new ArrayList<PwEntryV3>();
	// all groups
	public List<PwGroupV3> groups = new ArrayList<PwGroupV3>();
	// Algorithm used to encrypt the database
	public PwEncryptionAlgorithm algorithm;
	public int numKeyEncRounds;

	
	
	public void makeFinalKey(byte[] masterSeed, byte[] masterSeed2, int numRounds) throws IOException {

		// Write checksum Checksum
		MessageDigest md = null;
		try {
			md = MessageDigest.getInstance("SHA-256");
		} catch (NoSuchAlgorithmException e) {
			throw new IOException("SHA-256 not implemented here.");
		}
		NullOutputStream nos = new NullOutputStream();
		DigestOutputStream dos = new DigestOutputStream(nos, md);

		byte[] transformedMasterKey = transformMasterKey(masterSeed2, masterKey, numRounds); 
		dos.write(masterSeed);
		dos.write(transformedMasterKey);

		finalKey = md.digest();
	}
	
	/**
	 * Encrypt the master key a few times to make brute-force key-search harder
	 * @throws IOException 
	 */
	private static byte[] transformMasterKey( byte[] pKeySeed, byte[] pKey, int rounds ) throws IOException
	{
		FinalKey key = FinalKeyFactory.createFinalKey();
		
		return key.transformMasterKey(pKeySeed, pKey, rounds);
	}


	
	public void setMasterKey(String key, InputStream keyfileStream)
			throws InvalidKeyFileException, IOException {
				assert( key != null && keyfileStream != null );
			
				masterKey = getMasterKey(key, keyfileStream);
			}

	protected byte[] getCompositeKey(String key, InputStream keyfileStream)
			throws InvalidKeyFileException, IOException {
				assert(key != null && keyfileStream != null);
				
				byte[] fileKey = getFileKey(keyfileStream);
				
				byte[] passwordKey = getPasswordKey(key);
				
				MessageDigest md;
				try {
					md = MessageDigest.getInstance("SHA-256");
				} catch (NoSuchAlgorithmException e) {
					throw new IOException("SHA-256 not supported");
				}
				
				md.update(passwordKey);
				
				return md.digest(fileKey);
	}
	
	protected byte[] getFileKey(InputStream keyfileStream)
			throws InvalidKeyFileException, IOException {
				assert(keyfileStream != null);
				
				
				byte[] buff = new byte[8000];

				int bytesRead = 0;

				ByteArrayOutputStream bao = new ByteArrayOutputStream();

				while ((bytesRead = keyfileStream.read(buff)) != -1) {
					bao.write(buff, 0, bytesRead);
				}

				byte[] keyFileData = bao.toByteArray();

				ByteArrayInputStream bin = new ByteArrayInputStream(keyFileData);
				
				
				
				if ( keyFileData.length == 32 ) {
					byte[] outputKey = new byte[32];
					if ( bin.read(outputKey, 0, 32) != 32 ) {
						throw new IOException("Error reading key.");
					}
					
					return outputKey;
				} else if ( keyFileData.length == 64 ) {
					byte[] hex = new byte[64];
					
					bin.mark(64);
					if ( bin.read(hex, 0, 64) != 64 ) {
						throw new IOException("Error reading key.");
					}
			
					try {
						return hexStringToByteArray(new String(hex));
					} catch (IndexOutOfBoundsException e) {
						// Key is not base 64, treat it as binary data
						bin.reset();
					}
				}
			
				MessageDigest md;
				try {
					md = MessageDigest.getInstance("SHA-256");
				} catch (NoSuchAlgorithmException e) {
					throw new IOException("SHA-256 not supported");
				}
				//SHA256Digest md = new SHA256Digest();
				byte[] buffer = new byte[2048];
				int offset = 0;
				
				try {
					while (true) {
						bytesRead = bin.read(buffer, 0, 2048);
						if ( bytesRead == -1 ) break;  // End of file
						
						md.update(buffer, 0, bytesRead);
						offset += bytesRead;
						
					}
				} catch (Exception e) {
					System.out.println(e.toString());
				}
			
				return md.digest();
			}

	
	public static byte[] hexStringToByteArray(String s) {
	    int len = s.length();
	    byte[] data = new byte[len / 2];
	    for (int i = 0; i < len; i += 2) {
	        data[i / 2] = (byte) ((Character.digit(s.charAt(i), 16) << 4)
	                             + Character.digit(s.charAt(i+1), 16));
	    }
	    return data;
	}

	protected byte[] getPasswordKey(String key, String encoding) throws IOException {
		assert(key!=null);
		
		if ( key.length() == 0 )
		    throw new IllegalArgumentException( "Key cannot be empty." );
		
		MessageDigest md;
		try {
			md = MessageDigest.getInstance("SHA-256");
		} catch (NoSuchAlgorithmException e) {
			throw new IOException("SHA-256 not supported");
		}

		byte[] bKey;
		try {
			bKey = key.getBytes(encoding);
		} catch (UnsupportedEncodingException e) {
			assert false;
			bKey = key.getBytes();
		}
		md.update(bKey, 0, bKey.length );

		return md.digest();
	}
	
	public void super_addGroupTo(PwGroupV3 newGroup, PwGroupV3 parent) {
		// Add group to parent group
		if ( parent == null ) {
			parent = rootGroup;
		}
		
		parent.childGroups.add(newGroup);
		newGroup.setParent(parent);
		//groupList.put(newGroup.getId(), newGroup);
		
		parent.touch(true, true);
	}
	
	public void super_removeGroupFrom(PwGroupV3 remove, PwGroupV3 parent) {
		// Remove group from parent group
		parent.childGroups.remove(remove);
		
		//groupList.remove(remove.getId());
	}
	
	public void super_addEntryTo(PwEntryV3 newEntry, PwGroupV3 parent) {
		// Add entry to parent
		if (parent != null) {
			parent.childEntries.add(newEntry);
		}
		newEntry.setParent(parent);
		
		//entryList.put(newEntry.getUUID(), newEntry);
	}
	
	public void super_removeEntryFrom(PwEntryV3 remove, PwGroupV3 parent) {
		// Remove entry for parent
		if (parent != null) {
			parent.childEntries.remove(remove);
		}
		//entryList.remove(remove.getUUID());
	}

	
	/**
	 * Determine if an id number is already in use
	 * 
	 * @param id
	 *            ID number to check for
	 * @return True if the ID is used, false otherwise
	 */
	protected boolean isGroupIdUsed(PwGroupIdV3 id) {
		List<PwGroupV3> groups = getGroups();
		
		for (int i = 0; i < groups.size(); i++) {
			PwGroupV3 group =groups.get(i);
			if (group.getId().equals(id)) {
				return true;
			}
		}

		return false;
	}
	
	
	
	public boolean canRecycle(PwGroupV3 group) {
		return false;
	}
	
	public boolean canRecycle(PwEntryV3 entry) {
		return false;
	}
	
	public void recycle(PwEntryV3 entry) {
		// Assume calls to this are protected by calling inRecyleBin
		throw new RuntimeException("Call not valid for .kdb databases.");
	}
	
	public void undoRecycle(PwEntryV3 entry, PwGroupV3 origParent) {
		throw new RuntimeException("Call not valid for .kdb databases.");
	}
	
	public void deleteEntry(PwEntryV3 entry) {
		PwGroupV3 parent = entry.getParent();
		removeEntryFrom(entry, parent);
		parent.touch(false, true);
		
	}
	
	public void undoDeleteEntry(PwEntryV3 entry, PwGroupV3 origParent) {
		addEntryTo(entry, origParent);
	}
	
	public PwGroupV3 getRecycleBin() {
		return null;
	}
	
	public boolean super_isGroupSearchable(PwGroupV3 group, boolean omitBackup) {
		return group != null;
	}

	
	public PwEncryptionAlgorithm getEncAlgorithm() {
		return algorithm;
	}

	public int getNumKeyEncRecords() {
		return numKeyEncRounds;
	}

	public List<PwGroupV3> getGroups() {
		return groups;
	}

	public List<PwEntryV3> getEntries() {
		return entries;
	}

	public void setGroups(List<PwGroupV3> grp) {
		groups = grp;
	}

	public ArrayList<PwGroupV3> getGrpRoots() {
		int target = 0;
		ArrayList<PwGroupV3> kids = new ArrayList<PwGroupV3>();
		for (int i = 0; i < groups.size(); i++) {
			PwGroupV3 grp = (PwGroupV3) groups.get(i);
			if (grp.level == target)
				kids.add(grp);
		}
		return kids;
	}

	public int getRootGroupId() {
		for (int i = 0; i < groups.size(); i++) {
			PwGroupV3 grp = (PwGroupV3) groups.get(i);
			if (grp.level == 0) {
				return grp.groupId;
			}
		}

		return -1;
	}

	public ArrayList<PwGroupV3> getGrpChildren(PwGroupV3 parent) {
		int idx = groups.indexOf(parent);
		int target = parent.level + 1;
		ArrayList<PwGroupV3> kids = new ArrayList<PwGroupV3>();
		while (++idx < groups.size()) {
			PwGroupV3 grp = (PwGroupV3) groups.get(idx);
			if (grp.level < target)
				break;
			else if (grp.level == target)
				kids.add(grp);
		}
		return kids;
	}

	public ArrayList<PwEntryV3> getEntries(PwGroupV3 parent) {
		ArrayList<PwEntryV3> kids = new ArrayList<PwEntryV3>();
		/*
		 * for( Iterator i = entries.iterator(); i.hasNext(); ) { PwEntryV3 ent
		 * = (PwEntryV3)i.next(); if( ent.groupId == parent.groupId ) kids.add(
		 * ent ); }
		 */
		for (int i = 0; i < entries.size(); i++) {
			PwEntryV3 ent = (PwEntryV3) entries.get(i);
			if (ent.groupId == parent.groupId)
				kids.add(ent);
		}
		return kids;
	}

	public String toString() {
		return name;
	}

	public void constructTree(PwGroupV3 currentGroup) {
		// I'm in root
		if (currentGroup == null) {
			PwGroupV3 root = new PwGroupV3();
			rootGroup = root;

			ArrayList<PwGroupV3> rootChildGroups = getGrpRoots();
			root.setGroups(rootChildGroups);
			root.childEntries = new ArrayList<PwEntryV3>();
			root.level = -1;
			for (int i = 0; i < rootChildGroups.size(); i++) {
				PwGroupV3 grp = (PwGroupV3) rootChildGroups.get(i);
				grp.parent = root;
				constructTree(grp);
			}
			return;
		}

		// I'm in non-root
		// get child groups
		currentGroup.setGroups(getGrpChildren(currentGroup));
		currentGroup.childEntries = getEntries(currentGroup);

		// set parent in child entries
		for (int i = 0; i < currentGroup.childEntries.size(); i++) {
			PwEntryV3 entry = (PwEntryV3) currentGroup.childEntries.get(i);
			entry.parent = currentGroup;
		}
		// recursively construct child groups
		for (int i = 0; i < currentGroup.childGroups.size(); i++) {
			PwGroupV3 grp = (PwGroupV3) currentGroup.childGroups.get(i);
			grp.parent = currentGroup;
			constructTree((PwGroupV3) currentGroup.childGroups.get(i));
		}
		return;
	}

	/*
	public void removeGroup(PwGroupV3 group) {
		group.parent.childGroups.remove(group);
		groups.remove(group);
	}
	*/

	public PwGroupIdV3 newGroupId() {
		PwGroupIdV3 newId = new PwGroupIdV3(0);

		Random random = new Random();

		while (true) {
			newId = new PwGroupIdV3(random.nextInt());

			if (!isGroupIdUsed(newId)) break;
		}

		return newId;
	}

	public byte[] getMasterKey(String key, InputStream keyfileStream)
			throws InvalidKeyFileException, IOException {
		assert (key != null && keyfileStream != null);

		if (key.length() > 0 && keyfileStream != null) {
			return getCompositeKey(key, keyfileStream);
		} else if (key.length() > 0) {
			return getPasswordKey(key);
		} else if (keyfileStream != null) {
			return getFileKey(keyfileStream);
		} else {
			throw new IllegalArgumentException("Key cannot be empty.");
		}

	}

	public byte[] getPasswordKey(String key) throws IOException {
		return getPasswordKey(key, "ISO-8859-1");
	}


	public long getNumRounds() {
		return numKeyEncRounds;
	}

	public void setNumRounds(long rounds) throws NumberFormatException {
		if (rounds > Integer.MAX_VALUE || rounds < Integer.MIN_VALUE) {
			throw new NumberFormatException();
		}

		numKeyEncRounds = (int) rounds;
	}

	public boolean appSettingsEnabled() {
		return true;
	}

	
	public void addEntryTo(PwEntryV3 newEntry, PwGroupV3 parent) {
		super_addEntryTo(newEntry, parent);
		
		// Add entry to root entries
		entries.add(newEntry);
		
	}

	
	public void addGroupTo(PwGroupV3 newGroup, PwGroupV3 parent) {
		super_addGroupTo(newGroup, parent);
		
		// Add group to root groups
		groups.add(newGroup);
		
	}

	
	public void removeEntryFrom(PwEntryV3 remove, PwGroupV3 parent) {
		super_removeEntryFrom(remove, parent);
		
		// Remove entry from root entry
		entries.remove(remove);
	}

	
	public void removeGroupFrom(PwGroupV3 remove, PwGroupV3 parent) {
		super_removeGroupFrom(remove, parent);
		
		// Remove group from root entry
		groups.remove(remove);
	}

	public PwGroupV3 createGroup() {
		return new PwGroupV3();
	}
	
	// TODO: This could still be refactored cleaner
	public void copyEncrypted(byte[] buf, int offset, int size) {
		// No-op
	}

	// TODO: This could still be refactored cleaner
	public void copyHeader(PwDbHeaderV3 header) {
		// No-op
	}
	public boolean isBackup(PwGroupV3 group) {
		PwGroupV3 g = (PwGroupV3) group;
		while (g != null) {
			if (g.level == 0 && g.name.equalsIgnoreCase("Backup")) {
				return true;
			}
			
			g = g.parent;
		}
		
		return false;
	}

	public boolean isGroupSearchable(PwGroupV3 group, boolean omitBackup) {
		if (!super_isGroupSearchable(group, omitBackup)) {
			return false;
		}
		
		return !(omitBackup && isBackup(group));
	}
}
