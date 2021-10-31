using System.Buffers.Binary;
using System.Text;

namespace CodeSign.MachO.Commands
{
    public partial class SegmentHeader
    {
        public const int BinarySize = 48;

        public static SegmentHeader Read(ReadOnlySpan<byte> buffer, bool isLittleEndian)
        {
            if (isLittleEndian)
            {
                return new SegmentHeader
                {
                    Name = Encoding.UTF8.GetString(buffer.SliceZeroTerminated(0, 16)),
                    Address = BinaryPrimitives.ReadUInt32LittleEndian(buffer.Slice(16, 4)),
                    Size = BinaryPrimitives.ReadUInt32LittleEndian(buffer.Slice(20, 4)),
                    FileOffset = BinaryPrimitives.ReadUInt32LittleEndian(buffer.Slice(24, 4)),
                    FileSize = BinaryPrimitives.ReadUInt32LittleEndian(buffer.Slice(28, 4)),
                    MaximalProtection = (VmProtection)BinaryPrimitives.ReadUInt32LittleEndian(buffer.Slice(32, 4)),
                    InitialProtection = (VmProtection)BinaryPrimitives.ReadUInt32LittleEndian(buffer.Slice(36, 4)),
                    NumberOfSections = BinaryPrimitives.ReadUInt32LittleEndian(buffer.Slice(40, 4)),
                    Flags = BinaryPrimitives.ReadUInt32LittleEndian(buffer.Slice(44, 4)),
                };
            }
            else
            {
                return new SegmentHeader
                {
                    Name = Encoding.UTF8.GetString(buffer.SliceZeroTerminated(0, 16)),
                    Address = BinaryPrimitives.ReadUInt32BigEndian(buffer.Slice(16, 4)),
                    Size = BinaryPrimitives.ReadUInt32BigEndian(buffer.Slice(20, 4)),
                    FileOffset = BinaryPrimitives.ReadUInt32BigEndian(buffer.Slice(24, 4)),
                    FileSize = BinaryPrimitives.ReadUInt32BigEndian(buffer.Slice(28, 4)),
                    MaximalProtection = (VmProtection)BinaryPrimitives.ReadUInt32BigEndian(buffer.Slice(32, 4)),
                    InitialProtection = (VmProtection)BinaryPrimitives.ReadUInt32BigEndian(buffer.Slice(36, 4)),
                    NumberOfSections = BinaryPrimitives.ReadUInt32BigEndian(buffer.Slice(40, 4)),
                    Flags = BinaryPrimitives.ReadUInt32BigEndian(buffer.Slice(44, 4)),
                };
            }
        }

        // TODO: Write
    }
}