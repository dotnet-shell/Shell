using System;
using System.Collections.Generic;

namespace Dotnet.Shell.Logic.Compilation
{
    internal class PreProcessorSyntaxException : Exception
    {
        public List<int> RelatedLines { get; } = new List<int>();
        public int Line { get; }

        public PreProcessorSyntaxException(string msg, int line) : base(msg)
        {
            Line = line;
        }

        public PreProcessorSyntaxException(string msg, int line, List<int> relatedLines) : this(msg, line)
        {
            RelatedLines.AddRange(relatedLines);
        }
    }
}
