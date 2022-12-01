/*
  KeePass Password Safe - The Open-Source Password Manager
  Copyright (C) 2003-2021 Dominik Reichl <dominik.reichl@t-online.de>

  This program is free software; you can redistribute it and/or modify
  it under the terms of the GNU General Public License as published by
  the Free Software Foundation; either version 2 of the License, or
  (at your option) any later version.

  This program is distributed in the hope that it will be useful,
  but WITHOUT ANY WARRANTY; without even the implied warranty of
  MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
  GNU General Public License for more details.

  You should have received a copy of the GNU General Public License
  along with this program; if not, write to the Free Software
  Foundation, Inc., 51 Franklin St, Fifth Floor, Boston, MA  02110-1301  USA
*/

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Xml;
using keepass2android;

using KeePassLib;
using KeePassLib.Collections;
using KeePassLib.Interfaces;
using KeePassLib.Resources;
using KeePassLib.Security;
using KeePassLib.Utility;

namespace KeePassLib.Serialization
{
	/// <summary>
	/// Serialization to KeePass KDBX files.
	/// </summary>
	public sealed partial class KdbxFile
	{
		private enum KdbContext
		{
			Null,
			KeePassFile,
			Meta,
			Root,
			MemoryProtection,
			CustomIcons,
			CustomIcon,
			Binaries,
			CustomData,
			CustomDataItem,
			RootDeletedObjects,
			DeletedObject,
			Group,
			GroupTimes,
			GroupCustomData,
			GroupCustomDataItem,
			Entry,
			EntryTimes,
			EntryString,
			EntryBinary,
			EntryAutoType,
			EntryAutoTypeItem,
			EntryHistory,
			EntryCustomData,
			EntryCustomDataItem
		}

		private bool m_bReadNextNode = true;
		private Stack<PwGroup> m_ctxGroups = new Stack<PwGroup>();
		private PwGroup m_ctxGroup = null;
		private PwEntry m_ctxEntry = null;
		private string m_ctxStringName = null;
		private ProtectedString m_ctxStringValue = null;
		private string m_ctxBinaryName = null;
		private ProtectedBinary m_ctxBinaryValue = null;
		private string m_ctxATName = null;
		private string m_ctxATSeq = null;
		private bool m_bEntryInHistory = false;
		private PwEntry m_ctxHistoryBase = null;
		private PwDeletedObject m_ctxDeletedObject = null;
		private PwUuid m_uuidCustomIconID = PwUuid.Zero;
		private byte[] m_pbCustomIconData = null;
		private string m_strCustomIconName = null;
		private DateTime? m_odtCustomIconLastMod = null;
		private string m_strCustomDataKey = null;
		private string m_strCustomDataValue = null;
		private DateTime? m_odtCustomDataLastMod = null;
		private string m_strGroupCustomDataKey = null;
		private string m_strGroupCustomDataValue = null;
		private string m_strEntryCustomDataKey = null;
		private string m_strEntryCustomDataValue = null;

		private void ReadXmlStreamed(Stream sXml, Stream sParent)
		{
			using (XmlReader xr = XmlUtilEx.CreateXmlReader(sXml))
			{
				ReadDocumentStreamed(xr, sParent);
			}
		}

		private void ReadDocumentStreamed(XmlReader xr, Stream sParentStream)
		{
			Debug.Assert(xr != null);
			if (xr == null) throw new ArgumentNullException("xr");

			m_ctxGroups.Clear();

			KdbContext ctx = KdbContext.Null;

			uint uTagCounter = 0;

			bool bSupportsStatus = (m_slLogger != null);
			long lStreamLength = 1;
			try
			{
				sParentStream.Position.ToString(); // Test Position support
				lStreamLength = sParentStream.Length;
			}
			catch (Exception) { bSupportsStatus = false; }
			if (lStreamLength <= 0) { Debug.Assert(false); lStreamLength = 1; }

			m_bReadNextNode = true;

			while (true)
			{
				if (m_bReadNextNode)
				{
					if (!xr.Read()) break;
				}
				else m_bReadNextNode = true;

				switch (xr.NodeType)
				{
					case XmlNodeType.Element:
						ctx = ReadXmlElement(ctx, xr);
						break;

					case XmlNodeType.EndElement:
						ctx = EndXmlElement(ctx, xr);
						break;

					case XmlNodeType.XmlDeclaration:
						break; // Ignore

					default:
						Debug.Assert(false);
						break;
				}

				++uTagCounter;
				if (((uTagCounter & 0xFFU) == 0) && bSupportsStatus)
				{
					Debug.Assert(lStreamLength == sParentStream.Length);
					uint uPct = (uint)((sParentStream.Position * 100) /
						lStreamLength);

					// Clip percent value in case the stream reports incorrect
					// position/length values (M120413)
					if (uPct > 100) { Debug.Assert(false); uPct = 100; }

					if (!m_slLogger.SetProgress(uPct))
						throw new OperationCanceledException();
				}
			}

			Debug.Assert(ctx == KdbContext.Null);
			if (ctx != KdbContext.Null) throw new FormatException();

			Debug.Assert(m_ctxGroups.Count == 0);
			if (m_ctxGroups.Count != 0) throw new FormatException();
		}

		private KdbContext ReadXmlElement(KdbContext ctx, XmlReader xr)
		{
			Debug.Assert(xr.NodeType == XmlNodeType.Element);

			switch (ctx)
			{
				case KdbContext.Null:
					if (xr.Name == ElemDocNode)
						return SwitchContext(ctx, KdbContext.KeePassFile, xr);
					else ReadUnknown(xr);
					break;

				case KdbContext.KeePassFile:
					if (xr.Name == ElemMeta)
						return SwitchContext(ctx, KdbContext.Meta, xr);
					else if (xr.Name == ElemRoot)
						return SwitchContext(ctx, KdbContext.Root, xr);
					else ReadUnknown(xr);
					break;

				case KdbContext.Meta:
					if (xr.Name == ElemGenerator)
						ReadString(xr); // Ignore
					else if (xr.Name == ElemHeaderHash)
					{
						// The header hash is typically only stored in
						// KDBX <= 3.1 files, not in KDBX >= 4 files
						// (here, the header is verified via a HMAC),
						// but we also support it for KDBX >= 4 files
						// (i.e. if it's present, we check it)

						string strHash = ReadString(xr);
						if (!string.IsNullOrEmpty(strHash) && (m_pbHashOfHeader != null) &&
							!m_bRepairMode)
						{
							Debug.Assert(m_uFileVersion < FileVersion32_4);

							byte[] pbHash = Convert.FromBase64String(strHash);
							if (!MemUtil.ArraysEqual(pbHash, m_pbHashOfHeader))
								throw new InvalidDataException(KLRes.FileCorrupted);
						}
					}
					else if (xr.Name == ElemSettingsChanged)
						m_pwDatabase.SettingsChanged = ReadTime(xr);
					else if (xr.Name == ElemDbName)
						m_pwDatabase.Name = ReadString(xr);
					else if (xr.Name == ElemDbNameChanged)
						m_pwDatabase.NameChanged = ReadTime(xr);
					else if (xr.Name == ElemDbDesc)
						m_pwDatabase.Description = ReadString(xr);
					else if (xr.Name == ElemDbDescChanged)
						m_pwDatabase.DescriptionChanged = ReadTime(xr);
					else if (xr.Name == ElemDbDefaultUser)
						m_pwDatabase.DefaultUserName = ReadString(xr);
					else if (xr.Name == ElemDbDefaultUserChanged)
						m_pwDatabase.DefaultUserNameChanged = ReadTime(xr);
					else if (xr.Name == ElemDbMntncHistoryDays)
						m_pwDatabase.MaintenanceHistoryDays = ReadUInt(xr, 365);
					else if (xr.Name == ElemDbColor)
					{
						string strColor = ReadString(xr);
						if (!string.IsNullOrEmpty(strColor))
							m_pwDatabase.Color = ColorTranslator.FromHtml(strColor);
					}
					else if (xr.Name == ElemDbKeyChanged)
						m_pwDatabase.MasterKeyChanged = ReadTime(xr);
					else if (xr.Name == ElemDbKeyChangeRec)
						m_pwDatabase.MasterKeyChangeRec = ReadLong(xr, -1);
					else if (xr.Name == ElemDbKeyChangeForce)
						m_pwDatabase.MasterKeyChangeForce = ReadLong(xr, -1);
					else if (xr.Name == ElemDbKeyChangeForceOnce)
						m_pwDatabase.MasterKeyChangeForceOnce = ReadBool(xr, false);
					else if (xr.Name == ElemMemoryProt)
						return SwitchContext(ctx, KdbContext.MemoryProtection, xr);
					else if (xr.Name == ElemCustomIcons)
						return SwitchContext(ctx, KdbContext.CustomIcons, xr);
					else if (xr.Name == ElemRecycleBinEnabled)
						m_pwDatabase.RecycleBinEnabled = ReadBool(xr, true);
					else if (xr.Name == ElemRecycleBinUuid)
						m_pwDatabase.RecycleBinUuid = ReadUuid(xr);
					else if (xr.Name == ElemRecycleBinChanged)
						m_pwDatabase.RecycleBinChanged = ReadTime(xr);
					else if (xr.Name == ElemEntryTemplatesGroup)
						m_pwDatabase.EntryTemplatesGroup = ReadUuid(xr);
					else if (xr.Name == ElemEntryTemplatesGroupChanged)
						m_pwDatabase.EntryTemplatesGroupChanged = ReadTime(xr);
					else if (xr.Name == ElemHistoryMaxItems)
						m_pwDatabase.HistoryMaxItems = ReadInt(xr, -1);
					else if (xr.Name == ElemHistoryMaxSize)
						m_pwDatabase.HistoryMaxSize = ReadLong(xr, -1);
					else if (xr.Name == ElemLastSelectedGroup)
						m_pwDatabase.LastSelectedGroup = ReadUuid(xr);
					else if (xr.Name == ElemLastTopVisibleGroup)
						m_pwDatabase.LastTopVisibleGroup = ReadUuid(xr);
					else if (xr.Name == ElemBinaries)
						return SwitchContext(ctx, KdbContext.Binaries, xr);
					else if (xr.Name == ElemCustomData)
						return SwitchContext(ctx, KdbContext.CustomData, xr);
					else ReadUnknown(xr);
					break;

				case KdbContext.MemoryProtection:
					if (xr.Name == ElemProtTitle)
						m_pwDatabase.MemoryProtection.ProtectTitle = ReadBool(xr, false);
					else if (xr.Name == ElemProtUserName)
						m_pwDatabase.MemoryProtection.ProtectUserName = ReadBool(xr, false);
					else if (xr.Name == ElemProtPassword)
						m_pwDatabase.MemoryProtection.ProtectPassword = ReadBool(xr, true);
					else if (xr.Name == ElemProtUrl)
						m_pwDatabase.MemoryProtection.ProtectUrl = ReadBool(xr, false);
					else if (xr.Name == ElemProtNotes)
						m_pwDatabase.MemoryProtection.ProtectNotes = ReadBool(xr, false);
					// else if(xr.Name == ElemProtAutoHide)
					//	m_pwDatabase.MemoryProtection.AutoEnableVisualHiding = ReadBool(xr, true);
					else ReadUnknown(xr);
					break;

				case KdbContext.CustomIcons:
					if (xr.Name == ElemCustomIconItem)
						return SwitchContext(ctx, KdbContext.CustomIcon, xr);
					else ReadUnknown(xr);
					break;

				case KdbContext.CustomIcon:
					if (xr.Name == ElemCustomIconItemID)
						m_uuidCustomIconID = ReadUuid(xr);
					else if (xr.Name == ElemCustomIconItemData)
					{
						string strData = ReadString(xr);
						if (!string.IsNullOrEmpty(strData))
							m_pbCustomIconData = Convert.FromBase64String(strData);
						else { Debug.Assert(false); }
					}
					else if (xr.Name == ElemName)
						m_strCustomIconName = ReadString(xr);
					else if (xr.Name == ElemLastModTime)
						m_odtCustomIconLastMod = ReadTime(xr);
					else ReadUnknown(xr);
					break;

				case KdbContext.Binaries:
					if (xr.Name == ElemBinary)
					{
						if (xr.MoveToAttribute(AttrId))
						{
							string strKey = xr.Value;
							ProtectedBinary pbData = ReadProtectedBinary(xr);

							int iKey;
							if (!StrUtil.TryParseIntInvariant(strKey, out iKey))
								throw new FormatException();
							if (iKey < 0) throw new FormatException();

							Debug.Assert(m_pbsBinaries.Get(iKey) == null);
							Debug.Assert(m_pbsBinaries.Find(pbData) < 0);
							m_pbsBinaries.Set(iKey, pbData);
						}
						else ReadUnknown(xr);
					}
					else ReadUnknown(xr);
					break;

				case KdbContext.CustomData:
					if (xr.Name == ElemStringDictExItem)
						return SwitchContext(ctx, KdbContext.CustomDataItem, xr);
					else ReadUnknown(xr);
					break;

				case KdbContext.CustomDataItem:
					if (xr.Name == ElemKey)
						m_strCustomDataKey = ReadString(xr);
					else if (xr.Name == ElemValue)
						m_strCustomDataValue = ReadString(xr);
					else if (xr.Name == ElemLastModTime)
						m_odtCustomDataLastMod = ReadTime(xr);
					else ReadUnknown(xr);
					break;

				case KdbContext.Root:
					if (xr.Name == ElemGroup)
					{
						Debug.Assert(m_ctxGroups.Count == 0);
						if (m_ctxGroups.Count != 0) throw new FormatException();

						m_pwDatabase.RootGroup = new PwGroup(false, false);
						m_ctxGroups.Push(m_pwDatabase.RootGroup);
						m_ctxGroup = m_ctxGroups.Peek();

						return SwitchContext(ctx, KdbContext.Group, xr);
					}
					else if (xr.Name == ElemDeletedObjects)
						return SwitchContext(ctx, KdbContext.RootDeletedObjects, xr);
					else ReadUnknown(xr);
					break;

				case KdbContext.Group:
					if (xr.Name == ElemUuid)
						m_ctxGroup.Uuid = ReadUuid(xr);
					else if (xr.Name == ElemName)
						m_ctxGroup.Name = ReadString(xr);
					else if (xr.Name == ElemNotes)
						m_ctxGroup.Notes = ReadString(xr);
					else if (xr.Name == ElemIcon)
						m_ctxGroup.IconId = ReadIconId(xr, PwIcon.Folder);
					else if (xr.Name == ElemCustomIconID)
						m_ctxGroup.CustomIconUuid = ReadUuid(xr);
					else if (xr.Name == ElemTimes)
						return SwitchContext(ctx, KdbContext.GroupTimes, xr);
					else if (xr.Name == ElemIsExpanded)
						m_ctxGroup.IsExpanded = ReadBool(xr, true);
					else if (xr.Name == ElemGroupDefaultAutoTypeSeq)
						m_ctxGroup.DefaultAutoTypeSequence = ReadString(xr);
					else if (xr.Name == ElemEnableAutoType)
						m_ctxGroup.EnableAutoType = StrUtil.StringToBoolEx(ReadString(xr));
					else if (xr.Name == ElemEnableSearching)
						m_ctxGroup.EnableSearching = StrUtil.StringToBoolEx(ReadString(xr));
					else if (xr.Name == ElemLastTopVisibleEntry)
						m_ctxGroup.LastTopVisibleEntry = ReadUuid(xr);
					else if (xr.Name == ElemPreviousParentGroup)
						m_ctxGroup.PreviousParentGroup = ReadUuid(xr);
					else if (xr.Name == ElemTags)
						m_ctxGroup.Tags = StrUtil.StringToTags(ReadString(xr));
					else if (xr.Name == ElemCustomData)
						return SwitchContext(ctx, KdbContext.GroupCustomData, xr);
					else if (xr.Name == ElemGroup)
					{
						m_ctxGroup = new PwGroup(false, false);
						m_ctxGroups.Peek().AddGroup(m_ctxGroup, true);

						m_ctxGroups.Push(m_ctxGroup);

						return SwitchContext(ctx, KdbContext.Group, xr);
					}
					else if (xr.Name == ElemEntry)
					{
						m_ctxEntry = new PwEntry(false, false);
						m_ctxGroup.AddEntry(m_ctxEntry, true);

						m_bEntryInHistory = false;
						return SwitchContext(ctx, KdbContext.Entry, xr);
					}
					else ReadUnknown(xr);
					break;

				case KdbContext.GroupCustomData:
					if (xr.Name == ElemStringDictExItem)
						return SwitchContext(ctx, KdbContext.GroupCustomDataItem, xr);
					else ReadUnknown(xr);
					break;

				case KdbContext.GroupCustomDataItem:
					if (xr.Name == ElemKey)
						m_strGroupCustomDataKey = ReadString(xr);
					else if (xr.Name == ElemValue)
						m_strGroupCustomDataValue = ReadString(xr);
					else ReadUnknown(xr);
					break;

				case KdbContext.Entry:
					if (xr.Name == ElemUuid)
						m_ctxEntry.Uuid = ReadUuid(xr);
					else if (xr.Name == ElemIcon)
						m_ctxEntry.IconId = ReadIconId(xr, PwIcon.Key);
					else if (xr.Name == ElemCustomIconID)
						m_ctxEntry.CustomIconUuid = ReadUuid(xr);
					else if (xr.Name == ElemFgColor)
					{
						string strColor = ReadString(xr);
						if (!string.IsNullOrEmpty(strColor))
							m_ctxEntry.ForegroundColor = ColorTranslator.FromHtml(strColor);
					}
					else if (xr.Name == ElemBgColor)
					{
						string strColor = ReadString(xr);
						if (!string.IsNullOrEmpty(strColor))
							m_ctxEntry.BackgroundColor = ColorTranslator.FromHtml(strColor);
					}
					else if (xr.Name == ElemOverrideUrl)
						m_ctxEntry.OverrideUrl = ReadString(xr);
					else if (xr.Name == ElemQualityCheck)
						m_ctxEntry.QualityCheck = ReadBool(xr, true);
					else if (xr.Name == ElemTags)
						m_ctxEntry.Tags = StrUtil.StringToTags(ReadString(xr));
					else if (xr.Name == ElemPreviousParentGroup)
						m_ctxEntry.PreviousParentGroup = ReadUuid(xr);
					else if (xr.Name == ElemTimes)
						return SwitchContext(ctx, KdbContext.EntryTimes, xr);
					else if (xr.Name == ElemString)
						return SwitchContext(ctx, KdbContext.EntryString, xr);
					else if (xr.Name == ElemBinary)
						return SwitchContext(ctx, KdbContext.EntryBinary, xr);
					else if (xr.Name == ElemAutoType)
						return SwitchContext(ctx, KdbContext.EntryAutoType, xr);
					else if (xr.Name == ElemCustomData)
						return SwitchContext(ctx, KdbContext.EntryCustomData, xr);
					else if (xr.Name == ElemHistory)
					{
						Debug.Assert(m_bEntryInHistory == false);

						if (m_bEntryInHistory == false)
						{
							m_ctxHistoryBase = m_ctxEntry;
							return SwitchContext(ctx, KdbContext.EntryHistory, xr);
						}
						else ReadUnknown(xr);
					}
					else ReadUnknown(xr);
					break;

				case KdbContext.GroupTimes:
				case KdbContext.EntryTimes:
					ITimeLogger tl = ((ctx == KdbContext.GroupTimes) ?
						(ITimeLogger)m_ctxGroup : (ITimeLogger)m_ctxEntry);
					Debug.Assert(tl != null);

					if (xr.Name == ElemCreationTime)
						tl.CreationTime = ReadTime(xr);
					else if (xr.Name == ElemLastModTime)
						tl.LastModificationTime = ReadTime(xr);
					else if (xr.Name == ElemLastAccessTime)
						tl.LastAccessTime = ReadTime(xr);
					else if (xr.Name == ElemExpiryTime)
						tl.ExpiryTime = ReadTime(xr);
					else if (xr.Name == ElemExpires)
						tl.Expires = ReadBool(xr, false);
					else if (xr.Name == ElemUsageCount)
						tl.UsageCount = ReadULong(xr, 0);
					else if (xr.Name == ElemLocationChanged)
						tl.LocationChanged = ReadTime(xr);
					else ReadUnknown(xr);
					break;

				case KdbContext.EntryString:
					if (xr.Name == ElemKey)
						m_ctxStringName = ReadString(xr);
					else if (xr.Name == ElemValue)
						m_ctxStringValue = ReadProtectedString(xr);
					else ReadUnknown(xr);
					break;

				case KdbContext.EntryBinary:
					if (xr.Name == ElemKey)
						m_ctxBinaryName = ReadString(xr);
					else if (xr.Name == ElemValue)
						m_ctxBinaryValue = ReadProtectedBinary(xr);
					else ReadUnknown(xr);
					break;

				case KdbContext.EntryAutoType:
					if (xr.Name == ElemAutoTypeEnabled)
						m_ctxEntry.AutoType.Enabled = ReadBool(xr, true);
					else if (xr.Name == ElemAutoTypeObfuscation)
						m_ctxEntry.AutoType.ObfuscationOptions =
							(AutoTypeObfuscationOptions)ReadInt(xr, 0);
					else if (xr.Name == ElemAutoTypeDefaultSeq)
						m_ctxEntry.AutoType.DefaultSequence = ReadString(xr);
					else if (xr.Name == ElemAutoTypeItem)
						return SwitchContext(ctx, KdbContext.EntryAutoTypeItem, xr);
					else ReadUnknown(xr);
					break;

				case KdbContext.EntryAutoTypeItem:
					if (xr.Name == ElemWindow)
						m_ctxATName = ReadString(xr);
					else if (xr.Name == ElemKeystrokeSequence)
						m_ctxATSeq = ReadString(xr);
					else ReadUnknown(xr);
					break;

				case KdbContext.EntryCustomData:
					if (xr.Name == ElemStringDictExItem)
						return SwitchContext(ctx, KdbContext.EntryCustomDataItem, xr);
					else ReadUnknown(xr);
					break;

				case KdbContext.EntryCustomDataItem:
					if (xr.Name == ElemKey)
						m_strEntryCustomDataKey = ReadString(xr);
					else if (xr.Name == ElemValue)
						m_strEntryCustomDataValue = ReadString(xr);
					else ReadUnknown(xr);
					break;

				case KdbContext.EntryHistory:
					if (xr.Name == ElemEntry)
					{
						m_ctxEntry = new PwEntry(false, false);
						m_ctxHistoryBase.History.Add(m_ctxEntry);

						m_bEntryInHistory = true;
						return SwitchContext(ctx, KdbContext.Entry, xr);
					}
					else ReadUnknown(xr);
					break;

				case KdbContext.RootDeletedObjects:
					if (xr.Name == ElemDeletedObject)
					{
						m_ctxDeletedObject = new PwDeletedObject();
						m_pwDatabase.DeletedObjects.Add(m_ctxDeletedObject);

						return SwitchContext(ctx, KdbContext.DeletedObject, xr);
					}
					else ReadUnknown(xr);
					break;

				case KdbContext.DeletedObject:
					if (xr.Name == ElemUuid)
						m_ctxDeletedObject.Uuid = ReadUuid(xr);
					else if (xr.Name == ElemDeletionTime)
						m_ctxDeletedObject.DeletionTime = ReadTime(xr);
					else ReadUnknown(xr);
					break;

				default:
					ReadUnknown(xr);
					break;
			}

			return ctx;
		}

		private KdbContext EndXmlElement(KdbContext ctx, XmlReader xr)
		{
			Debug.Assert(xr.NodeType == XmlNodeType.EndElement);

			if ((ctx == KdbContext.KeePassFile) && (xr.Name == ElemDocNode))
				return KdbContext.Null;
			else if ((ctx == KdbContext.Meta) && (xr.Name == ElemMeta))
				return KdbContext.KeePassFile;
			else if ((ctx == KdbContext.Root) && (xr.Name == ElemRoot))
				return KdbContext.KeePassFile;
			else if ((ctx == KdbContext.MemoryProtection) && (xr.Name == ElemMemoryProt))
				return KdbContext.Meta;
			else if ((ctx == KdbContext.CustomIcons) && (xr.Name == ElemCustomIcons))
				return KdbContext.Meta;
			else if ((ctx == KdbContext.CustomIcon) && (xr.Name == ElemCustomIconItem))
			{
				if (!m_uuidCustomIconID.Equals(PwUuid.Zero) &&
					(m_pbCustomIconData != null))
				{
					PwCustomIcon ci = new PwCustomIcon(m_uuidCustomIconID,
						m_pbCustomIconData);
					if (m_strCustomIconName != null) ci.Name = m_strCustomIconName;
					ci.LastModificationTime = m_odtCustomIconLastMod;
					m_pwDatabase.CustomIcons.Add(ci);
				}
				else { Debug.Assert(false); }

				m_uuidCustomIconID = PwUuid.Zero;
				m_pbCustomIconData = null;
				m_strCustomIconName = null;
				m_odtCustomIconLastMod = null;

				return KdbContext.CustomIcons;
			}
			else if ((ctx == KdbContext.Binaries) && (xr.Name == ElemBinaries))
				return KdbContext.Meta;
			else if ((ctx == KdbContext.CustomData) && (xr.Name == ElemCustomData))
				return KdbContext.Meta;
			else if ((ctx == KdbContext.CustomDataItem) && (xr.Name == ElemStringDictExItem))
			{
				if ((m_strCustomDataKey != null) && (m_strCustomDataValue != null))
					m_pwDatabase.CustomData.Set(m_strCustomDataKey,
						m_strCustomDataValue, m_odtCustomDataLastMod);
				else { Debug.Assert(false); }

				m_strCustomDataKey = null;
				m_strCustomDataValue = null;
				m_odtCustomDataLastMod = null;

				return KdbContext.CustomData;
			}
			else if ((ctx == KdbContext.Group) && (xr.Name == ElemGroup))
			{
				if (PwUuid.Zero.Equals(m_ctxGroup.Uuid))
					m_ctxGroup.Uuid = new PwUuid(true); // No assert (import)

				m_ctxGroups.Pop();

				if (m_ctxGroups.Count == 0)
				{
					m_ctxGroup = null;
					return KdbContext.Root;
				}
				else
				{
					m_ctxGroup = m_ctxGroups.Peek();
					return KdbContext.Group;
				}
			}
			else if ((ctx == KdbContext.GroupTimes) && (xr.Name == ElemTimes))
				return KdbContext.Group;
			else if ((ctx == KdbContext.GroupCustomData) && (xr.Name == ElemCustomData))
				return KdbContext.Group;
			else if ((ctx == KdbContext.GroupCustomDataItem) && (xr.Name == ElemStringDictExItem))
			{
				if ((m_strGroupCustomDataKey != null) && (m_strGroupCustomDataValue != null))
					m_ctxGroup.CustomData.Set(m_strGroupCustomDataKey, m_strGroupCustomDataValue);
				else { Debug.Assert(false); }

				m_strGroupCustomDataKey = null;
				m_strGroupCustomDataValue = null;

				return KdbContext.GroupCustomData;
			}
			else if ((ctx == KdbContext.Entry) && (xr.Name == ElemEntry))
			{
				// Create new UUID if absent
				if (PwUuid.Zero.Equals(m_ctxEntry.Uuid))
					m_ctxEntry.Uuid = new PwUuid(true); // No assert (import)

				if (m_bEntryInHistory)
				{
					m_ctxEntry = m_ctxHistoryBase;
					return KdbContext.EntryHistory;
				}

				return KdbContext.Group;
			}
			else if ((ctx == KdbContext.EntryTimes) && (xr.Name == ElemTimes))
				return KdbContext.Entry;
			else if ((ctx == KdbContext.EntryString) && (xr.Name == ElemString))
			{
				m_ctxEntry.Strings.Set(m_ctxStringName, m_ctxStringValue);
				m_ctxStringName = null;
				m_ctxStringValue = null;
				return KdbContext.Entry;
			}
			else if ((ctx == KdbContext.EntryBinary) && (xr.Name == ElemBinary))
			{
				if (string.IsNullOrEmpty(m_strDetachBins))
					m_ctxEntry.Binaries.Set(m_ctxBinaryName, m_ctxBinaryValue);
				else
				{
					SaveBinary(m_ctxBinaryName, m_ctxBinaryValue, m_strDetachBins);

					m_ctxBinaryValue = null;
					GC.Collect();
				}

				m_ctxBinaryName = null;
				m_ctxBinaryValue = null;
				return KdbContext.Entry;
			}
			else if ((ctx == KdbContext.EntryAutoType) && (xr.Name == ElemAutoType))
				return KdbContext.Entry;
			else if ((ctx == KdbContext.EntryAutoTypeItem) && (xr.Name == ElemAutoTypeItem))
			{
				AutoTypeAssociation atAssoc = new AutoTypeAssociation(m_ctxATName,
					m_ctxATSeq);
				m_ctxEntry.AutoType.Add(atAssoc);
				m_ctxATName = null;
				m_ctxATSeq = null;
				return KdbContext.EntryAutoType;
			}
			else if ((ctx == KdbContext.EntryCustomData) && (xr.Name == ElemCustomData))
				return KdbContext.Entry;
			else if ((ctx == KdbContext.EntryCustomDataItem) && (xr.Name == ElemStringDictExItem))
			{
				if ((m_strEntryCustomDataKey != null) && (m_strEntryCustomDataValue != null))
					m_ctxEntry.CustomData.Set(m_strEntryCustomDataKey, m_strEntryCustomDataValue);
				else { Debug.Assert(false); }

				m_strEntryCustomDataKey = null;
				m_strEntryCustomDataValue = null;

				return KdbContext.EntryCustomData;
			}
			else if ((ctx == KdbContext.EntryHistory) && (xr.Name == ElemHistory))
			{
				m_bEntryInHistory = false;
				return KdbContext.Entry;
			}
			else if ((ctx == KdbContext.RootDeletedObjects) && (xr.Name == ElemDeletedObjects))
				return KdbContext.Root;
			else if ((ctx == KdbContext.DeletedObject) && (xr.Name == ElemDeletedObject))
			{
				m_ctxDeletedObject = null;
				return KdbContext.RootDeletedObjects;
			}
			else
			{
				Debug.Assert(false);
				throw new FormatException();
			}
		}

		private string ReadString(XmlReader xr)
		{
			XorredBuffer xb = ProcessNode(xr);
			if (xb != null)
			{
				Debug.Assert(false); // Protected data is unexpected here
				try
				{
					byte[] pb = xb.ReadPlainText();
					if (pb.Length == 0) return string.Empty;
					try { return StrUtil.Utf8.GetString(pb, 0, pb.Length); }
					finally { MemUtil.ZeroByteArray(pb); }
				}
				finally { xb.Dispose(); }
			}

			m_bReadNextNode = false; // ReadElementString skips end tag
			return xr.ReadElementString();
		}

		private string ReadStringRaw(XmlReader xr)
		{
			m_bReadNextNode = false; // ReadElementString skips end tag
			return xr.ReadElementString();
		}

		private byte[] ReadBase64(XmlReader xr, bool bRaw)
		{
			// if(bRaw) return ReadBase64RawInChunks(xr);

			string str = (bRaw ? ReadStringRaw(xr) : ReadString(xr));
			if (string.IsNullOrEmpty(str)) return MemUtil.EmptyByteArray;

			return Convert.FromBase64String(str);
		}

		/* private byte[] m_pbBase64ReadBuf = new byte[1024 * 1024 * 3];
		private byte[] ReadBase64RawInChunks(XmlReader xr)
		{
			xr.MoveToContent();

			List<byte[]> lParts = new List<byte[]>();
			byte[] pbBuf = m_pbBase64ReadBuf;
			while(true)
			{
				int cb = xr.ReadElementContentAsBase64(pbBuf, 0, pbBuf.Length);
				if(cb == 0) break;

				byte[] pb = new byte[cb];
				Array.Copy(pbBuf, 0, pb, 0, cb);
				lParts.Add(pb);

				// No break when cb < pbBuf.Length, because ReadElementContentAsBase64
				// moves to the next XML node only when returning 0
			}
			m_bReadNextNode = false;

			if(lParts.Count == 0) return MemUtil.EmptyByteArray;
			if(lParts.Count == 1) return lParts[0];

			long cbRes = 0;
			for(int i = 0; i < lParts.Count; ++i)
				cbRes += lParts[i].Length;

			byte[] pbRes = new byte[cbRes];
			int cbCur = 0;
			for(int i = 0; i < lParts.Count; ++i)
			{
				Array.Copy(lParts[i], 0, pbRes, cbCur, lParts[i].Length);
				cbCur += lParts[i].Length;
			}

			return pbRes;
		} */

		private bool ReadBool(XmlReader xr, bool bDefault)
		{
			string str = ReadString(xr);
			if (str == ValTrue) return true;
			else if (str == ValFalse) return false;

			Debug.Assert(false);
			return bDefault;
		}

		private PwUuid ReadUuid(XmlReader xr)
		{
			byte[] pb = ReadBase64(xr, false);
			if (pb.Length == 0) return PwUuid.Zero;
			return new PwUuid(pb);
		}

		private int ReadInt(XmlReader xr, int nDefault)
		{
			string str = ReadString(xr);

			int n;
			if (StrUtil.TryParseIntInvariant(str, out n)) return n;

			// Backward compatibility
			if (StrUtil.TryParseInt(str, out n)) return n;

			Debug.Assert(false);
			return nDefault;
		}

		private uint ReadUInt(XmlReader xr, uint uDefault)
		{
			string str = ReadString(xr);

			uint u;
			if (StrUtil.TryParseUIntInvariant(str, out u)) return u;

			// Backward compatibility
			if (StrUtil.TryParseUInt(str, out u)) return u;

			Debug.Assert(false);
			return uDefault;
		}

		private long ReadLong(XmlReader xr, long lDefault)
		{
			string str = ReadString(xr);

			long l;
			if (StrUtil.TryParseLongInvariant(str, out l)) return l;

			// Backward compatibility
			if (StrUtil.TryParseLong(str, out l)) return l;

			Debug.Assert(false);
			return lDefault;
		}

		private ulong ReadULong(XmlReader xr, ulong uDefault)
		{
			string str = ReadString(xr);

			ulong u;
			if (StrUtil.TryParseULongInvariant(str, out u)) return u;

			// Backward compatibility
			if (StrUtil.TryParseULong(str, out u)) return u;

			Debug.Assert(false);
			return uDefault;
		}

		private DateTime ReadTime(XmlReader xr)
		{
			// Cf. WriteObject(string, DateTime)
			if ((m_format == KdbxFormat.Default) && (m_uFileVersion >= FileVersion32_4))
			{
				// long l = ReadLong(xr, -1);
				// if(l != -1) return DateTime.FromBinary(l);

				byte[] pb = ReadBase64(xr, false);
				if (pb.Length != 8)
				{
					Debug.Assert(false);
					byte[] pb8 = new byte[8];
					Array.Copy(pb, pb8, Math.Min(pb.Length, 8)); // Little-endian
					pb = pb8;
				}
				long lSec = MemUtil.BytesToInt64(pb);
                try
                {
                    return new DateTime(lSec * TimeSpan.TicksPerSecond, DateTimeKind.Utc);
                }
                catch (System.ArgumentOutOfRangeException e)
                {
                    //files might contain bad data, e.g. see #868. Fall back to MinValue
                    Kp2aLog.Log("Failed to read date from file.");
                    return DateTime.MinValue;
                }
            }
			else
			{
				string str = ReadString(xr);

				DateTime dt;
				if (TimeUtil.TryDeserializeUtc(str, out dt)) return dt;
			}

			Debug.Assert(false);
			return m_dtNow;
		}

		private PwIcon ReadIconId(XmlReader xr, PwIcon icDefault)
		{
			int i = ReadInt(xr, (int)icDefault);
			if ((i >= 0) && (i < (int)PwIcon.Count)) return (PwIcon)i;

			Debug.Assert(false);
			return icDefault;
		}

		private ProtectedString ReadProtectedString(XmlReader xr)
		{
			XorredBuffer xb = ProcessNode(xr);
			if (xb != null)
			{
				try { return new ProtectedString(true, xb); }
				finally { xb.Dispose(); }
			}

			bool bProtect = false;
			if (m_format == KdbxFormat.PlainXml)
			{
				if (xr.MoveToAttribute(AttrProtectedInMemPlainXml))
				{
					string strProtect = xr.Value;
					bProtect = ((strProtect != null) && (strProtect == ValTrue));
				}
			}

			return new ProtectedString(bProtect, ReadString(xr));
		}

		private ProtectedBinary ReadProtectedBinary(XmlReader xr)
		{
			if (xr.MoveToAttribute(AttrRef))
			{
				string strRef = xr.Value;
				if (!string.IsNullOrEmpty(strRef))
				{
					int iRef;
					if (StrUtil.TryParseIntInvariant(strRef, out iRef))
					{
						ProtectedBinary pb = m_pbsBinaries.Get(iRef);
						if (pb != null)
						{
							// https://sourceforge.net/p/keepass/feature-requests/2023/
							xr.MoveToElement();
#if DEBUG
							string strInner = ReadStringRaw(xr);
							Debug.Assert(string.IsNullOrEmpty(strInner));
#else
							ReadStringRaw(xr);
#endif

							return pb;
						}
						else { Debug.Assert(false); }
					}
					else { Debug.Assert(false); }
				}
				else { Debug.Assert(false); }
			}

			bool bCompressed = false;
			if (xr.MoveToAttribute(AttrCompressed))
				bCompressed = (xr.Value == ValTrue);

			XorredBuffer xb = ProcessNode(xr);
			if (xb != null)
			{
				Debug.Assert(!bCompressed); // See SubWriteValue(ProtectedBinary value)
				try { return new ProtectedBinary(true, xb); }
				finally { xb.Dispose(); }
			}

			byte[] pbData = ReadBase64(xr, true);
			if (pbData.Length == 0) return new ProtectedBinary();

			if (bCompressed) pbData = MemUtil.Decompress(pbData);
			return new ProtectedBinary(false, pbData);
		}

		private void ReadUnknown(XmlReader xr)
		{
			Debug.Assert(false); // Unknown node!
			Debug.Assert(xr.NodeType == XmlNodeType.Element);

			bool bRead = false;
			int cOpen = 0;

			do
			{
				if (bRead) xr.Read();
				bRead = true;

				if (xr.NodeType == XmlNodeType.EndElement) --cOpen;
				else if (xr.NodeType == XmlNodeType.Element)
				{
					if (!xr.IsEmptyElement)
					{
						XorredBuffer xb = ProcessNode(xr);
						if (xb != null) { xb.Dispose(); bRead = m_bReadNextNode; continue; }

						++cOpen;
					}
				}
			}
			while (cOpen > 0);

			m_bReadNextNode = bRead;
		}

		private XorredBuffer ProcessNode(XmlReader xr)
		{
			// Debug.Assert(xr.NodeType == XmlNodeType.Element);

			if (xr.HasAttributes)
			{
				if (xr.MoveToAttribute(AttrProtected))
				{
					if (xr.Value == ValTrue)
					{
						xr.MoveToElement();

						byte[] pbCT = ReadBase64(xr, true);
						byte[] pbPad = m_randomStream.GetRandomBytes((uint)pbCT.Length);

						return new XorredBuffer(pbCT, pbPad);
					}
				}
			}

			return null;
		}

		private static KdbContext SwitchContext(KdbContext ctxCurrent,
			KdbContext ctxNew, XmlReader xr)
		{
			if (xr.IsEmptyElement) return ctxCurrent;
			return ctxNew;
		}
	}
}
