using Dotnet.Shell.UI;
using System;
using System.Drawing;

namespace Dotnet.Shell.API.Helpers
{
    public sealed class ConsoleEx
    {
        public static void WriteLine(string message, Color? textColor = null)
        {
            Color txtColor = textColor ?? Color.Cyan;
            Console.WriteLine(new ColorString(message, txtColor));
        }
    }
}
