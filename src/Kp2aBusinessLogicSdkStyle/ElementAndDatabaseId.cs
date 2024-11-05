using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Android.App;
using Android.Content;
using Android.OS;
using Android.Runtime;
using Android.Views;
using Android.Widget;
using keepass2android.Io;
using KeePassLib;
using KeePassLib.Interfaces;
using KeePassLib.Utility;

namespace keepass2android
{
    public class ElementAndDatabaseId
    {
        private const char Separator = '+';

        public ElementAndDatabaseId(Database db, IStructureItem element)
        {
            DatabaseId = db.IocAsHexString();
            ElementIdString = element.Uuid.ToHexString();
        }

        public ElementAndDatabaseId(string fullId)
        {
            string[] parts = fullId.Split(Separator);
            if (parts.Length != 2)
                throw new Exception("Invalid full id " + fullId);
            DatabaseId = parts[0];
            ElementIdString = parts[1];
        }

        public string DatabaseId { get; set; }
        public string ElementIdString { get; set; }
        public PwUuid ElementId {  get {  return new PwUuid(MemUtil.HexStringToByteArray(ElementIdString));} }

        public string FullId
        {
            get { return DatabaseId + Separator + ElementIdString; }
        }
    }
}