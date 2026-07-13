using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.ComponentModel.Design.Serialization;

namespace BinaryCreator.Exceptions
{
    [System.Serializable]
    public class IncorrectHeaderSizeException : Exception
    {
        public IncorrectHeaderSizeException()
        { }

        public IncorrectHeaderSizeException(string message)
            : base(message)
        { }

        public IncorrectHeaderSizeException(string message, Exception innerException)
            : base(message, innerException)
        { }

    }
}