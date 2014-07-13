using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using MasterPassword;
using Microsoft.VisualStudio.TestTools.UnitTesting;
namespace MPTest
{

	[TestClass()]
	public class MpAlgorithmTests
	{

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


		private static byte[] HashHMAC(byte[] key, byte[] message)
		{
			var hash = new HMACSHA256(key);
			return hash.ComputeHash(message);
		}

		[TestMethod()]
		public void GenerateKeyTest()
		{
			sbyte[] expectedRes =
				{
					-103, 59, -64, -27, 39, -62, 10, -76, -24, -28, -111, -75, 13, -128, -80, -101, 39, 41, -98, -22,
					-42, 61, -75, 38, -107, -40, 111, 61, 108, 63, 60, 82, 92, -39, 72, 14, 14, -26, 93, 67, 83, 25, -32, 5, 32, 102,
					-126, 24, 15, 65, 9, 17, 0, 123, 91, 105, -46, -99, -64, 123, -12, 80, -37, -77
				};
			var result = MpAlgorithm.GetKeyForPassword("u", "test");

			Assert.IsTrue(expectedRes.SequenceEqual(ToSignedByteArray(result)));

		}
		[TestMethod]
		public void GenerateContentTest()
		{
			var key = MpAlgorithm.GetKeyForPassword("u", "test");
			string password = MpAlgorithm.GenerateContent("Long Password", "strn", key, 1, HashHMAC);
			Assert.AreEqual("LapdKebv2_Tele", password);
		}

		[TestMethod]
		public void GenerateContentWithUmlautsTest()
		{
			var key = MpAlgorithm.GetKeyForPassword("uÜ", "testÖ");
			string password = MpAlgorithm.GenerateContent("Long Password", "strnÄ", key, 1, HashHMAC);
			Assert.AreEqual("YepiHedo1*Kada", password);
		}

		[TestMethod]
		public void GenerateContentWithUmlautsAndCounterTest()
		{
			var key = MpAlgorithm.GetKeyForPassword("uÜ", "testÖ");
			string password = MpAlgorithm.GenerateContent("Long Password", "strnÄ", key, 42, HashHMAC);
			Assert.AreEqual("Gasc3!YeluMoqb", password);
		}

		[TestMethod]
		public void GetKeyWithUmlautsTest()
		{
			var key = MpAlgorithm.GetKeyForPassword("uÜ", "testÖ");
			sbyte[] expected =
				{
					-53, -69, -89, 48, 122, 56, 34, 13, -70, -103, 102, 90, -96, -75, 45, 68, 43, 67, 97, 60, 84, -90, 98, -95, -2, -2, 99, -60, -121, -2, -26, -45, 53, -31, 47, 0, -46, -97, 77, -41, 63, -15, -30, 60, 4, -120, 32, 122, -94, 42, 122, -103, -61, -115, 75, -123, -15, 47, 61, -100, -119, 115, 118, 82
				};
			Assert.IsTrue(expected.SequenceEqual(ToSignedByteArray(key)));
		}
	}
}
