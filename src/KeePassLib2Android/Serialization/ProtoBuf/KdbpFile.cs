using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using KeePassLib.Collections;
using KeePassLib.Cryptography;
using KeePassLib.Security;
using KeePassLib.Utility;
using ProtoBuf;
using ProtoBuf.Meta;

namespace KeePassLib.Serialization
{
	public class KdbpFile
	{
		public const string FileNameExtension = "kdbp";

		/// <summary>
		/// Determines whether the database pointed to by the specified ioc should be (de)serialised in default (xml) or protocol buffers format.
		/// </summary>
		public static KdbxFormat GetFormatToUse(IOConnectionInfo ioc)
		{
			// If the filename ends in .kdbp, use ProtocolBuffers format.
			return UrlUtil.GetExtension(UrlUtil.GetFileName(ioc.Path)).Equals(KdbpFile.FileNameExtension, StringComparison.OrdinalIgnoreCase) ? KdbxFormat.ProtocolBuffers : KdbxFormat.Default;
		}

		public static void WriteDocument(PwDatabase database, Stream stream, byte[] protectedStreamKey, byte[] hashOfHeader)
		{
			var context = new SerializationContext 
			{
				Context = new BufferContext(database,
					new CryptoRandomStream(CrsAlgorithm.Salsa20, protectedStreamKey), hashOfHeader) 
			};

			RuntimeTypeModel.Default.Serialize(stream, new PwDatabaseBuffer(database), context);
		}

		public static void ReadDocument(PwDatabase database, Stream stream, byte[] protectedStreamKey, byte[] expectedHashOfHeader)
		{

			var context = new BufferContext(database, new CryptoRandomStream(CrsAlgorithm.Salsa20, protectedStreamKey));
				
			// Deserialisation will occur into the database already in context.
			RuntimeTypeModel.Default.Deserialize(stream, null, typeof(PwDatabaseBuffer), new SerializationContext { Context = context });

			if (expectedHashOfHeader.Length > 0 &&
				!KeePassLib.Utility.MemUtil.ArraysEqual(context.HeaderHash, expectedHashOfHeader))
			{
				throw new IOException(KeePassLib.Resources.KLRes.FileCorrupted);
			}
		}

		private class BufferContext
		{
			// ProtectedBinary objects may be referred to multipe times by entry histories, so reference them only once by ensuring a 1:1 mapping to the buffer wrapping them.
			public readonly Dictionary<ProtectedBinary, NamedProtectedBinaryBuffer> BinaryPool = new Dictionary<ProtectedBinary, NamedProtectedBinaryBuffer>();

			public readonly PwDatabase Database;
			public readonly CryptoRandomStream RandomStream;
			public byte[] HeaderHash;

			public BufferContext(PwDatabase database, CryptoRandomStream randomStream, byte[] pbHashOfHeader = null)
			{
				Database = database;
				RandomStream = randomStream;
				HeaderHash = pbHashOfHeader;
			}
		}

		[ProtoContract]
		private class PwDatabaseBuffer
		{
			#region Serialization

			private PwDatabase mDatabase;
			private PwDeletedObjectListBuffer mDeletedObjects;
			private PwCustomIconListBuffer mCustomIcons;

			public PwDatabaseBuffer(PwDatabase database)
			{
				mDatabase = database;
				mDeletedObjects = new PwDeletedObjectListBuffer(mDatabase.DeletedObjects);
				mCustomIcons = new PwCustomIconListBuffer(mDatabase.CustomIcons);
			}

			[ProtoBeforeSerialization]
			private void BeforeSerialization(SerializationContext context)
			{
				var bufferContext = (BufferContext)context.Context;

				System.Diagnostics.Debug.Assert(mDatabase == bufferContext.Database);

				HeaderHash = bufferContext.HeaderHash;
			}
			#endregion

			#region Deserialization
			public PwDatabaseBuffer()
			{
			}

			[ProtoBeforeDeserialization]
			private void BeforeDeserialization(SerializationContext context)
			{
				var bufferContext = (BufferContext)context.Context;

				mDatabase = bufferContext.Database;
				mDeletedObjects = new PwDeletedObjectListBuffer(mDatabase.DeletedObjects);
				mCustomIcons = new PwCustomIconListBuffer(mDatabase.CustomIcons);
			}

			[ProtoAfterDeserialization]
			private void AfterDeserialization(SerializationContext context)
			{
				var bufferContext = (BufferContext)context.Context;
				
				bufferContext.HeaderHash = HeaderHash;
			}
			#endregion

			[ProtoMember(1)]
			public string Generator
			{
				get { return PwDatabase.LocalizedAppName; }
				set { /* Ignore */ }
			}

			[ProtoMember(2, OverwriteList = true)]
			public byte[] HeaderHash;

			[ProtoMember(3)]
			public string Name
			{
				get { return mDatabase.Name; }
				set { mDatabase.Name = value; }
			}

			[ProtoMember(4)]
			public DateTime NameChanged
			{
				get { return mDatabase.NameChanged; }
				set { mDatabase.NameChanged = value; }
			}

			[ProtoMember(5)]
			public string Description
			{
				get { return mDatabase.Description; }
				set { mDatabase.Description = value; }
			}

			[ProtoMember(6)]
			public DateTime DescriptionChanged
			{
				get { return mDatabase.DescriptionChanged; }
				set { mDatabase.DescriptionChanged = value; }
			}

			[ProtoMember(7)]
			public string DefaultUserName
			{
				get { return mDatabase.DefaultUserName; }
				set { mDatabase.DefaultUserName = value; }
			}

			[ProtoMember(8)]
			public DateTime DefaultUserNameChanged
			{
				get { return mDatabase.DefaultUserNameChanged; }
				set { mDatabase.DefaultUserNameChanged = value; }
			}

			[ProtoMember(9)]
			public uint MaintenanceHistoryDays
			{
				get { return mDatabase.MaintenanceHistoryDays; }
				set { mDatabase.MaintenanceHistoryDays = value; }
			}

			[ProtoMember(10)]
			public int Color
			{
				get { return mDatabase.Color.ToArgb(); }
				set { mDatabase.Color = System.Drawing.Color.FromArgb(value); }
			}

			[ProtoMember(11)]
			public DateTime MasterKeyChanged
			{
				get { return mDatabase.MasterKeyChanged; }
				set { mDatabase.MasterKeyChanged = value; }
			}

			[ProtoMember(12)]
			public long MasterKeyChangeRec
			{
				get { return mDatabase.MasterKeyChangeRec; }
				set { mDatabase.MasterKeyChangeRec = value; }
			}

			[ProtoMember(13)]
			public long MasterKeyChangeForce
			{
				get { return mDatabase.MasterKeyChangeForce; }
				set { mDatabase.MasterKeyChangeForce = value; }
			}

			[ProtoMember(14)]
			public MemoryProtectionConfigBuffer MemoryProtection
			{
				get { return new MemoryProtectionConfigBuffer(mDatabase.MemoryProtection); }
				set { mDatabase.MemoryProtection = value.MemoryProtectionConfig; }
			}

			[ProtoMember(15)]
			public PwCustomIconListBuffer CustomIcons
			{
				get { return mCustomIcons; }
			}

			[ProtoMember(16)]
			public bool RecycleBinEnabled
			{
				get { return mDatabase.RecycleBinEnabled; }
				set { mDatabase.RecycleBinEnabled = value; }
			}

			[ProtoMember(17, OverwriteList = true)]
			public byte[] RecycleBinUuid
			{
				get { return mDatabase.RecycleBinUuid.UuidBytes; }
				set { mDatabase.RecycleBinUuid = new PwUuid(value); }
			}

			[ProtoMember(18)]
			public DateTime RecycleBinChanged
			{
				get { return mDatabase.RecycleBinChanged; }
				set { mDatabase.RecycleBinChanged = value; }
			}

			[ProtoMember(19, OverwriteList = true)]
			public byte[] EntryTemplatesGroup
			{
				get { return mDatabase.EntryTemplatesGroup.UuidBytes; }
				set { mDatabase.EntryTemplatesGroup = new PwUuid(value); }
			}

			[ProtoMember(20)]
			public DateTime EntryTemplatesGroupChanged
			{
				get { return mDatabase.EntryTemplatesGroupChanged; }
				set { mDatabase.EntryTemplatesGroupChanged = value; }
			}

			[ProtoMember(21)]
			public int HistoryMaxItems
			{
				get { return mDatabase.HistoryMaxItems; }
				set { mDatabase.HistoryMaxItems = value; }
			}

			[ProtoMember(22)]
			public long HistoryMaxSize
			{
				get { return mDatabase.HistoryMaxSize; }
				set { mDatabase.HistoryMaxSize = value; }
			}

			[ProtoMember(23, OverwriteList = true)]
			public byte[] LastSelectedGroup
			{
				get { return mDatabase.LastSelectedGroup.UuidBytes; }
				set { mDatabase.LastSelectedGroup = new PwUuid(value); }
			}

			[ProtoMember(24, OverwriteList = true)]
			public byte[] LastTopVisibleGroup
			{
				get { return mDatabase.LastTopVisibleGroup.UuidBytes; }
				set { mDatabase.LastTopVisibleGroup = new PwUuid(value); }
			}

			[ProtoMember(25)]
			public StringDictionaryExBuffer CustomData
			{
				get { return new StringDictionaryExBuffer(mDatabase.CustomData); }
				set { mDatabase.CustomData = value.StringDictionaryEx; }
			}
			
			[ProtoMember(27)]
			public PwGroupBuffer RootGroup
			{
				get { return new PwGroupBuffer(mDatabase.RootGroup); }
				set { mDatabase.RootGroup = value.Group; }
			}

			[ProtoMember(28)]
			public PwDeletedObjectListBuffer DeletedObjects
			{
				get { return mDeletedObjects; }
			}
		}

		[ProtoContract]
		private class StringDictionaryExBuffer : IEnumerable<KeyValuePair<String, String>>
		{
			#region Serialization
			private StringDictionaryEx mStringDictionaryEx;

			public StringDictionaryExBuffer(StringDictionaryEx stringDictionaryEx)
			{
				mStringDictionaryEx = stringDictionaryEx;
			}
			#endregion

			#region Deserialization
			public StringDictionaryExBuffer()
			{
				mStringDictionaryEx = new StringDictionaryEx();
			}

			public StringDictionaryEx StringDictionaryEx { get { return mStringDictionaryEx; } }

			public void Add(KeyValuePair<String, String> kvp)
			{
				mStringDictionaryEx.Set(kvp.Key, kvp.Value);
			}
			#endregion

			public IEnumerator<KeyValuePair<String, String>> GetEnumerator()
			{
				return mStringDictionaryEx.GetEnumerator();
			}

			System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
			{
				return GetEnumerator();
			}
		}

		[ProtoContract]
		private class PwCustomIconListBuffer : IEnumerable<PwCustomIconBuffer>
		{
			private List<PwCustomIcon> mCustomIcons;
			#region Serialization
			public PwCustomIconListBuffer(List<PwCustomIcon> customIcons)
			{
				mCustomIcons = customIcons;
			}
			#endregion

			#region Deserialization
			public void Add(PwCustomIconBuffer item)
			{
				mCustomIcons.Add(item.CustomIcon);
			}
			#endregion

			public IEnumerator<PwCustomIconBuffer> GetEnumerator()
			{
				foreach (var customIcon in mCustomIcons)
				{
					yield return new PwCustomIconBuffer(customIcon);
				}
			}

			System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
			{
				return GetEnumerator();
			}
		}

		[ProtoContract]
		private class PwCustomIconBuffer
		{
			#region Serialization
			private PwCustomIcon mCustomIcon;
			public PwCustomIconBuffer(PwCustomIcon CustomIcon)
			{
				mCustomIcon = CustomIcon;
				Uuid = mCustomIcon.Uuid.UuidBytes;
				ImageData = mCustomIcon.ImageDataPng;
			}
			#endregion

			#region Deserialization
			public PwCustomIconBuffer()
			{
			}

			[ProtoAfterDeserialization]
			private void AfterDeserialization(SerializationContext context)
			{
				mCustomIcon = new PwCustomIcon(new PwUuid(Uuid), ImageData);
			}

			public PwCustomIcon CustomIcon { get { return mCustomIcon; } }
			#endregion

			[ProtoMember(1, OverwriteList = true)]
			public byte[] Uuid;

			[ProtoMember(2, OverwriteList = true)]
			public byte[] ImageData;
		}

		[ProtoContract]
		private class MemoryProtectionConfigBuffer
		{
			#region Serialization
			private readonly MemoryProtectionConfig mMemoryProtectionConfig;

			public MemoryProtectionConfigBuffer(MemoryProtectionConfig memoryProtectionConfig)
			{
				mMemoryProtectionConfig = memoryProtectionConfig;
			}
			#endregion

			#region Deserialization
			public MemoryProtectionConfigBuffer()
			{
				mMemoryProtectionConfig = new MemoryProtectionConfig();
			}

			public MemoryProtectionConfig MemoryProtectionConfig { get { return mMemoryProtectionConfig; } }
			#endregion

			[ProtoMember(1)]
			public bool ProtectTitle
			{
				get { return mMemoryProtectionConfig.ProtectTitle; }
				set { mMemoryProtectionConfig.ProtectTitle = value; }
			}

			[ProtoMember(2)]
			public bool ProtectUserName
			{
				get { return mMemoryProtectionConfig.ProtectUserName; }
				set { mMemoryProtectionConfig.ProtectUserName = value; }
			}

			[ProtoMember(3)]
			public bool ProtectPassword
			{
				get { return mMemoryProtectionConfig.ProtectPassword; }
				set { mMemoryProtectionConfig.ProtectPassword = value; }
			}

			[ProtoMember(4)]
			public bool ProtectUrl
			{
				get { return mMemoryProtectionConfig.ProtectUrl; }
				set { mMemoryProtectionConfig.ProtectUrl = value; }
			}

			[ProtoMember(5)]
			public bool ProtectNotes
			{
				get { return mMemoryProtectionConfig.ProtectNotes; }
				set { mMemoryProtectionConfig.ProtectNotes = value; }
			}
		}

		[ProtoContract]
		private class PwDeletedObjectListBuffer : PwObjectListBufferBase<PwDeletedObject, PwDeletedObjectBuffer>
		{
			#region Serialization
			public PwDeletedObjectListBuffer(PwObjectList<PwDeletedObject> objectList)
				: base(objectList)
			{
			}

			protected override PwDeletedObjectBuffer CreateBuffer(PwDeletedObject item)
			{
				return new PwDeletedObjectBuffer(item);
			}
			#endregion

			#region Deserialization
			public PwDeletedObjectListBuffer()
				: base()
			{
			}

			public override void Add(PwDeletedObjectBuffer item)
			{
				ObjectList.Add(item.DeletedObject);
			}
			#endregion
		}

		[ProtoContract]
		private class PwDeletedObjectBuffer
		{
			#region Serialization
			private readonly PwDeletedObject mDeletedObject;

			public PwDeletedObjectBuffer(PwDeletedObject deletedObject)
			{
				mDeletedObject = deletedObject;
			}
			#endregion

			#region Deserialization
			public PwDeletedObjectBuffer()
			{
				mDeletedObject = new PwDeletedObject();
			}

			public PwDeletedObject DeletedObject { get { return mDeletedObject; } }
			#endregion

			[ProtoMember(1, OverwriteList = true)]
			public byte[] Uuid
			{
				get { return mDeletedObject.Uuid.UuidBytes; }
				set { mDeletedObject.Uuid = new PwUuid(value); }
			}

			[ProtoMember(2)]
			public DateTime DeletionTime
			{
				get { return mDeletedObject.DeletionTime; }
				set { mDeletedObject.DeletionTime = value; }
			}
		}

		[ProtoContract]
		private class PwGroupBuffer
		{
			#region Serialization
			private readonly PwGroup mGroup;
			private readonly PwGroupEntryListBuffer mEntries;
			private readonly PwGroupGroupListBuffer mGroups;

			public PwGroupBuffer(PwGroup group)
			{
				mGroup = group;
				mEntries = new PwGroupEntryListBuffer(mGroup);
				mGroups = new PwGroupGroupListBuffer(mGroup);
			}
			#endregion

			#region Deserialization
			public PwGroupBuffer()
			{
				mGroup = new PwGroup(false, false);
				mEntries = new PwGroupEntryListBuffer(mGroup);
				mGroups = new PwGroupGroupListBuffer(mGroup);
			}

			public PwGroup Group { get { return mGroup; } }
			#endregion

			[ProtoMember(1, OverwriteList = true)]
			public byte[] Uuid
			{
				get { return mGroup.Uuid.UuidBytes; }
				set { mGroup.Uuid = new PwUuid(value); }
			}

			[ProtoMember(2)]
			public string Name
			{
				get { return mGroup.Name; }
				set { mGroup.Name = value; }
			}

			[ProtoMember(3)]
			public string Notes
			{
				get { return mGroup.Notes; }
				set { mGroup.Notes = value; }
			}

			[ProtoMember(4)]
			public PwIcon IconId
			{
				get { return mGroup.IconId; }
				set { mGroup.IconId = value; }
			}

			[ProtoMember(5, OverwriteList = true)]
			public byte[] CustomIconUuid
			{
				get { return mGroup.CustomIconUuid.UuidBytes; }
				set { mGroup.CustomIconUuid = new PwUuid(value); }
			}

			[ProtoMember(6)]
			public bool IsExpanded
			{
				get { return mGroup.IsExpanded; }
				set { mGroup.IsExpanded = value; }
			}

			[ProtoMember(7)]
			public string DefaultAutoTypeSequence
			{
				get { return mGroup.DefaultAutoTypeSequence; }
				set { mGroup.DefaultAutoTypeSequence = value; }
			}

			[ProtoMember(8)]
			public DateTime LastModificationTime
			{
				get { return mGroup.LastModificationTime; }
				set { mGroup.LastModificationTime = value; }
			}

			[ProtoMember(9)]
			public DateTime CreationTime
			{
				get { return mGroup.CreationTime; }
				set { mGroup.CreationTime = value; }
			}

			[ProtoMember(10)]
			public DateTime LastAccessTime
			{
				get { return mGroup.LastAccessTime; }
				set { mGroup.LastAccessTime = value; }
			}

			[ProtoMember(11)]
			public DateTime ExpiryTime
			{
				get { return mGroup.ExpiryTime; }
				set { mGroup.ExpiryTime = value; }
			}

			[ProtoMember(12)]
			public bool Expires
			{
				get { return mGroup.Expires; }
				set { mGroup.Expires = value; }
			}

			[ProtoMember(13)]
			public ulong UsageCount
			{
				get { return mGroup.UsageCount; }
				set { mGroup.UsageCount = value; }
			}

			[ProtoMember(14)]
			public DateTime LocationChanged
			{
				get { return mGroup.LocationChanged; }
				set { mGroup.LocationChanged = value; }
			}

			[ProtoMember(15)]
			public bool? EnableAutoType
			{
				get { return mGroup.EnableAutoType; }
				set { mGroup.EnableAutoType = value; }
			}

			[ProtoMember(16)]
			public bool? EnableSearching
			{
				get { return mGroup.EnableSearching; }
				set { mGroup.EnableSearching = value; }
			}

			[ProtoMember(17, OverwriteList = true)]
			public byte[] LastTopVisibleEntry
			{
				get { return mGroup.LastTopVisibleEntry.UuidBytes; }
				set { mGroup.LastTopVisibleEntry = new PwUuid(value); }
			}

			[ProtoMember(18)]
			public PwGroupGroupListBuffer Groups
			{
				get { return mGroups; }
			}

			[ProtoMember(19)]
			public PwGroupEntryListBuffer Entries
			{
				get { return mEntries; }
			}
		}

		private abstract class PwObjectListBufferBase<TData, TDataBuffer> : IEnumerable<TDataBuffer>
			where TData : class, KeePassLib.Interfaces.IDeepCloneable<TData>
		{
			#region Serialization
			private PwObjectList<TData> mObjectList;

			protected PwObjectListBufferBase(PwObjectList<TData> objectList)
			{
				mObjectList = objectList;
			}

			protected abstract TDataBuffer CreateBuffer(TData item);
			#endregion

			#region Deserialization
			protected PwObjectListBufferBase()
			{
				mObjectList = new PwObjectList<TData>();
			}

			public PwObjectList<TData> ObjectList { get { return mObjectList; } }

			public abstract void Add(TDataBuffer item);
			#endregion

			public IEnumerator<TDataBuffer> GetEnumerator()
			{
				foreach (var item in mObjectList)
				{
					yield return CreateBuffer(item);
				}
			}

			System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
			{
				return GetEnumerator();
			}
		}

		[ProtoContract]
		private class PwGroupGroupListBuffer : PwObjectListBufferBase<PwGroup, PwGroupBuffer>
		{
			#region Serialization
			private PwGroup mGroup;
			public PwGroupGroupListBuffer(PwGroup group)
				: base(group.Groups)
			{
				mGroup = group;
			}

			protected override PwGroupBuffer CreateBuffer(PwGroup item)
			{
				return new PwGroupBuffer(item);
			}
			#endregion

			#region Deserialization
			public override void Add(PwGroupBuffer item)
			{
				mGroup.AddGroup(item.Group, true);
			}
			#endregion
		}

		[ProtoContract]
		private class PwGroupEntryListBuffer : PwObjectListBufferBase<PwEntry, PwEntryBuffer>
		{
			#region Serialization
			private PwGroup mGroup;
			public PwGroupEntryListBuffer(PwGroup group)
				: base(group.Entries)
			{
				mGroup = group;
			}

			protected override PwEntryBuffer CreateBuffer(PwEntry item)
			{
				return new PwEntryBuffer(item);
			}
			#endregion

			#region Deserialization
			public override void Add(PwEntryBuffer item)
			{
				mGroup.AddEntry(item.Entry, true);
			}
			#endregion
		}

		[ProtoContract]
		private class PwEntryListBuffer : PwObjectListBufferBase<PwEntry, PwEntryBuffer>
		{
			#region Serialization
			public PwEntryListBuffer(PwObjectList<PwEntry> entryList)
				: base(entryList)
			{
			}

			protected override PwEntryBuffer CreateBuffer(PwEntry item)
			{
				return new PwEntryBuffer(item);
			}
			#endregion

			#region Deserialization
			public PwEntryListBuffer()
				: base()
			{
			}

			public override void Add(PwEntryBuffer item)
			{
				ObjectList.Add(item.Entry);
			}
			#endregion
		}

		[ProtoContract]
		private class PwEntryBuffer
		{
			#region Serialization
			private readonly PwEntry mEntry;
			private ProtectedStandardFieldDictionaryBuffer mEntryStandardStrings;
			private ProtectedCustomFieldDictionaryBuffer mEntryCustomStrings;
			private NamedProtectedBinaryListBuffer mEntryBinaries;

			public PwEntryBuffer(PwEntry entry)
			{
				mEntry = entry;
			}

			[ProtoBeforeSerialization]
			private void BeforeSerialization(SerializationContext context)
			{
				var bufferContext = (BufferContext)context.Context;

				// ProtectedStringDictionaryBuffer nver gets its own ProtoBeforeSerialization called as it's a list of objects rather than an object itself
				List<KeyValuePair<String, ProtectedString>> customFields;
				mEntryStandardStrings = new ProtectedStandardFieldDictionaryBuffer(mEntry.Strings, (int)mEntry.Strings.UCount, bufferContext, out customFields);
				mEntryCustomStrings = new ProtectedCustomFieldDictionaryBuffer(customFields);
				mEntryBinaries = new NamedProtectedBinaryListBuffer(mEntry.Binaries, (int)mEntry.Binaries.UCount, bufferContext);
			}
			#endregion

			#region Deserialization
			public PwEntryBuffer()
			{
				mEntry = new PwEntry(false, false);
				mEntryStandardStrings = new ProtectedStandardFieldDictionaryBuffer(mEntry.Strings);
				mEntryCustomStrings = new ProtectedCustomFieldDictionaryBuffer(mEntry.Strings);
				mEntryBinaries = new NamedProtectedBinaryListBuffer(mEntry.Binaries);
			}

			public PwEntry Entry { get { return mEntry; } }
			#endregion

			[ProtoMember(1, OverwriteList = true)]
			public byte[] Uuid
			{
				get { return mEntry.Uuid.UuidBytes; }
				set { mEntry.SetUuid(new PwUuid(value), false); }
			}

			[ProtoMember(2)]
			public PwIcon IconId
			{
				get { return mEntry.IconId; }
				set { mEntry.IconId = value; }
			}

			[ProtoMember(3, OverwriteList = true)]
			public byte[] CustomIconUuid
			{
				get { return mEntry.CustomIconUuid.UuidBytes; }
				set { mEntry.CustomIconUuid = new PwUuid(value); }
			}

			[ProtoMember(4)]
			public int ForegroundColor
			{
				get { return mEntry.ForegroundColor.ToArgb(); }
				set { mEntry.ForegroundColor = Color.FromArgb(value); }
			}

			[ProtoMember(5)]
			public int BackgroundColor
			{
				get { return mEntry.BackgroundColor.ToArgb(); }
				set { mEntry.BackgroundColor = Color.FromArgb(value); }
			}

			[ProtoMember(6)]
			public string OverrideUrl
			{
				get { return mEntry.OverrideUrl; }
				set { mEntry.OverrideUrl = value; }
			}

			[ProtoMember(7)]
			public IList<String> Tags
			{
				get { return mEntry.Tags; }
			}

			[ProtoMember(8)]
			public DateTime LastModificationTime
			{
				get { return mEntry.LastModificationTime; }
				set { mEntry.LastModificationTime = value; }
			}

			[ProtoMember(9)]
			public DateTime CreationTime
			{
				get { return mEntry.CreationTime; }
				set { mEntry.CreationTime = value; }
			}

			[ProtoMember(10)]
			public DateTime LastAccessTime
			{
				get { return mEntry.LastAccessTime; }
				set { mEntry.LastAccessTime = value; }
			}

			[ProtoMember(11)]
			public DateTime ExpiryTime
			{
				get { return mEntry.ExpiryTime; }
				set { mEntry.ExpiryTime = value; }
			}

			[ProtoMember(12)]
			public bool Expires
			{
				get { return mEntry.Expires; }
				set { mEntry.Expires = value; }
			}

			[ProtoMember(13)]
			public ulong UsageCount
			{
				get { return mEntry.UsageCount; }
				set { mEntry.UsageCount = value; }
			}

			[ProtoMember(14)]
			public DateTime LocationChanged
			{
				get { return mEntry.LocationChanged; }
				set { mEntry.LocationChanged = value; }
			}

			[ProtoMember(15)]
			public ProtectedStandardFieldDictionaryBuffer StandardStrings
			{
				get { return mEntryStandardStrings; }
			}

			[ProtoMember(16)]
			public ProtectedCustomFieldDictionaryBuffer CustomStrings
			{
				get { return mEntryCustomStrings; }
			}

			[ProtoMember(17, AsReference = true)]
			public NamedProtectedBinaryListBuffer Binaries
			{
				get { return mEntryBinaries; }
			}

			[ProtoMember(18)]
			public AutoTypeConfigBuffer AutoType
			{
				get { return new AutoTypeConfigBuffer(mEntry.AutoType); }
				set { mEntry.AutoType = value.AutoTypeConfig; }
			}

			[ProtoMember(19)]
			public PwEntryListBuffer History
			{
				get { return new PwEntryListBuffer(mEntry.History); }
				set { mEntry.History = value.ObjectList; }
			}

		}

		private abstract class ProtectedStringDictionaryBuffer<TKey> : IEnumerable<KeyValuePair<TKey, ProtectedStringBuffer>>
		{
			#region Serialization
			private List<KeyValuePair<TKey, ProtectedStringBuffer>> mProtectedStringBuffers;

			/// <summary>
			/// Serialisation constructor. Reads strings from dictionary, does not write to it
			/// </summary>
			protected ProtectedStringDictionaryBuffer(int capacity)
			{
				mProtectedStringBuffers = new List<KeyValuePair<TKey, ProtectedStringBuffer>>(capacity);
			}

			protected void AddStringField(TKey key, ProtectedString value, bool? overrideProtect)
			{
				mProtectedStringBuffers.Add(new KeyValuePair<TKey, ProtectedStringBuffer>(key, new ProtectedStringBuffer(value, overrideProtect)));
			}

			public IEnumerator<KeyValuePair<TKey, ProtectedStringBuffer>> GetEnumerator()
			{
				return mProtectedStringBuffers.GetEnumerator();
			}

			System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
			{
				return GetEnumerator();
			}
			#endregion

			#region Deserialization
			private readonly ProtectedStringDictionary mDictionary;

			/// <summary>
			/// Deerialisation constructor. Writes strings to dictionary, does read from it
			/// </summary>
			protected ProtectedStringDictionaryBuffer(ProtectedStringDictionary dictionary)
			{
				mDictionary = dictionary;
			}

			public void Add(KeyValuePair<TKey, ProtectedStringBuffer> item)
			{
				mDictionary.Set(GetFieldName(item.Key), item.Value.ProtectedString);
			}

			protected abstract string GetFieldName(TKey key);

			#endregion
		}

		[ProtoContract]
		private class ProtectedCustomFieldDictionaryBuffer : ProtectedStringDictionaryBuffer<String>
		{
			public ProtectedCustomFieldDictionaryBuffer(List<KeyValuePair<String, ProtectedString>> entryStrings)
				: base(entryStrings.Count)
			{
				foreach (var kvp in entryStrings)
				{
					System.Diagnostics.Debug.Assert(!PwDefs.IsStandardField(kvp.Key));
					AddStringField(kvp.Key, kvp.Value, null);
				}
			}

			public ProtectedCustomFieldDictionaryBuffer(ProtectedStringDictionary dictionary)
				: base(dictionary)
			{ }

			protected override string GetFieldName(string key)
			{
				return key;
			}
		}

		[ProtoContract]
		private class ProtectedStandardFieldDictionaryBuffer : ProtectedStringDictionaryBuffer<ProtectedStandardFieldDictionaryBuffer.StandardField>
		{
			public enum StandardField
			{
				Title,
				UserName,
				Password,
				Url,
				Notes
			}

			public ProtectedStandardFieldDictionaryBuffer(IEnumerable<KeyValuePair<String, ProtectedString>> entryStrings, int entryStringCount, BufferContext context,
				out List<KeyValuePair<String, ProtectedString>> customFields) // Perf optimisation - return the custom fields so we don't have to determine them again
				: base(entryStringCount)
			{
				customFields = new List<KeyValuePair<string, ProtectedString>>(entryStringCount);

				var database = context.Database;

				foreach (var kvp in entryStrings)
				{
					var field = GetField(kvp.Key);

					if (field.HasValue)
					{
						// Logic from KdbxFile.Write

						bool? overrideProtect = null;
						// Adjust memory protection setting (which might be different
						// from the database default, e.g. due to an import which
						// didn't specify the correct setting)
						switch (field.Value)
						{
							case StandardField.Title:
								overrideProtect = database.MemoryProtection.ProtectTitle;
								break;
							case StandardField.UserName:
								overrideProtect = database.MemoryProtection.ProtectUserName;
								break;
							case StandardField.Password:
								overrideProtect = database.MemoryProtection.ProtectPassword;
								break;
							case StandardField.Url:
								overrideProtect = database.MemoryProtection.ProtectUrl;
								break;
							case StandardField.Notes:
								overrideProtect = database.MemoryProtection.ProtectNotes;
								break;
						}

						AddStringField(field.Value, kvp.Value, overrideProtect);
					}
					else
					{
						customFields.Add(kvp);
					}
				}
			}

			private static StandardField? GetField(string fieldName)
			{
				switch (fieldName)
				{
					case PwDefs.TitleField:
						return StandardField.Title;
					case PwDefs.UserNameField:
						return StandardField.UserName;
					case PwDefs.PasswordField:
						return StandardField.Password;
					case PwDefs.UrlField:
						return StandardField.Url;
					case PwDefs.NotesField:
						return StandardField.Notes;

					default:
						System.Diagnostics.Debug.Assert(!PwDefs.IsStandardField(fieldName));
						return null;
				}
			}

			public ProtectedStandardFieldDictionaryBuffer(ProtectedStringDictionary dictionary)
				: base(dictionary)
			{ }

			protected override string GetFieldName(StandardField key)
			{
				switch (key)
				{
					case StandardField.Title:
						return PwDefs.TitleField;
					case StandardField.UserName:
						return PwDefs.UserNameField;
					case StandardField.Password:
						return PwDefs.PasswordField;
					case StandardField.Url:
						return PwDefs.UrlField;
					case StandardField.Notes:
						return PwDefs.NotesField;
					default:
						throw new ArgumentOutOfRangeException();
				}
			}
		}

		[ProtoContract]
		private class ProtectedStringBuffer
		{
			#region Serialisation
			private ProtectedString mProtectedString;

			public ProtectedStringBuffer(ProtectedString protectedString, bool? overrideProtect)
			{
				mProtectedString = protectedString;
				IsProtected = overrideProtect.GetValueOrDefault(mProtectedString.IsProtected);
			}

			[ProtoBeforeSerialization]
			private void BeforeSerialization(SerializationContext context)
			{
				if (IsProtected)
				{
					Value = mProtectedString.ReadXorredString(((BufferContext)context.Context).RandomStream);
				}
				else
				{
					Value = mProtectedString.ReadUtf8();
				}
			}
			#endregion

			#region Deserialisation
			public ProtectedStringBuffer()
			{
			}

			[ProtoAfterDeserialization]
			private void AfterDeserialization(SerializationContext context)
			{
				if (IsProtected)
				{
					byte[] pbPad = ((BufferContext)context.Context).RandomStream.GetRandomBytes((uint)Value.Length);
					mProtectedString = new ProtectedString(IsProtected, new XorredBuffer(Value, pbPad));
				}
				else
				{
					mProtectedString = new ProtectedString(IsProtected, Value);
				}
			}

			public ProtectedString ProtectedString { get { return mProtectedString; } }

			#endregion

			[ProtoMember(1)]
			public bool IsProtected;

			[ProtoMember(2, OverwriteList = true)]
			public byte[] Value;
		}

		[ProtoContract]
		private class NamedProtectedBinaryListBuffer : IEnumerable<NamedProtectedBinaryBuffer>
		{
			#region Serialisation
			private readonly List<NamedProtectedBinaryBuffer> mNamedBinaries;

			public NamedProtectedBinaryListBuffer(IEnumerable<KeyValuePair<String, ProtectedBinary>> binaries, int binariesCount, BufferContext context)
			{
				mNamedBinaries = new List<NamedProtectedBinaryBuffer>(binariesCount);
				foreach (var kvp in binaries)
				{
					NamedProtectedBinaryBuffer namedProtectedBinaryBuffer;
					if (!context.BinaryPool.TryGetValue(kvp.Value, out namedProtectedBinaryBuffer))
					{
						// Hasn't been put in the pool yet, so create it
						namedProtectedBinaryBuffer = new NamedProtectedBinaryBuffer(kvp);
						context.BinaryPool.Add(kvp.Value, namedProtectedBinaryBuffer);
					}
					mNamedBinaries.Add(namedProtectedBinaryBuffer);
				}
			}

			public IEnumerator<NamedProtectedBinaryBuffer> GetEnumerator()
			{
				return mNamedBinaries.GetEnumerator();
			}

			System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
			{
				return GetEnumerator();
			}
			#endregion

			#region Deserialization
			private readonly ProtectedBinaryDictionary mBinaryDictionary;

			public NamedProtectedBinaryListBuffer(ProtectedBinaryDictionary binaryDictionary)
			{
				mBinaryDictionary = binaryDictionary;
			}

			public void Add(NamedProtectedBinaryBuffer item)
			{
				mBinaryDictionary.Set(item.Name, item.ProtectedBinary);
			}
			#endregion
		}

		[ProtoContract]
		private class NamedProtectedBinaryBuffer
		{
			#region Serialization
			private ProtectedBinary mProtectedBinary;
			public NamedProtectedBinaryBuffer(KeyValuePair<string, ProtectedBinary> namedBinary)
			{
				Name = namedBinary.Key;
				mProtectedBinary = namedBinary.Value;
				IsProtected = mProtectedBinary.IsProtected;
			}

			[ProtoBeforeSerialization]
			private void BeforeSerialization(SerializationContext context)
			{
				if (IsProtected)
				{
					Value = mProtectedBinary.ReadXorredData(((BufferContext)context.Context).RandomStream);
				}
				else
				{
					Value = mProtectedBinary.ReadData();
				}
			}
			#endregion

			#region Deserialisation
			public NamedProtectedBinaryBuffer()
			{
			}

			[ProtoAfterDeserialization]
			private void AfterDeserialization(SerializationContext context)
			{
				if (IsProtected)
				{
					byte[] pbPad = ((BufferContext)context.Context).RandomStream.GetRandomBytes((uint)Value.Length);
					mProtectedBinary = new ProtectedBinary(IsProtected, new XorredBuffer(Value, pbPad));
				}
				else
				{
					mProtectedBinary = new ProtectedBinary(IsProtected, Value);
				}
			}

			public ProtectedBinary ProtectedBinary { get { return mProtectedBinary; } }

			#endregion

			[ProtoMember(1)]
			public string Name;

			[ProtoMember(2)]
			public bool IsProtected;

			[ProtoMember(3, OverwriteList = true)]
			public byte[] Value;
		}

		[ProtoContract]
		private class AutoTypeConfigBuffer
		{
			private readonly AutoTypeAssociationsBuffer mAutoTypeAssociationsBuffer;
			#region Serialization
			private AutoTypeConfig mAutoTypeConfig;
			public AutoTypeConfigBuffer(AutoTypeConfig autoTypeConfig)
			{
				mAutoTypeConfig = autoTypeConfig;
				mAutoTypeAssociationsBuffer = new AutoTypeAssociationsBuffer(mAutoTypeConfig);
			}
			#endregion

			#region Deserialization
			public AutoTypeConfigBuffer()
			{
				mAutoTypeConfig = new AutoTypeConfig();
				mAutoTypeAssociationsBuffer = new AutoTypeAssociationsBuffer(mAutoTypeConfig);
			}

			public AutoTypeConfig AutoTypeConfig { get { return mAutoTypeConfig; } }
			#endregion

			[ProtoMember(1)]
			public bool Enabled
			{
				get { return mAutoTypeConfig.Enabled; }
				set { mAutoTypeConfig.Enabled = value; }
			}

			[ProtoMember(2)]
			public AutoTypeObfuscationOptions ObfuscationOptions
			{
				get { return mAutoTypeConfig.ObfuscationOptions; }
				set { mAutoTypeConfig.ObfuscationOptions = value; }
			}

			[ProtoMember(3)]
			public string DefaultSequence
			{
				get { return mAutoTypeConfig.DefaultSequence; }
				set { mAutoTypeConfig.DefaultSequence = value; }
			}

			[ProtoMember(4)]
			public AutoTypeAssociationsBuffer Associations
			{
				get { return mAutoTypeAssociationsBuffer; }
			}
		}

		[ProtoContract]
		private class AutoTypeAssociationsBuffer : IEnumerable<AutoTypeAssociationBuffer>
		{
			#region Serialization
			private AutoTypeConfig mAutoTypeConfig;

			public AutoTypeAssociationsBuffer(AutoTypeConfig autoTypeConfig)
			{
				mAutoTypeConfig = autoTypeConfig;
			}

			public IEnumerator<AutoTypeAssociationBuffer> GetEnumerator()
			{
				foreach (var autoTypeAssociation in mAutoTypeConfig.Associations)
				{
					yield return new AutoTypeAssociationBuffer(autoTypeAssociation);
				}
			}

			System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
			{
				return GetEnumerator();
			}
			#endregion

			#region Deserialization
			public void Add(AutoTypeAssociationBuffer value)
			{
				mAutoTypeConfig.Add(value.AutoTypeAssociation);
			}
			#endregion
		}

		[ProtoContract]
		private class AutoTypeAssociationBuffer
		{
			#region Serialization
			private AutoTypeAssociation mAutoTypeAssociation;

			public AutoTypeAssociationBuffer(AutoTypeAssociation autoTypeAssociation)
			{
				mAutoTypeAssociation = autoTypeAssociation;
			}
			#endregion

			#region Deserialization
			public AutoTypeAssociationBuffer()
			{
				mAutoTypeAssociation = new AutoTypeAssociation();
			}

			public AutoTypeAssociation AutoTypeAssociation { get { return mAutoTypeAssociation; } }
			#endregion

			[ProtoMember(1)]
			public string WindowName
			{
				get { return mAutoTypeAssociation.WindowName; }
				set { mAutoTypeAssociation.WindowName = value; }
			}

			[ProtoMember(2)]
			public string Sequence
			{
				get { return mAutoTypeAssociation.Sequence; }
				set { mAutoTypeAssociation.Sequence = value; }
			}
		}
	}
}
