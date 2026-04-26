/*//
Binary Creator - Binary Utility Library
Copyright (C) 2026  Alec James McKeever
//*/
using System;
using System.Collections.Generic;
using System.ComponentModel.Design.Serialization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BinaryCreator.Exceptions
{
    [System.Serializable]
    public class HeaderMismatchException : Exception
    {
        public HeaderMismatchException()
        { }

        public HeaderMismatchException(string message)
            : base(message)
        { }

        public HeaderMismatchException(string message, Exception innerException)
            : base(message, innerException)
        { }
    }
}
