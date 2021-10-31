using System.Buffers.Binary;
using System.Text;

namespace CodeSign.MachO.Commands
{
    public partial class Section64Header
    {
        public const int BinarySize = 80;

        public static Section64Header Read(ReadOnlySpan<byte> buffer, bool isLittleEndian)
        {
            if (isLittleEndian)
            {
                return new Section64Header
                {
                    SectionName = Encoding.UTF8.GetString(buffer.SliceZeroTerminated(0, 16)),
                    SegmentName = Encoding.UTF8.GetString(buffer.SliceZeroTerminated(16, 16)),
                    Address = BinaryPrimitives.ReadUInt64LittleEndian(buffer.Slice(32, 8)),
                    Size = BinaryPrimitives.ReadUInt64LittleEndian(buffer.Slice(40, 8)),
                    FileOffset = BinaryPrimitives.ReadUInt32LittleEndian(buffer.Slice(48, 4)),
                    Alignment = BinaryPrimitives.ReadUInt32LittleEndian(buffer.Slice(52, 4)),
                    RelocationOffset = BinaryPrimitives.ReadUInt32LittleEndian(buffer.Slice(56, 4)),
                    NumberOfReloationEntries = BinaryPrimitives.ReadUInt32LittleEndian(buffer.Slice(60, 4)),
                    Flags = BinaryPrimitives.ReadUInt32LittleEndian(buffer.Slice(64, 4)),
                    Reserved1 = BinaryPrimitives.ReadUInt32LittleEndian(buffer.Slice(68, 4)),
                    Reserved2 = BinaryPrimitives.ReadUInt32LittleEndian(buffer.Slice(72, 4)),
                    Reserved3 = BinaryPrimitives.ReadUInt32LittleEndian(buffer.Slice(76, 4)),
                };
            }
            else
            {
                return new Section64Header
                {
                    SectionName = Encoding.UTF8.GetString(buffer.SliceZeroTerminated(0, 16)),
                    SegmentName = Encoding.UTF8.GetString(buffer.SliceZeroTerminated(16, 16)),
                    Address = BinaryPrimitives.ReadUInt64BigEndian(buffer.Slice(32, 8)),
                    Size = BinaryPrimitives.ReadUInt64BigEndian(buffer.Slice(40, 8)),
                    FileOffset = BinaryPrimitives.ReadUInt32BigEndian(buffer.Slice(48, 4)),
                    Alignment = BinaryPrimitives.ReadUInt32BigEndian(buffer.Slice(52, 4)),
                    RelocationOffset = BinaryPrimitives.ReadUInt32BigEndian(buffer.Slice(56, 4)),
                    NumberOfReloationEntries = BinaryPrimitives.ReadUInt32BigEndian(buffer.Slice(60, 4)),
                    Flags = BinaryPrimitives.ReadUInt32BigEndian(buffer.Slice(64, 4)),
                    Reserved1 = BinaryPrimitives.ReadUInt32BigEndian(buffer.Slice(68, 4)),
                    Reserved2 = BinaryPrimitives.ReadUInt32BigEndian(buffer.Slice(72, 4)),
                    Reserved3 = BinaryPrimitives.ReadUInt32BigEndian(buffer.Slice(76, 4)),
                };
            }
        }

        // TODO: Write
    }
}