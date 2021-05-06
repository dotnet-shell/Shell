using Newtonsoft.Json;
using System;

namespace Dotnet.Shell.API
{
    public class HistoryItem
    {
        public string CmdLine { get; set; }
        public DateTime TimeRun { get; set; }
        public int LegacyOffset { get; set; } = -1;

        [JsonConstructor]
        public HistoryItem(string cmdLine, DateTime timeRun, int legacyOffset)
        {
            CmdLine = cmdLine;
            TimeRun = timeRun;
            LegacyOffset = legacyOffset;
        }

        public HistoryItem(string command, DateTime time)
        {
            CmdLine = command;
            TimeRun = time;
        }

        public HistoryItem(string command, int offset)
        {
            CmdLine = command;
            LegacyOffset = offset;
            TimeRun = new DateTime(DateTime.MinValue.Ticks + (offset * 1000));
        }

        public override string ToString()
        {
            return CmdLine;
        }

        public string Serialize()
        {
            return JsonConvert.SerializeObject(this, Formatting.None);
        }
    }
}
