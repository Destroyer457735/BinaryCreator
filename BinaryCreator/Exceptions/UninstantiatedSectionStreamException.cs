using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BinaryCreator.Exceptions
{
    [System.Serializable]
    public class UninstantiatedSectionStreamException : Exception
    {
        public UninstantiatedSectionStreamException()
        { }

        public UninstantiatedSectionStreamException(string message)
            : base(message)
        { }

        public UninstantiatedSectionStreamException(string message, Exception innerException)
            : base(message, innerException)
        { }
    }
}
