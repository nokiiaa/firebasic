using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace firebasic
{
    public partial class Platform
    {
        public int SizeOfBool { get; set; }

        public int SizeOfChar { get; set; }

        public int SizeOfWChar { get; set; }

        public int SizeOfShort { get; set; }

        public int SizeOfInt { get; set; }

        public int SizeOfLong { get; set; }

        public int SizeOfFloat { get; set; }

        public int SizeOfDouble { get; set; }

        public int SizeOfPtr { get; set; }

        public (long Min, long Max) BoolLimits { get; set; }

        public (long Min, long Max) CharLimits { get; set; }

        public (ulong Min, ulong Max) UCharLimits { get; set; }

        public (long Min, long Max) WCharLimits { get; set; }

        public (long Min, long Max) ShortLimits { get; set; }

        public (ulong Min, ulong Max) UShortLimits { get; set; }

        public (long Min, long Max) IntLimits { get; set; }

        public (ulong Min, ulong Max) UIntLimits { get; set; }

        public (long Min, long Max) LongLimits { get; set; }

        public (ulong Min, ulong Max) ULongLimits { get; set; }

        public static Platform Current => Universal;
    }
}
