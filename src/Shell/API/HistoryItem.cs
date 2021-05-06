using Newtonsoft.Json;
using System;

namespace Dotnet.Shell.API
{
    /// <summary>
    /// Representation of an item of shell history
    /// </summary>
    public class HistoryItem
    {
        /// <summary>
        /// Gets or sets the command line.
        /// </summary>
        /// <value>
        /// The command line.
        /// </value>
        public string CmdLine { get; set; }

        /// <summary>
        /// Gets or sets the time run.
        /// </summary>
        /// <value>
        /// The time run.
        /// </value>
        public DateTime TimeRun { get; set; }

        /// <summary>
        /// Gets or sets the legacy offset. In a file like .bash_history this is the line number
        /// </summary>
        /// <value>
        /// The legacy offset.
        /// </value>
        public int LegacyOffset { get; set; } = -1;

        /// <summary>
        /// Initializes a new instance of the <see cref="HistoryItem"/> class.
        /// </summary>
        /// <param name="cmdLine">The command line.</param>
        /// <param name="timeRun">The time run.</param>
        /// <param name="legacyOffset">The legacy offset.</param>
        [JsonConstructor]
        public HistoryItem(string cmdLine, DateTime timeRun, int legacyOffset)
        {
            CmdLine = cmdLine;
            TimeRun = timeRun;
            LegacyOffset = legacyOffset;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="HistoryItem"/> class.
        /// </summary>
        /// <param name="command">The command.</param>
        /// <param name="time">The time.</param>
        public HistoryItem(string command, DateTime time)
        {
            CmdLine = command;
            TimeRun = time;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="HistoryItem"/> class.
        /// </summary>
        /// <param name="command">The command.</param>
        /// <param name="offset">The offset.</param>
        public HistoryItem(string command, int offset)
        {
            CmdLine = command;
            LegacyOffset = offset;
            TimeRun = new DateTime(DateTime.MinValue.Ticks + (offset * 1000));
        }

        /// <summary>
        /// Converts the history item into a command line
        /// </summary>
        /// <returns>
        /// A <see cref="System.String" /> that represents this instance.
        /// </returns>
        public override string ToString()
        {
            return CmdLine;
        }

        /// <summary>
        /// Serializes this instance to JSON
        /// </summary>
        /// <returns></returns>
        public string Serialize()
        {
            return JsonConvert.SerializeObject(this, Formatting.None);
        }
    }
}
