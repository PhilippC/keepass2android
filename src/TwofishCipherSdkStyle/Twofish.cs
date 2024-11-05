/*
  A C# implementation of the Twofish cipher
  By Shaun Wilde

  An article on integrating a C# implementation of the Twofish cipher into the
  .NET framework.
 
  http://www.codeproject.com/KB/recipes/twofish_csharp.aspx
  
  The Code Project Open License (CPOL) 1.02
  http://www.codeproject.com/info/cpol10.aspx
  
  Download a copy of the CPOL.
  http://www.codeproject.com/info/CPOL.zip
*/

using System;
using System.Diagnostics;
using System.Security.Cryptography;

namespace TwofishCipher.Crypto
{
	/// <summary>
	/// Summary description for Twofish encryption algorithm of which more information can be found at http://www.counterpane.com/twofish.html. 
	/// This is based on the MS cryptographic framework and can therefore be used in place of the RijndaelManaged classes
	/// provided by MS in System.Security.Cryptography and the other related classes
	/// </summary>
	public sealed class Twofish : SymmetricAlgorithm
	{
		/// <summary>
		/// This is the Twofish constructor.
		/// </summary>
		public Twofish()
		{
			this.LegalKeySizesValue = new KeySizes[]{new KeySizes(128,256,64)}; // this allows us to have 128,192,256 key sizes

			this.LegalBlockSizesValue = new KeySizes[]{new KeySizes(128,128,0)}; // this is in bits - typical of MS - always 16 bytes

			this.BlockSize = 128; // set this to 16 bytes we cannot have any other value
			this.KeySize = 128; // in bits - this can be changed to 128,192,256

			this.Padding = PaddingMode.Zeros; 

			this.Mode = CipherMode.ECB;

		}

		/// <summary>
		/// Creates an object that supports ICryptoTransform that can be used to encrypt data using the Twofish encryption algorithm.
		/// </summary>
		/// <param name="key">A byte array that contains a key. The length of this key should be equal to the KeySize property</param>
		/// <param name="iv">A byte array that contains an initialization vector. The length of this IV should be equal to the BlockSize property</param>
		public override ICryptoTransform CreateEncryptor(byte[] key, byte[] iv)
		{
			Key = key; // this appears to make a new copy

			if (Mode == CipherMode.CBC)
				IV = iv;
			
			return new TwofishEncryption(KeySize, ref KeyValue, ref IVValue, ModeValue, TwofishBase.EncryptionDirection.Encrypting);
		}

		/// <summary>
		/// Creates an object that supports ICryptoTransform that can be used to decrypt data using the Twofish encryption algorithm.
		/// </summary>
		/// <param name="key">A byte array that contains a key. The length of this key should be equal to the KeySize property</param>
		/// <param name="iv">A byte array that contains an initialization vector. The length of this IV should be equal to the BlockSize property</param>
		public override ICryptoTransform CreateDecryptor(byte[] key, byte[] iv)
		{
			Key = key;

			if (Mode == CipherMode.CBC)
				IV = iv;

			return new TwofishEncryption(KeySize, ref KeyValue, ref IVValue, ModeValue, TwofishBase.EncryptionDirection.Decrypting);
		}

		/// <summary>
		/// Generates a random initialization Vector (IV). 
		/// </summary>
		public override void GenerateIV()
		{
			IV = new byte[16]{0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0};
		}

		/// <summary>
		/// Generates a random Key. This is only really useful in testing scenarios.
		/// </summary>
		public override void GenerateKey()
		{
			Key = new byte[KeySize/8];

			// set the array to all 0 - implement a random key generation mechanism later probably based on PRNG
			for (int i=Key.GetLowerBound(0);i<Key.GetUpperBound(0);i++)
			{
				Key[i]=0;
			}
		}

		/// <summary>
		/// Override the Set method on this property so that we only support CBC and EBC
		/// </summary>
		public override CipherMode Mode
		{
			set
			{
				switch (value)
				{
					case CipherMode.CBC:
						break;
					case CipherMode.ECB:
						break;
					default:
						throw (new CryptographicException("Specified CipherMode is not supported."));
				}
				this.ModeValue = value;
			}
		}

	}
}
