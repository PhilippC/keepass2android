/*
 * This file is part of Keepass2Android, Copyright 2025 Philipp Crocoll.
 *
 *   Keepass2Android is free software: you can redistribute it and/or modify
 *   it under the terms of the GNU General Public License as published by
 *   the Free Software Foundation, either version 3 of the License, or
 *   (at your option) any later version.
 *
 *   Keepass2Android is distributed in the hope that it will be useful,
 *   but WITHOUT ANY WARRANTY; without even the implied warranty of
 *   MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 *   GNU General Public License for more details.
 *
 *   You should have received a copy of the GNU General Public License
 *   along with Keepass2Android.  If not, see <http://www.gnu.org/licenses/>.
 */

package keepass2android.kp2afilechooser;


public class FileEntry {
	public String path;
	public String displayName;
	public boolean isDirectory;
	public long lastModifiedTime;
	public boolean canRead;
	public boolean canWrite;
	public long sizeInBytes;
	
	public FileEntry()
	{
		isDirectory = false;
		canRead = canWrite = true;
	}

	@Override
	public String toString() {
		StringBuilder s = new StringBuilder("kp2afilechooser.FileEntry{")
				.append(displayName).append("|")
				.append("path=").append(path).append(",sz=").append(sizeInBytes)
				.append(",").append(isDirectory ? "dir" : "file")
				.append(",lastMod=").append(lastModifiedTime);

		StringBuilder perms = new StringBuilder();
		if (canRead)
			perms.append("r");
		if (canWrite)
			perms.append("w");
		if (perms.length() > 0) {
			s.append(",").append(perms);
		}

		return s.append("}").toString();
	}
}
