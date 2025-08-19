using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;


namespace KeePass.Util
{
    public class ExceptionUtil
    {

        public static string GetErrorMessage(Exception e)
        {

            try
            {
                string errorMessage = e.Message;
                if (e is Java.Lang.Exception javaException)
                {
                    errorMessage = javaException.LocalizedMessage ?? javaException.Message ?? errorMessage;
                }

                return errorMessage;
            }
            catch
            {
                return "";
            }
        }

    }
}