using System.Drawing;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;

[assembly: InternalsVisibleTo("UnitTests")]

namespace Dotnet.Shell.UI
{
    /// <summary>
    /// Implements a color string replacemnt class using ANSI formatting escape codes
    /// </summary>
    public class ColorString
    {
        private const string Reset = "\u001b[0m";

        private const string RGBForegroundFormat = "\u001b[38;2;{0};{1};{2}m";
        private const string RGBBackgroundFormat = "\u001b[48;2;{0};{1};{2}m";

        private static readonly Regex RemoveEscapeCharsRegex = new(@"(\x9B|\x1B\[)[0-?]*[ -\/]*[@-~]", RegexOptions.Compiled);

        /// <summary>
        /// Gets the string representation but without formatting characters
        /// </summary>
        public string Text { get; private set; }

        /// <summary>
        /// Gets the string representation but WITH formatting characters
        /// </summary>
        public string TextWithFormattingCharacters { get; private set; }

        /// <summary>
        /// Convert to a color string from an ANSI string with escape sequences
        /// </summary>
        /// <param name="ansi"></param>
        /// <returns></returns>
        public static ColorString FromRawANSI(string ansi)
        {
            return new ColorString(RemoveEscapeCharsRegex.Replace(ansi, string.Empty), ansi);
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ColorString"/> class.
        /// </summary>
        /// <param name="s">The s.</param>
        /// <param name="ansi">The ANSI.</param>
        internal ColorString(string s, string ansi = null)
        {
            Text = s;
            if (ansi != null)
            {
                TextWithFormattingCharacters = ansi;
            }
            else
            {
                TextWithFormattingCharacters = s;
            }
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ColorString"/> class.
        /// </summary>
        /// <param name="s">The s.</param>
        /// <param name="c">The c.</param>
        public ColorString(string s, Color c) : this(s)
        {
            // Modern terminals support RGB so we use that to get a full range of colors
            TextWithFormattingCharacters = string.Format(RGBForegroundFormat, c.R, c.G, c.B) + s + Reset;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ColorString"/> class.
        /// </summary>
        /// <param name="s">The s.</param>
        /// <param name="c">The c.</param>
        /// <param name="d">The d.</param>
        public ColorString(string s, Color c, Color d) : this(s, c)
        {
            // the base construct will already set the text and add reset so we only have to prepend the background colour
            TextWithFormattingCharacters = string.Format(RGBBackgroundFormat, d.R, d.G, d.B) + TextWithFormattingCharacters;
        }

        /// <summary>
        /// Gets the length.
        /// </summary>
        /// <value>
        /// The length.
        /// </value>
        public int Length => Text.Length;

        /// <summary>
        /// Performs an implicit conversion from <see cref="System.String"/> to <see cref="ColorString"/>.
        /// </summary>
        /// <param name="t">The t.</param>
        /// <returns>
        /// The result of the conversion.
        /// </returns>
        public static implicit operator ColorString(string t)
        {
            if (t == null)
            {
                return null;
            }

            return new ColorString(t);
        }

        /// <summary>
        /// Converts to string.
        /// </summary>
        /// <returns>
        /// A <see cref="System.String" /> that represents this instance.
        /// </returns>
        public override string ToString()
        {
            return Text;
        }

        /// <summary>
        /// Determines whether the specified <see cref="System.Object" />, is equal to this instance.
        /// </summary>
        /// <param name="obj">The <see cref="System.Object" /> to compare with this instance.</param>
        /// <returns>
        ///   <c>true</c> if the specified <see cref="System.Object" /> is equal to this instance; otherwise, <c>false</c>.
        /// </returns>
        public override bool Equals(object obj)
        {
            return Text.Equals(obj);
        }

        /// <summary>
        /// Returns a hash code for this instance.
        /// </summary>
        /// <returns>
        /// A hash code for this instance, suitable for use in hashing algorithms and data structures like a hash table. 
        /// </returns>
        public override int GetHashCode()
        {
            return Text.GetHashCode();
        }

        /// <summary>
        /// Gets the <see cref="System.Char"/> with the specified i.
        /// </summary>
        /// <value>
        /// The <see cref="System.Char"/>.
        /// </value>
        /// <param name="i">The i.</param>
        /// <returns></returns>
        public char this[int i]
        {
            get { return Text[i]; }
        }

        /// <summary>
        /// Implements the operator +.
        /// </summary>
        /// <param name="a">a.</param>
        /// <param name="b">The b.</param>
        /// <returns>
        /// The result of the operator.
        /// </returns>
        public static ColorString operator +(ColorString a, ColorString b)
        {
            var text = a.Text + b.Text;
            var newFormattedString = a.TextWithFormattingCharacters + b.TextWithFormattingCharacters;

            return new ColorString(text, newFormattedString);
        }
    }
}
