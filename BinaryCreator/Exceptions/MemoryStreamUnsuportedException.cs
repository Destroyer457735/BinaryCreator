using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BinaryCreator.Exceptions
{
    [System.Serializable]
    public class MemoryStreamUnsuportedException : Exception
    {
        public MemoryStreamUnsuportedException()
        { }

        public MemoryStreamUnsuportedException(string message)
            : base(message)
        { }

        public MemoryStreamUnsuportedException(string message, Exception innerException)
            : base(message, innerException)
        { }
    }
}
