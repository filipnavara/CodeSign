namespace CodeSign.MachO.Commands
{
    static class SliceExtensions
    {
        public static ReadOnlySpan<byte> SliceZeroTerminated(this ReadOnlySpan<byte> span, int offset, int length)
        {
            var slice = span.Slice(offset, length);
            var zeroIndex = slice.IndexOf((byte)0);
            if (zeroIndex >= 0)
                return slice.Slice(0, zeroIndex);
            return slice;
        }
    }
}