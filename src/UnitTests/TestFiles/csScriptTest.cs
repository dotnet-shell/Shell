using System;
using System.Drawing;
using CSXShell.UI;

namespace Helper
{
    public class Pretty
    {
        public static void Print(string str)
        {
            var strSplit = str.Split(Environment.NewLine);
            for (int x = 0; x < strSplit.Length; x++)
            {
                Console.WriteLine(
                    new ColorString(
                        strSplit[x],
                        // Alternate between two colours
                        x % 2 == 0 ? Color.Gray : Color.LightBlue).TextWithFormattingCharacters);
            }
        }
    }
}