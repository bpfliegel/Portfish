using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Portfish
{
    internal class Stopwatch
    {
        private DateTime _lastTime = DateTime.Now;

        // Starts, or resumes, measuring elapsed time for an interval.
        internal void Start()
        {
            _lastTime = DateTime.Now;
        }

        // Stops time interval measurement and resets the elapsed time to zero.
        internal void Reset()
        {
            // No need
        }

        // Gets the total elapsed time measured by the current instance, in milliseconds.
        internal long ElapsedMilliseconds
        {
            get
            {
                return (long)((DateTime.Now - _lastTime).TotalMilliseconds);
            }
        }
    }
}
