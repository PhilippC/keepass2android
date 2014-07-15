using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml;
using System.Xml.Linq;

namespace MasterPassword.Data
{
	public class PList : Dictionary<string, dynamic>
	{
		public PList()
		{
		}

		public PList(string data)
		{
			Read(data);
		}

		public void Read(string data)
		{
			Clear();

			//XDocument doc = XDocument.Load(file);
			XDocument doc = XDocument.Load(XmlReader.Create(new StringReader(data)));
			XElement plist = doc.Element("plist");
			XElement dict = plist.Element("dict");

			var dictElements = dict.Elements();
			Parse(this, dictElements);
		}

		private void Parse(PList dict, IEnumerable<XElement> elements)
		{
			for (int i = 0; i < elements.Count(); i += 2)
			{
				XElement key = elements.ElementAt(i);
				XElement val = elements.ElementAt(i + 1);

				dict[key.Value] = ParseValue(val);
			}
		}

		private List<dynamic> ParseArray(IEnumerable<XElement> elements)
		{
			List<dynamic> list = new List<dynamic>();
			foreach (XElement e in elements)
			{
				dynamic one = ParseValue(e);
				list.Add(one);
			}

			return list;
		}

		private dynamic ParseValue(XElement val)
		{
			switch (val.Name.ToString())
			{
				case "string":
					return val.Value;
				case "integer":
					return int.Parse(val.Value);
				case "real":
					return float.Parse(val.Value);
				case "true":
					return true;
				case "false":
					return false;
				case "dict":
					PList plist = new PList();
					Parse(plist, val.Elements());
					return plist;
				case "array":
					List<dynamic> list = ParseArray(val.Elements());
					return list;
				default:
					throw new ArgumentException("Unsupported");
			}
		}
	}
}