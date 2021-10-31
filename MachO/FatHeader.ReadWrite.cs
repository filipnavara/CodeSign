using System.Buffers.Binary;

namespace CodeSign.MachO
{
    public partial class FatHeader
    {
        public const int BinarySize = 4;

        public static FatHeader Read(ReadOnlySpan<byte> buffer, bool isLittleEndian)
        {
            if (isLittleEndian)
            {
                return new FatHeader
                {
                    NumberOfFatArchitectures = BinaryPrimitives.ReadUInt32LittleEndian(buffer.Slice(0, 4)),
                };
            }
            else
            {
                return new FatHeader
                {
                    NumberOfFatArchitectures = BinaryPrimitives.ReadUInt32BigEndian(buffer.Slice(0, 4)),
                };
            }
        }

        public void Write(Span<byte> buffer, bool isLittleEndian)
        {
            if (isLittleEndian)
            {
                BinaryPrimitives.WriteUInt32LittleEndian(buffer.Slice(0, 4), this.NumberOfFatArchitectures);
            }
            else
            {
                BinaryPrimitives.WriteUInt32BigEndian(buffer.Slice(0, 4), this.NumberOfFatArchitectures);
            }
        }
    }
}