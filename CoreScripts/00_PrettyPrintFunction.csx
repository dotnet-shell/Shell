// description: Provides a function to print StdOut nicely
// author: @therealshodan
using System.Drawing;
using Dotnet.Shell.UI;

void PrettyPrint(string str, bool firstLineIsHeader=false)
{
  var header = Color.White;
  var rowA = Color.Gray;
  var rowB = Color.LightBlue;

  var strSplit = str.Split(Environment.NewLine);
  for (int x = 0; x < strSplit.Length; x++)
  {
    var color = x % 2 == 0 ? rowA : rowB;
    if (x == 0 && firstLineIsHeader)
    {
      color = header;
    }

    Console.WriteLine(new ColorString(strSplit[x],color).TextWithFormattingCharacters);
  }
}
