using System;
using System.Threading.Tasks;

namespace Dotnet.Shell.Logic.Console
{
    /// <summary>
    /// Defines the method that a console implementation is required to provide
    /// </summary>
    public interface IConsole
    {
        /// <summary>
        /// Gets or sets the cursor left position.
        /// </summary>
        /// <value>
        /// The cursor left position.
        /// </value>
        int CursorLeft { get; set; }

        /// <summary>
        /// Gets or sets the cursor top position.
        /// </summary>
        /// <value>
        /// The cursor top position.
        /// </value>
        int CursorTop { get; set; }

        /// <summary>
        /// Sets a value indicating whether [cursor visible].
        /// </summary>
        /// <value>
        ///   <c>true</c> if [cursor visible]; otherwise, <c>false</c>.
        /// </value>
        bool CursorVisible { set; }

        /// <summary>
        /// Gets the width of the window.
        /// </summary>
        /// <value>
        /// The width of the window.
        /// </value>
        int WindowWidth { get; }

        /// <summary>
        /// Gets the height of the window.
        /// </summary>
        /// <value>
        /// The height of the window.
        /// </value>
        int WindowHeight { get; }

        /// <summary>
        /// Gets or sets the color of the foreground.
        /// </summary>
        /// <value>
        /// The color of the foreground.
        /// </value>
        ConsoleColor ForegroundColor { get; set; }

        /// <summary>
        /// Gets or sets the color of the background.
        /// </summary>
        /// <value>
        /// The color of the background.
        /// </value>
        ConsoleColor BackgroundColor { get; set; }

        /// <summary>
        /// Writes the specified text.
        /// </summary>
        /// <param name="text">The text.</param>
        void Write(string text = default);

        /// <summary>
        /// Writes the specified text with a newline at the end
        /// </summary>
        /// <param name="message">The message.</param>
        void WriteLine(string message = default);

        /// <summary>
        /// Saves the terminal screen state
        /// </summary>
        Task SaveAsync();

        /// <summary>
        /// Restores the terminal screen state
        /// </summary>
        Task RestoreAsync();

        /// <summary>
        /// Reads the next key press
        /// </summary>
        /// <returns>ConsoleKeyInfo</returns>
        ConsoleKeyInfo ReadKey();

        /// <summary>
        /// Gets a value indicating whether [key availiable].
        /// </summary>
        /// <value>
        ///   <c>true</c> if [key availiable]; otherwise, <c>false</c>.
        /// </value>
        bool KeyAvailiable { get; }
    }
}
