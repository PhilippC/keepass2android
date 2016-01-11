//
//  ChallengeInfo.cs
//
//  Author:
//       Ben.Rush <>
//
//  Copyright (c) 2014 Ben.Rush
//
//  This program is free software; you can redistribute it and/or modify
//  it under the terms of the GNU General Public License as published by
//  the Free Software Foundation; either version 2 of the License, or
//  (at your option) any later version.
//
//  This program is distributed in the hope that it will be useful,
//  but WITHOUT ANY WARRANTY; without even the implied warranty of
//  MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
//  GNU General Public License for more details.
//
//  You should have received a copy of the GNU General Public License
//  along with this program; if not, write to the Free Software
//  Foundation, Inc., 59 Temple Place, Suite 330, Boston, MA 02111-1307 USA
//
using System;
using System.Xml;
using System.IO;
using keepass2android;
using KeePassLib.Serialization;
using System.Xml.Serialization;

namespace KeeChallenge
{
	public class ChallengeInfo
	{
        private bool m_LT64;

		public byte[] EncryptedSecret {
			get;
			private set;
		}

		public byte[] IV {
			get;
			private set;
		}

		public byte[] Challenge {
			get;
			private set;
		}

		public byte[] Verification {
			get;
			private set;
		}

        public bool LT64
        {
            get { return m_LT64;  }
            private set { m_LT64 = value; }
        }

        private ChallengeInfo()
        {
            LT64 = false;
        }

		public ChallengeInfo(byte[] encryptedSecret, byte[] iv, byte[] challenge, byte[] verification, bool lt64)
		{
			EncryptedSecret = encryptedSecret;
			IV = iv;
			Challenge = challenge;
			Verification = verification;
            LT64 = lt64;
		}

		public static ChallengeInfo Load(IOConnectionInfo ioc)
		{
			Stream sIn = null;
            ChallengeInfo inf = new ChallengeInfo();
			try
			{
				sIn = App.Kp2a.GetOtpAuxFileStorage(ioc).OpenFileForRead(ioc);

				XmlSerializer xs = new XmlSerializer(typeof (ChallengeInfo));
                if (!inf.LoadStream(sIn)) return null;
			}
			catch (Exception e)
			{
				Kp2aLog.LogUnexpectedError(e);
			}
			finally
			{
				if(sIn != null) sIn.Close();
			}

			return inf;
		}

		private bool LoadStream(Stream AuxFile)
		{
			//read file
			XmlReader xml;
			try
			{
				XmlReaderSettings settings = new XmlReaderSettings();
				settings.CloseInput = true;                
				xml = XmlReader.Create(AuxFile,settings);
			}
			catch (Exception) 
			{
				return false;
			}

			try
			{
				while (xml.Read())
				{
					if (xml.IsStartElement())
					{
						switch (xml.Name)
						{
						case "encrypted":
							xml.Read();
							EncryptedSecret = Convert.FromBase64String(xml.Value.Trim());
							break;
						case "iv":
							xml.Read();
							IV = Convert.FromBase64String(xml.Value.Trim());
							break;
						case "challenge":
							xml.Read();
							Challenge = Convert.FromBase64String(xml.Value.Trim());
							break;
						case "verification":
							xml.Read();
							Verification = Convert.FromBase64String(xml.Value.Trim());
							break;
                        case "lt64":
                            xml.Read();
                            if (!bool.TryParse(xml.Value.Trim(), out m_LT64)) throw new Exception("Unable to parse LT64 flag");
                            break;
						}
					}
				}
			}
			catch (Exception)
			{
				return false;
			}

			xml.Close();
			//if failed, return false
			return true;
		}

		public bool Save(IOConnectionInfo ioc)
		{
			Stream sOut = null;
			try
			{
				using (var trans = App.Kp2a.GetOtpAuxFileStorage(ioc)
					.OpenWriteTransaction(ioc, App.Kp2a.GetBooleanPreference(PreferenceKey.UseFileTransactions)))
				{
					sOut = trans.OpenFile();
					if (SaveStream(sOut))
					{
						trans.CommitWrite();
					}
					else return false;
				}
				return true;
			}
			catch(Exception) { return false; }
			finally
			{
				if (sOut != null) sOut.Close();
			}
		}

		private bool SaveStream(Stream file)
		{
			try
			{
				XmlWriterSettings settings = new XmlWriterSettings();
				settings.CloseOutput = true;
				settings.Indent = true;
				settings.IndentChars = "\t";
				settings.NewLineOnAttributes = true;                

				XmlWriter xml = XmlWriter.Create(file,settings);
				xml.WriteStartDocument();
				xml.WriteStartElement("data");

				xml.WriteStartElement("aes");
					xml.WriteElementString("encrypted", Convert.ToBase64String(EncryptedSecret));
				xml.WriteElementString("iv", Convert.ToBase64String(IV));
				xml.WriteEndElement();

				xml.WriteElementString("challenge", Convert.ToBase64String(Challenge));
				xml.WriteElementString("verification", Convert.ToBase64String(Verification));
                xml.WriteElementString("lt64", LT64.ToString());

				xml.WriteEndElement();
				xml.WriteEndDocument();
				xml.Close();                
			}
			catch (Exception)
			{
				return false;
			}

			return true;
		}

	}
}

