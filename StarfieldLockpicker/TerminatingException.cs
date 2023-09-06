using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace StarfieldLockpicker
{
    public class TerminatingException : Exception
    {
        public TerminatingException()
        {
        }

        public TerminatingException(string message) : base(message)
        {
        }

        public TerminatingException(string message, Exception inner) : base(message, inner)
        {
        }
    }
}
