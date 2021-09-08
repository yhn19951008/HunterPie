﻿using HunterPie.Core.Logger;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HunterPie.Domain.Logger
{
    internal class LogTracer : TraceListener
    {
        public override void Write(string message)
        {
            
        }

        public override void WriteLine(string message)
        {
            Log.Error(message);
        }
    }
}
