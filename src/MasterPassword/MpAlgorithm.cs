using System;
using System.Linq;
using System.Text;
using CryptSharp.Utility;
using MasterPassword.Data;


namespace MasterPassword
{
	public partial class MpAlgorithm
    {
	    
		static readonly PList PlistData = new PList(plist);
		private const int MP_N         = 32768;
		private const int MP_r         = 8;
		private const int MP_p         = 2;
		private const int MP_dkLen = 64;

		static sbyte[] ToSignedByteArray(byte[] unsigned)
		{
			sbyte[] signed = new sbyte[unsigned.Length];
			Buffer.BlockCopy(unsigned, 0, signed, 0, unsigned.Length);
			return signed;
		}
		static byte[] ToUnsignedByteArray(sbyte[] signed)
		{
			byte[] unsigned = new byte[signed.Length];
			Buffer.BlockCopy(signed, 0, unsigned, 0, signed.Length);
			return unsigned;
		}

		public static byte[] GetKeyForPassword(string user, string password)
		{
			var salt = Combine(Encoding.UTF8.GetBytes("com.lyndir.masterpassword"),
			        IntAsByteArray(user.Length),
			        Encoding.UTF8.GetBytes(user));
			var ssalt = ToSignedByteArray(salt);
			var spwd = ToSignedByteArray(Encoding.UTF8.GetBytes(password));
			var key = SCrypt.ComputeDerivedKey(Encoding.UTF8.GetBytes(password),
			                         salt,
									 MP_N,
									 MP_r, 
									 MP_p,null, MP_dkLen);

			sbyte[] signed = ToSignedByteArray(key);
			return signed;
		}


		static byte[] Combine(params byte[][] arrays)
		{
			byte[] ret = new byte[arrays.Sum(x => x.Length)];
			int offset = 0;
			foreach (byte[] data in arrays)
			{
				Buffer.BlockCopy(data, 0, ret, offset, data.Length);
				offset += data.Length;
			}
			return ret;
		}
		static byte[] IntAsByteArray(int intValue)
		{
			byte[] intBytes = BitConverter.GetBytes(intValue);
			if (BitConverter.IsLittleEndian)
				Array.Reverse(intBytes);
			return intBytes;
		}

		public static String GenerateContent(string elementType, string siteName, sbyte[] key, int counter, Func<byte[], byte[], byte[]> hmacFunc)
		{
			if (counter == 0)
			{
				throw new Exception("counter==0 not support in .net. ");
				//TODO: what does this line do in Java?
				//counter = (int) (System.currentTimeMillis() / (300 * 1000)) * 300;
			}
				

			byte[] nameLengthBytes = IntAsByteArray(siteName.Length);
			byte[] counterBytes =IntAsByteArray(counter);
			
			sbyte[] seed = ToSignedByteArray(hmacFunc( ToUnsignedByteArray(key), Combine( Encoding.UTF8.GetBytes("com.lyndir.masterpassword"), //
														nameLengthBytes, //
														Encoding.UTF8.GetBytes(siteName), //
														counterBytes ) ) );
			//logger.trc( "seed is: %s", CryptUtils.encodeBase64( seed ) );

			//Preconditions.checkState( seed.length > 0 );
			int templateIndex = seed[0] & 0xFF; // Mask the integer's sign.
			//MPTemplate template = templates.getTemplateForTypeAtRollingIndex( type, templateIndex );
			//MPTemplate template = null;

			

			var templatesLongPwd = PlistData["MPElementGeneratedEntity"]["Long Password"];
			string template = templatesLongPwd[templateIndex%templatesLongPwd.Count];
			//logger.trc( "type: %s, template: %s", type, template );

			StringBuilder password = new StringBuilder( template.Length );
			for (int i = 0; i < template.Length; ++i) {
				int characterIndex = seed[i + 1] & 0xFF; // Mask the integer's sign.

				char c = template[i];

				PList listOfCharacterSets = PlistData["MPCharacterClasses"];
				var listOfCharacters = listOfCharacterSets.Single(kvp => { return kvp.Key == c.ToString(); }).Value;

				char passwordCharacter = listOfCharacters[characterIndex % listOfCharacters.Length];
				/*logger.trc( "class: %s, index: %d, byte: 0x%02X, chosen password character: %s", characterClass, characterIndex, seed[i + 1],
							passwordCharacter );
				*/
				password.Append(passwordCharacter);
			}

			return password.ToString();
		}


    }
}
