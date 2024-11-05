/* KeeChallenge--Provides Yubikey challenge-response capability to Keepass
*  Copyright (C) 2014  Ben Rush
*  
*  This program is free software; you can redistribute it and/or
*  modify it under the terms of the GNU General Public License
*  as published by the Free Software Foundation; either version 2
*  of the License, or (at your option) any later version.
*  
*  This program is distributed in the hope that it will be useful,
*  but WITHOUT ANY WARRANTY; without even the implied warranty of
*  MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
*  GNU General Public License for more details.
*  
*  You should have received a copy of the GNU General Public License
*  along with this program; if not, write to the Free Software
*  Foundation, Inc., 51 Franklin Street, Fifth Floor, Boston, MA  02110-1301, USA.
*/

using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Security.Cryptography;
using System.Diagnostics;
using System.Xml;

using KeePassLib.Keys;
using KeePassLib.Utility;
using KeePassLib.Cryptography;
using KeePassLib.Cryptography.Cipher;
using KeePassLib.Serialization;

using keepass2android;
using keepass2android.Io;

namespace KeeChallenge
{
    public sealed class KeeChallengeProv 
	{
        private const string m_name = "Yubikey challenge-response";

        public static string Name { get { return m_name; } }

        public const int keyLenBytes = 20;
        public const int challengeLenBytes = 64;
        public const int responseLenBytes = 20;
        public const int secretLenBytes = 20;

        //If variable length challenges are enabled, a 63 byte challenge is sent instead.
        //See GenerateChallenge() and http://forum.yubico.com/viewtopic.php?f=16&t=1078
        //This field is automatically set by calling GetSecret(). However, when creating 
        //a new database it will need to be set manually based on the user's yubikey settings
        public bool LT64
        {
            get;
            set;
        }

	    public KeeChallengeProv()
        {
            LT64 = false;
        }         

        private byte[] GenerateChallenge()
        {
	        byte[] chal = CryptoRandom.Instance.GetRandomBytes(challengeLenBytes);
            if (LT64)
            {
                chal[challengeLenBytes - 2] = (byte)~chal[challengeLenBytes - 1];
            }

            return chal;  
        }

        private byte[] GenerateResponse(byte[] challenge, byte[] key)
        {
            HMACSHA1 hmac = new HMACSHA1(key);

            if (LT64)
                challenge = challenge.Take(challengeLenBytes - 1).ToArray();

            byte[] resp = hmac.ComputeHash(challenge);
            hmac.Clear();
            return resp;
        }

        /// <summary>
        /// A method for generating encrypted ChallengeInfo to be saved. For security, this method should
        /// be called every time you get a successful challenge-response pair from the Yubikey. Failure to
        /// do so will permit password re-use attacks. 
        /// </summary>
        /// <param name="secret">The un-encrypted secret</param>
        /// <returns>A fully populated ChallengeInfo object ready to be saved</returns>
		public ChallengeInfo Encrypt(byte[] secret)
        {
            //generate a random challenge for use next time
            byte[] challenge = GenerateChallenge();

            //generate the expected HMAC-SHA1 response for the challenge based on the secret
            byte[] resp = GenerateResponse(challenge, secret);

            //use the response to encrypt the secret
            SHA256 sha = SHA256Managed.Create();
            byte[] key = sha.ComputeHash(resp); // get a 256 bit key from the 160 bit hmac response
            byte[] secretHash = sha.ComputeHash(secret);

            StandardAesEngine aes = new StandardAesEngine();
	        const uint aesIVLenBytes = 16	;
	        byte[] IV = CryptoRandom.Instance.GetRandomBytes(aesIVLenBytes);            
            byte[] encrypted;

            using (MemoryStream msEncrypt = new MemoryStream())
            {
                using (CryptoStream csEncrypt = (CryptoStream)aes.EncryptStream(msEncrypt, key, IV))
                {
                    csEncrypt.Write(secret, 0, secret.Length);
                    csEncrypt.Close();
                }

                encrypted = msEncrypt.ToArray();
                msEncrypt.Close();
            }

			ChallengeInfo inf = new ChallengeInfo (encrypted, IV, challenge, secretHash, LT64);

			sha.Clear();

			return inf;
        }
                
		private bool DecryptSecret(byte[] yubiResp, ChallengeInfo inf, out byte[] secret)
        {
            secret = new byte[keyLenBytes];

            if (inf.IV == null) return false;
            if (inf.Verification == null) return false;

            //use the response to decrypt the secret
            SHA256 sha = SHA256Managed.Create();
            byte[] key = sha.ComputeHash(yubiResp); // get a 256 bit key from the 160 bit hmac response

            StandardAesEngine aes = new StandardAesEngine();

			using (MemoryStream msDecrypt = new MemoryStream(inf.EncryptedSecret))
            {
                using (CryptoStream csDecrypt = (CryptoStream)aes.DecryptStream(msDecrypt, key, inf.IV))
                {
                    csDecrypt.Read(secret, 0, secret.Length);
                    csDecrypt.Close();
                }
                msDecrypt.Close();
            }

            byte[] secretHash = sha.ComputeHash(secret);
            for (int i = 0; i < secretHash.Length; i++)
            {
				if (secretHash[i] != inf.Verification[i])
                {
					//wrong response
                    Array.Clear(secret, 0, secret.Length);
                    return false;
                }
            }

            //return the secret
            sha.Clear();
            return true;
        }
    
        /// <summary>
        /// The primary access point for challenge-response utility functions. Accepts a pre-populated ChallengeInfo object
        /// containing at least the IV, EncryptedSecret, and Verification fields. These fields are combined with the Yubikey response
        /// to decrypt and verify the secret. 
        /// </summary>
        /// <param name="inf">A pre-populated object containing minimally the IV, EncryptedSecret and Verification fields. 
        ///                   This should be populated from the database.xml auxilliary file</param>
        /// <param name="resp"	>The Yubikey's response to the issued challenge</param>
        /// <returns>The common secret, used as a composite key to encrypt a Keepass database</returns>
		public byte[] GetSecret(ChallengeInfo inf, byte[] resp)
        {
			if (resp.Length != responseLenBytes)
				return null;
            if (inf == null)
                return null;
            if (inf.Challenge == null ||
                inf.Verification == null)
                return null;
            
            LT64 = inf.LT64;

			byte[] secret;
                      
			if (DecryptSecret(resp, inf, out secret))
            {				
                return secret;                
            }
            else
            {
                return null;
            }
        }		      

    }
}
