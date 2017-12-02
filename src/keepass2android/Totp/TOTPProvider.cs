using System;
using System.Security;
using System.Security.Cryptography;

namespace KeeTrayTOTP.Libraries
{
    /// <summary>
    /// Provides Time-based One Time Passwords RFC 6238.
    /// </summary>
    public class TOTPProvider
    {
        /// <summary>
        /// Time reference for TOTP generation.
        /// </summary>
        private static readonly DateTime UnixEpoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        /// <summary>
        /// Duration of generation of each totp, in seconds.
        /// </summary>
        private int duration;
        public int Duration
        {
            get
            {
                return this.duration;
            }
            set
            {
                if (!(value > 0)) throw new Exception("Invalid Duration."); //Throws an exception if the duration is invalid as the class cannot work without it.
                this.duration = value; //Defines variable from argument.
            }
        }

        /// <summary>
        /// Length of the generated totp.
        /// </summary>
        private int length;
        public int Length
        {
            get
            {
                return this.length;
            }
            set
            {
                //Throws an exception if the length is invalid as the class cannot work without it.
                if (value < 4 || value > 8) throw new Exception("Invalid Length.");
                this.length = value; //Defines variable from argument.
            }

        }


        /// <summary>
        /// TOTP Encoder.
        /// </summary>
        private Func<byte[], int, string> encoder;
        public Func<byte[], int, string> Encoder
        {
            get
            {
                return this.encoder;
            }
            set
            {
                this.encoder = value; //Defines variable from argument.
            }
        }

        /// <summary>
        /// Sets the time span that is used to match the server's UTC time to ensure accurate generation of Time-based One Time Passwords.
        /// </summary>
        private TimeSpan timeCorrection;
        public TimeSpan TimeCorrection
        {
            get
            {
                return this.timeCorrection;
            }
            set
            {
                this.timeCorrection = value; //Defines variable from argument.
            }
        }

        private bool timeCorrectionError;
        public bool TimeCorrectionError
        {
            get
            {
                return this.timeCorrectionError;
            } 
        }

        /// <summary>
        /// Instanciates a new TOTP_Generator.
        /// </summary>
        /// <param name="initDuration">Duration of generation of each totp, in seconds.</param>
        /// <param name="initLength">Length of the generated totp.</param>
        /// <param name="initEncoder">The output encoder.</param>
        /*public TOTPProvider(int initDuration, int initLength, Func<byte[], int, string> initEncoder)
        {
            this.Duration = initDuration;
            this.Length = initLength;
            this.encoder = initEncoder;
            this.TimeCorrection = TimeSpan.Zero;
        }*/

        /// <summary>
        /// Instanciates a new TOTP_Generator.
        /// </summary>
        /// <param name="initSettings">Saved Settings.</param>
        public TOTPProvider(string[] Settings)
        {
            this.duration = Convert.ToInt16(Settings[0]);

            if (Settings[1] == "S")
            {
                this.length = 5;
                this.encoder = TOTPEncoder.steam;
            }
            else
            {
                this.length = Convert.ToInt16(Settings[1]);
                this.encoder = TOTPEncoder.rfc6238;
            }

            if(Settings.Length > 2 && Settings[2] != String.Empty)
            {

                {
                    this.TimeCorrection = TimeSpan.Zero;
                    this.timeCorrectionError = false;
                }
            }
            else
            {
                this.TimeCorrection = TimeSpan.Zero;
            }

                           
        }

        /// <summary>
        /// Returns current time with correction int UTC format.
        /// </summary>
        public DateTime Now
        {
            get
            {
                return DateTime.UtcNow - timeCorrection; //Computes current time minus time correction giving the corrected time.
            }
        }

        /// <summary>
        /// Returns the time remaining before counter incrementation.
        /// </summary>
        public int Timer
        {
            get
            {
                var n = (duration - (int)((Now - UnixEpoch).TotalSeconds % duration)); //Computes the seconds left before counter incrementation.
                return n == 0 ? duration : n; //Returns timer value from 30 to 1.
            }
        }

        /// <summary>
        /// Returns number of intervals that have elapsed.
        /// </summary>
        public long Counter
        {
            get
            {
                var ElapsedSeconds = (long)Math.Floor((Now - UnixEpoch).TotalSeconds); //Compute current counter for current time.
                return ElapsedSeconds / duration; //Applies specified interval to computed counter.
            }
        }

        /// <summary>
        /// Converts an unsigned integer to binary data.
        /// </summary>
        /// <param name="n">Unsigned Integer.</param>
        /// <returns>Binary data.</returns>
        private byte[] GetBytes(ulong n)
        {
            byte[] b = new byte[8]; //Math.
            b[0] = (byte)(n >> 56); //Math.
            b[1] = (byte)(n >> 48); //Math.
            b[2] = (byte)(n >> 40); //Math.
            b[3] = (byte)(n >> 32); //Math.
            b[4] = (byte)(n >> 24); //Math.
            b[5] = (byte)(n >> 16); //Math.
            b[6] = (byte)(n >> 8);  //Math.
            b[7] = (byte)(n);       //Math.
            return b;
        }

        /// <summary>
        /// Generate a TOTP using provided binary data.
        /// </summary>
        /// <param name="key">Binary data.</param>
        /// <returns>Time-based One Time Password encoded byte array.</returns>
        public byte[] Generate(byte[] key)
        {
            System.Security.Cryptography.HMACSHA1 hmac = new System.Security.Cryptography.HMACSHA1(key, true); //Instanciates a new hash provider with a key.
            byte[] hash = hmac.ComputeHash(GetBytes((ulong)Counter)); //Generates hash from key using counter.
            hmac.Clear(); //Clear hash instance securing the key.

            /*int binary =                                        //Math.
               ((hash[offset] & 0x7f) << 24)                   //Math.
               | ((hash[offset + 1] & 0xff) << 16)             //Math.
               | ((hash[offset + 2] & 0xff) << 8)              //Math.
               | (hash[offset + 3] & 0xff);                    //Math.

          int password = binary % (int)Math.Pow(10, length); //Math.*/

            int offset = hash[hash.Length - 1] & 0x0f;           //Math.
            byte[] totp = { hash[offset + 3], hash[offset + 2], hash[offset + 1], hash[offset] };
            return totp;

            /* 
             return password.ToString(new string('0', length)); //Math.*/
        }

        /// <summary>
        /// Generate a TOTP using provided binary data.
        /// </summary>
        /// <param name="key">Key in String Format.</param>
        /// <returns>Time-based One Time Password encoded byte array.</returns>
        public string Generate(string key)
        {
            byte[] bkey = Base32.Decode(key);
            return this.GenerateByByte(bkey);
        }

         /// <summary>
        /// Generate a TOTP using provided binary data.
        /// </summary>
        /// <param name="key">Binary data.</param>
        /// <returns>Time-based One Time Password encoded byte array.</returns>
        public string GenerateByByte(byte[] key)
        {

            HMACSHA1 hmac = new HMACSHA1(key, true); //Instanciates a new hash provider with a key.

            byte[] codeInterval = BitConverter.GetBytes((ulong)Counter);

            if (BitConverter.IsLittleEndian)
                Array.Reverse(codeInterval);

            byte[] hash = hmac.ComputeHash(codeInterval); //Generates hash from key using counter.
            hmac.Clear(); //Clear hash instance securing the key.
            int start = hash[hash.Length - 1] & 0xf;
            byte[] totp = new byte[4];

            Array.Copy(hash, start, totp, 0, 4);
            if (BitConverter.IsLittleEndian)
                Array.Reverse(totp);

            return this.encoder(totp, length);
        }

    }
}