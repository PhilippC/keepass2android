using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace KeeTrayTOTP.Libraries
{
    /// <summary>
    /// Provides time correction for Time-based One Time Passwords that require accurate DateTime syncronisation with server.
    /// </summary>
    public class TimeCorrectionProvider
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
        /// Instanciates a new TOTP_TimeCorrection using the specified URL to contact the server.
        /// </summary>
        /// <param name="Url">URL of the server to get check.</param>
        /// <param name="Enable">Enable or disable the time correction check.</param>
        public TimeCorrectionProvider(string Url, bool Enable = true)
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
}
