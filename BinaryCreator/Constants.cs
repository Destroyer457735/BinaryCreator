using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BinaryCreator
{
    internal static class Constants
    {
        public static readonly Version thisVersion = new Version(1, 0, 0, 0);
        public static readonly long headerSize = 32;
        public static readonly long versionsSize = 16; // Version Library + Version Custom
        public static readonly long sectionAmountIntSize = 4;
        public static readonly long checksumSize = 32;
        public static readonly long sectionEntryMetaSize = 20; // Section ID Size + Section Size + Section RVA
    }
}
