using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Stopwatch
{
    internal class StopwatchSettings
    {
        internal string SpineFileName { get; set; }

        internal string StopwatchId { get; set; }

        internal bool ResetOnStart { get; set; }

        internal bool ClearFileOnReset { get; set; }

        internal bool LapMode { get; set; }

        internal string FileName { get; set; }
    }
}
