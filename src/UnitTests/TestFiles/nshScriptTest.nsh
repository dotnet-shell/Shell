using System.Drawing;

void PrettyPrint(string str)
{
  var strSplit = str.Split(Environment.NewLine);
  for (int x = 0; x < strSplit.Length; x++)
  {
    Console.WriteLine(new ColorString(strSplit[x], x % 2 == 0 ? Color.Red : Color.Yellow).TextWithFormattingCharacters);
  }
}