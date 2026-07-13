using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BinaryCreator.Exceptions
{
    [System.Serializable]
    public class ChecksumMismatchException : Exception
    {
        public ChecksumMismatchException()
        { }

        public ChecksumMismatchException(string message)
            : base(message)
        { }

        public ChecksumMismatchException(string message, Exception innerException)
            : base(message, innerException)
        { }
    }
}
