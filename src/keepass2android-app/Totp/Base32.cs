using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace KeeTrayTOTP.Libraries
{
    /// <summary>
    /// Utility to deal with Base32 encoding and decoding.
    /// </summary>
    /// <remarks>
    /// http://tools.ietf.org/html/rfc4648
    /// </remarks>
    public static class Base32
    {
        /// <summary>
        /// The number of bits in a base32 encoded character.
        /// </summary>
        private const int encodedBitCount = 5;
        /// <summary>
        /// The number of bits in a byte.
        /// </summary>
        private const int byteBitCount = 8;
        /// <summary>
        /// A string containing all of the base32 characters in order.
        /// This allows a simple indexof or [index] to convert between
        /// a numeric value and an encoded character and vice versa.
        /// </summary>
        private const string encodingChars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";
        /// <summary>
        /// Takes a block of data and converts it to a base 32 encoded string.
        /// </summary>
        /// <param name="data">Input data.</param>
        /// <returns>base 32 string.</returns>
        public static string Encode(byte[] data)
        {
            if (data == null)
                throw new ArgumentNullException();
            if (data.Length == 0)
                throw new ArgumentNullException();

            // The output character count is calculated in 40 bit blocks.  That is because the least
            // common blocks size for both binary (8 bit) and base 32 (5 bit) is 40.  Padding must be used
            // to fill in the difference.
            int outputCharacterCount = (int)Math.Ceiling(data.Length / (decimal)encodedBitCount) * byteBitCount;
            char[] outputBuffer = new char[outputCharacterCount];

            byte workingValue = 0;
            short remainingBits = encodedBitCount;
            int currentPosition = 0;

            foreach (byte workingByte in data)
            {
                workingValue = (byte)(workingValue | (workingByte >> (byteBitCount - remainingBits)));
                outputBuffer[currentPosition++] = encodingChars[workingValue];

                if (remainingBits <= byteBitCount - encodedBitCount)
                {
                    workingValue = (byte)((workingByte >> (byteBitCount - encodedBitCount - remainingBits)) & 31);
                    outputBuffer[currentPosition++] = encodingChars[workingValue];
                    remainingBits += encodedBitCount;
                }

                remainingBits -= byteBitCount - encodedBitCount;
                workingValue = (byte)((workingByte << remainingBits) & 31);
            }

            // If we didn't finish, write the last current working char.
            if (currentPosition != outputCharacterCount)
                outputBuffer[currentPosition++] = encodingChars[workingValue];

            // RFC 4648 specifies that padding up to the end of the next 40 bit block must be provided
            // Since the outputCharacterCount does account for the paddingCharacters, fill it out.
            while (currentPosition < outputCharacterCount)
            {
                // The RFC defined paddinc char is '='.
                outputBuffer[currentPosition++] = '=';
            }

            return new string(outputBuffer);
        }

        /// <summary>
        /// Takes a base 32 encoded value and converts it back to binary data.
        /// </summary>
        /// <param name="base32">Base 32 encoded string.</param>
        /// <returns>Binary data.</returns>
        public static byte[] Decode(string base32)
        {
            if (string.IsNullOrEmpty(base32))
                throw new ArgumentNullException();

            var unpaddedBase32 = base32.ToUpperInvariant().TrimEnd('=');
            foreach (var c in unpaddedBase32)
            {
                if (encodingChars.IndexOf(c) < 0)
                    throw new ArgumentException("Base32 contains illegal characters.");
            }

            // we have already removed the padding so this will tell us how many actual bytes there should be.
            int outputByteCount = unpaddedBase32.Length * encodedBitCount / byteBitCount;
            byte[] outputBuffer = new byte[outputByteCount];

            byte workingByte = 0;
            short bitsRemaining = byteBitCount;
            int mask = 0;
            int arrayIndex = 0;

            foreach (char workingChar in unpaddedBase32)
            {
                int encodedCharacterNumericValue = encodingChars.IndexOf(workingChar);

                if (bitsRemaining > encodedBitCount)
                {
                    mask = encodedCharacterNumericValue << (bitsRemaining - encodedBitCount);
                    workingByte = (byte)(workingByte | mask);
                    bitsRemaining -= encodedBitCount;
                }
                else
                {
                    mask = encodedCharacterNumericValue >> (encodedBitCount - bitsRemaining);
                    workingByte = (byte)(workingByte | mask);
                    outputBuffer[arrayIndex++] = workingByte;
                    workingByte = (byte)(encodedCharacterNumericValue << (byteBitCount - encodedBitCount + bitsRemaining));
                    bitsRemaining += byteBitCount - encodedBitCount;
                }
            }

            return outputBuffer;
        }
    }
}
