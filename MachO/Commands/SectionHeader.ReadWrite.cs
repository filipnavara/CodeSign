using System.Buffers.Binary;
using System.Text;

namespace CodeSign.MachO.Commands
{
    public partial class SectionHeader
    {
        public const int BinarySize = 68;

        public static SectionHeader Read(ReadOnlySpan<byte> buffer, bool isLittleEndian)
        {
            if (isLittleEndian)
            {
                return new SectionHeader
                {
                    SectionName = Encoding.UTF8.GetString(buffer.SliceZeroTerminated(0, 16)),
                    SegmentName = Encoding.UTF8.GetString(buffer.SliceZeroTerminated(16, 16)),
                    Address = BinaryPrimitives.ReadUInt32LittleEndian(buffer.Slice(32, 4)),
                    Size = BinaryPrimitives.ReadUInt32LittleEndian(buffer.Slice(36, 4)),
                    FileOffset = BinaryPrimitives.ReadUInt32LittleEndian(buffer.Slice(40, 4)),
                    Alignment = BinaryPrimitives.ReadUInt32LittleEndian(buffer.Slice(44, 4)),
                    RelocationOffset = BinaryPrimitives.ReadUInt32LittleEndian(buffer.Slice(48, 4)),
                    NumberOfReloationEntries = BinaryPrimitives.ReadUInt32LittleEndian(buffer.Slice(52, 4)),
                    Flags = BinaryPrimitives.ReadUInt32LittleEndian(buffer.Slice(56, 4)),
                    Reserved1 = BinaryPrimitives.ReadUInt32LittleEndian(buffer.Slice(60, 4)),
                    Reserved2 = BinaryPrimitives.ReadUInt32LittleEndian(buffer.Slice(64, 4)),
                };
            }
            else
            {
                return new SectionHeader
                {
                    SectionName = Encoding.UTF8.GetString(buffer.SliceZeroTerminated(0, 16)),
                    SegmentName = Encoding.UTF8.GetString(buffer.SliceZeroTerminated(16, 16)),
                    Address = BinaryPrimitives.ReadUInt32BigEndian(buffer.Slice(32, 4)),
                    Size = BinaryPrimitives.ReadUInt32BigEndian(buffer.Slice(36, 4)),
                    FileOffset = BinaryPrimitives.ReadUInt32BigEndian(buffer.Slice(40, 4)),
                    Alignment = BinaryPrimitives.ReadUInt32BigEndian(buffer.Slice(44, 4)),
                    RelocationOffset = BinaryPrimitives.ReadUInt32BigEndian(buffer.Slice(48, 4)),
                    NumberOfReloationEntries = BinaryPrimitives.ReadUInt32BigEndian(buffer.Slice(52, 4)),
                    Flags = BinaryPrimitives.ReadUInt32BigEndian(buffer.Slice(56, 4)),
                    Reserved1 = BinaryPrimitives.ReadUInt32BigEndian(buffer.Slice(60, 4)),
                    Reserved2 = BinaryPrimitives.ReadUInt32BigEndian(buffer.Slice(64, 4)),
                };
            }
        }

        // TODO: Write
    }
}