/*
 * Copyright 2009 Brian Pellin.

This file was derived from

Copyright 2007 Naomaru Itoi <nao@phoneid.org>

This file was derived from 

Java clone of KeePass - A KeePass file viewer for Java
Copyright 2006 Bill Zwicky <billzwicky@users.sourceforge.net>

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

package com.keepassdroid.database;

import java.security.acl.LastOwnerException;
import java.util.ArrayList;
import java.util.Calendar;
import java.util.Date;
import java.util.List;

/**
 * @author Brian Pellin <bpellin@gmail.com>
 * @author Naomaru Itoi <nao@phoneid.org>
 * @author Bill Zwicky <wrzwicky@pobox.com>
 * @author Dominik Reichl <dominik.reichl@t-online.de>
 */
public class PwGroupV3 {
	public PwGroupV3() {
	}

	//Mono for Android binding somehow doesn't return List<PwGroupV3> but only IList and casting of the contents doesn't work.
	//therefore, getGroupAt() must be used in C#, see below
	public ArrayList<PwGroupV3> childGroups = new ArrayList<PwGroupV3>();

	public ArrayList<PwEntryV3> childEntries = new ArrayList<PwEntryV3>();
	public String name = "";
	public PwIconStandard icon;
	
	 
	public PwGroupV3 getGroupAt(int i)
	{
		return childGroups.get(i);
	}
	public PwEntryV3 getEntryAt(int i)
	{
		return childEntries.get(i);
	}

	public PwIconStandard getIcon() {
		return icon;
	}
	public void setIcon(PwIconStandard _icon) {
		icon = _icon;
	}

	public void super_initNewGroup(String nm, PwGroupIdV3 newId) {
		setId(newId);
		name = nm;
	}

	public boolean isContainedIn(PwGroupV3 container) {
		PwGroupV3 cur = this;
		while (cur != null) {
			if (cur == container) {
				return true;
			}

			cur = cur.getParent();
		}

		return false;
	}

	public void touch(boolean modified, boolean touchParents) {
		Date now = new Date();

		setLastAccessTime(now);

		if (modified) {
			setLastModificationTime(now);
		}

		PwGroupV3 parent = getParent();
		if (touchParents && parent != null) {
			parent.touch(modified, true);
		}
	}

	public String toString() {
		return name;
	}

	public static final Date NEVER_EXPIRE = PwEntryV3.NEVER_EXPIRE;

	/** Size of byte buffer needed to hold this struct. */
	public static final int BUF_SIZE = 124;

	// for tree traversing
	public PwGroupV3 parent = null;

	public int groupId;

	public PwDate tCreation;
	public PwDate tLastMod;
	public PwDate tLastAccess;
	public PwDate tExpire;

	public int level; // short

	/** Used by KeePass internally, don't use */
	public int flags;

	public void setGroups(ArrayList<PwGroupV3> groups) {
		childGroups = groups;
	}

	public PwGroupV3 getParent() {
		return parent;
	}

	public PwGroupIdV3 getId() {
		return new PwGroupIdV3(groupId);
	}

	public void setId(PwGroupIdV3 id) {
		PwGroupIdV3 id3 = (PwGroupIdV3) id;
		groupId = id3.getId();
	}

	public String getName() {
		return name;
	}
	public void setName(String n) {
		name = n;
	}


	public Date getLastMod() {
		return tLastMod.getJDate();
	}

	public void setParent(PwGroupV3 prt) {
		parent = (PwGroupV3) prt;
		level = parent.level + 1;

	}

	public void initNewGroup(String nm, PwGroupIdV3 newId) {
		super_initNewGroup(nm, newId);

		Date now = Calendar.getInstance().getTime();
		tCreation = new PwDate(now);
		tLastAccess = new PwDate(now);
		tLastMod = new PwDate(now);
		tExpire = new PwDate(PwGroupV3.NEVER_EXPIRE);

	}

	public void populateBlankFields(PwDatabaseV3 db) {
		if (icon == null) {
			icon = db.iconFactory.getIcon(1);
		}

		if (name == null) {
			name = "";
		}

		if (tCreation == null) {
			tCreation = PwEntryV3.DEFAULT_PWDATE;
		}

		if (tLastMod == null) {
			tLastMod = PwEntryV3.DEFAULT_PWDATE;
		}

		if (tLastAccess == null) {
			tLastAccess = PwEntryV3.DEFAULT_PWDATE;
		}

		if (tExpire == null) {
			tExpire = PwEntryV3.DEFAULT_PWDATE;
		}
	}

	public void setLastAccessTime(Date date) {
		tLastAccess = new PwDate(date);
	}

	public void setLastModificationTime(Date date) {
		tLastMod = new PwDate(date);
	}

}
