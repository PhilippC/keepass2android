using System;

namespace OtpProviderClient
{
    /// <summary>
    /// Provides Time-based One Time Passwords RFC 6238.
    /// </summary>
    public class Totp_Provider
    {
        /// <summary>
        /// Time reference for TOTP generation.
        /// </summary>
        private static readonly DateTime UnixEpoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        /// <summary>
        /// Duration of generation of each totp, in seconds.
        /// </summary>
        private int _Duration;

        /// <summary>
        /// Length of the generated totp.
        /// </summary>
        private int _Length;

        private TimeSpan _TimeCorrection;
        /// <summary>
        /// Sets the time span that is used to match the server's UTC time to ensure accurate generation of Time-based One Time Passwords.
        /// </summary>
        public TimeSpan TimeCorrection { set { _TimeCorrection = value; } }

        /// <summary>
        /// Instanciates a new Totp_Generator.
        /// </summary>
        /// <param name="Duration">Duration of generation of each totp, in seconds.</param>
        /// <param name="Length">Length of the generated totp.</param>
        public Totp_Provider(int Duration, int Length)
        {
            if (!(Duration > 0)) throw new Exception("Invalid Duration."); //Throws an exception if the duration is invalid as the class cannot work without it.
            _Duration = Duration; //Defines variable from argument.
            if (!((Length > 5) && (Length < 9))) throw new Exception("Invalid Length."); //Throws an exception if the length is invalid as the class cannot work without it.
            _Length = Length; //Defines variable from argument.
            _TimeCorrection = TimeSpan.Zero; //Defines variable from non-constant default value.
        }

        /// <summary>
        /// Returns current time with correction int UTC format.
        /// </summary>
        public DateTime Now
        {
            get
            {
                return DateTime.UtcNow - _TimeCorrection; //Computes current time minus time correction giving the corrected time.
            }
        }

        /// <summary>
        /// Returns the time remaining before counter incrementation.
        /// </summary>
        public int Timer
        {
            get
            {
                var n = (_Duration - (int)((Now - UnixEpoch).TotalSeconds % _Duration)); //Computes the seconds left before counter incrementation.
                return n == 0 ? _Duration : n; //Returns timer value from 30 to 1.
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
                return ElapsedSeconds / _Duration; //Applies specified interval to computed counter.
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
        /// Generate a Totp using provided binary data.
        /// </summary>
        /// <param name="key">Binary data.</param>
        /// <returns>Time-based One Time Password.</returns>
        public string Generate(byte[] key)
        {
            System.Security.Cryptography.HMACSHA1 hmac = new System.Security.Cryptography.HMACSHA1(key, true); //Instanciates a new hash provider with a key.
            byte[] hash = hmac.ComputeHash(GetBytes((ulong)Counter)); //Generates hash from key using counter.
            hmac.Clear(); //Clear hash instance securing the key.

            int offset = hash[hash.Length - 1] & 0xf;           //Math.
            int binary =                                        //Math.
                ((hash[offset] & 0x7f) << 24)                   //Math.
                | ((hash[offset + 1] & 0xff) << 16)             //Math.
                | ((hash[offset + 2] & 0xff) << 8)              //Math.
                | (hash[offset + 3] & 0xff);                    //Math.

            int password = binary % (int)Math.Pow(10, _Length); //Math.
            return password.ToString(new string('0', _Length)); //Math.
        }
    }

    /// <summary>
    /// Provides time correction for Time-based One Time Passwords that require accurate DateTime syncronisation with server.
    /// </summary>
    public class TimeCorrection_Provider
    {
        /// <summary>
        /// Timer providing the delay between each time correction check.
        /// </summary>
        private System.Timers.Timer _Timer;

        /// <summary>
        /// Thread which handles the time correction check.
        /// </summary>
        private System.Threading.Thread Task;

        private bool _Enable;
        /// <summary>
        /// Defines weither or not the class will attempt to get time correction from the server.
        /// </summary>
        public bool Enable { get { return _Enable; } set { _Enable = value; _Timer.Enabled = value; } }

        private static int _Interval = 60;
        /// <summary>
        /// Gets or sets the interval in minutes between each online checks for time correction.
        /// </summary>
        /// <value>Time</value>
        public static int Interval { get { return _Interval; } set { _Interval = value; } }
        private long _IntervalStretcher;

        private volatile string _Url;
        /// <summary>
        /// Returns the URL this instance is using to checks for time correction.
        /// </summary>
        public string Url { get { return _Url; } }

        private TimeSpan _TimeCorrection;
        /// <summary>
        /// Returns the time span between server UTC time and this computer's UTC time of the last check for time correction.
        /// </summary>
        public TimeSpan TimeCorrection { get { return _TimeCorrection; } }

        private DateTime _LastUpdateDateTime;
        /// <summary>
        /// Returns the date and time in universal format of the last online check for time correction.
        /// </summary>
        public DateTime LastUpdateDateTime { get { return _LastUpdateDateTime; } }

        private bool _LastUpdateSucceded = false;
        /// <summary>
        /// Returns true if the last check for time correction was successful.
        /// </summary>
        public bool LastUpdateSucceded { get { return _LastUpdateSucceded; } }
        
        /// <summary>
        /// Instanciates a new Totp_TimeCorrection using the specified URL to contact the server.
        /// </summary>
        /// <param name="Url">URL of the server to get check.</param>
        /// <param name="Enable">Enable or disable the time correction check.</param>
        public TimeCorrection_Provider(string Url, bool Enable = true)
        {
            if (Url == string.Empty) throw new Exception("Invalid URL."); //Throws exception if the URL is invalid as the class cannot work without it.
            _Url = Url; //Defines variable from argument.
            _Enable = Enable; //Defines variable from argument.
            _LastUpdateDateTime = DateTime.MinValue; //Defines variable from non-constant default value.
            _TimeCorrection = TimeSpan.Zero; //Defines variable from non-constant default value.
            _Timer = new System.Timers.Timer(); //Instanciates timer.
            _Timer.Elapsed += Timer_Elapsed; //Handles the timer event
            _Timer.Interval = 1000; //Defines the timer interval to 1 seconds.
            _Timer.Enabled = _Enable; //Defines the timer to run if the class is initially enabled.
            Task = new System.Threading.Thread(Task_Thread); //Instanciate a new task.
            if (_Enable) Task.Start(); //Starts the new thread if the class is initially enabled.
        }

        /// <summary>
        /// Task that occurs every time the timer's interval has elapsed.
        /// </summary>
        private void Timer_Elapsed(object sender, EventArgs e)
        {
            _IntervalStretcher++; //Increments timer.
            if (_IntervalStretcher >= (60 * _Interval)) //Checks if the specified delay has been reached.
            {
                _IntervalStretcher = 0; //Resets the timer.
                Task_Do(); //Attempts to run a new task
            }
        }

        /// <summary>
        /// Instanciates a new task and starts it.
        /// </summary>
        /// <returns>Informs if reinstanciation of the task has succeeded or not. Will fail if the thread is still active from a previous time correction check.</returns>
        private bool Task_Do()
        {
            if (!Task.IsAlive) //Checks if the task is still running.
            {
                Task = new System.Threading.Thread(Task_Thread); //Instanciate a new task.
                Task.Start(); //Starts the new thread.
                return true; //Informs if successful
            }
            return false; //Informs if failed
        }

        /// <summary>
        /// Event that occurs when the timer has reached the required value. Attempts to get time correction from the server.
        /// </summary>
        private void Task_Thread()
        {
            try
            {
                var WebClient = new System.Net.WebClient(); //WebClient to connect to server.
                WebClient.DownloadData(_Url); //Downloads the server's page using HTTP or HTTPS.
                var DateHeader = WebClient.ResponseHeaders.Get("Date"); //Gets the date from the HTTP header of the downloaded page.
                _TimeCorrection = DateTime.UtcNow - DateTime.Parse(DateHeader, System.Globalization.CultureInfo.InvariantCulture.DateTimeFormat).ToUniversalTime(); //Compares the downloaded date to the systems date giving us a timespan.
                _LastUpdateSucceded = true; //Informs that the date check has succeeded.
            }
            catch (Exception)
            {
                _LastUpdateSucceded = false; //Informs that the date check has failed.
            }
            _LastUpdateDateTime = DateTime.Now; //Informs when the last update has been attempted (succeeded or not).
        }

        /// <summary>
        /// Perform a time correction check, may a few seconds.
        /// </summary>
        /// <param name="ResetTimer">Resets the timer to 0. Occurs even if the attempt to attempt a new time correction fails.</param>
        /// <param name="ForceCheck">Attempts to get time correction even if disabled.</param>
        /// <returns>Informs if the time correction check was attempted or not. Will fail if the thread is still active from a previous time correction check.</returns>
        public bool CheckNow(bool ResetTimer = true, bool ForceCheck = false)
        {
            if (ResetTimer) //Checks if the timer should be reset.
            {
                _IntervalStretcher = 0; //Resets the timer.
            }
            if (ForceCheck || _Enable) //Checks if this check is forced or if time correction is enabled.
            {
                return Task_Do(); //Attempts to run a new task and informs if attempt to attemp is a success of fail
            }
            return false; //Informs if not attempted to attempt
        }
    }

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