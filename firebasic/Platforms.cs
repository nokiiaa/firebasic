namespace firebasic
{
    public partial class Platform
    {
        public static Platform Universal = new Platform
        {
            SizeOfBool   = 4,
            SizeOfChar   = 1,
            SizeOfWChar  = 2,
            SizeOfShort  = 2,
            SizeOfInt    = 4,
            SizeOfLong   = 8,
            SizeOfPtr    = 8,
            SizeOfFloat  = 4,
            SizeOfDouble = 8,
            BoolLimits   = (int.MinValue, int.MaxValue),
            CharLimits   = (char.MinValue, char.MaxValue),
            UCharLimits  = (0, byte.MaxValue),
            ShortLimits  = (short.MinValue, short.MaxValue),
            UShortLimits = (0, ushort.MaxValue),
            IntLimits    = (int.MinValue, int.MaxValue),
            UIntLimits   = (0, uint.MaxValue),
            LongLimits   = (long.MinValue, long.MaxValue),
            ULongLimits  = (0, ulong.MaxValue),
        };
    }
}