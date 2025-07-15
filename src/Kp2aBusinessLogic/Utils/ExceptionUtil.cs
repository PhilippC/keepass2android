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
            string errorMessage = e.Message;
            if (e is Java.Lang.Exception javaException)
            {
                try
                {
                    errorMessage = javaException.LocalizedMessage ?? javaException.Message ?? errorMessage;
                }
                finally
                {

                }
            }

            return errorMessage;
        }

    }
}