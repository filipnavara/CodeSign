using System.Buffers.Binary;
using System.Text;

namespace CodeSign.MachO.Commands
{
    public partial class Segment64Header
    {
        public const int BinarySize = 64;

        public static Segment64Header Read(ReadOnlySpan<byte> buffer, bool isLittleEndian)
        {
            if (isLittleEndian)
            {
                return new Segment64Header
                {
                    Name = Encoding.UTF8.GetString(buffer.SliceZeroTerminated(0, 16)),
                    Address = BinaryPrimitives.ReadUInt64LittleEndian(buffer.Slice(16, 8)),
                    Size = BinaryPrimitives.ReadUInt64LittleEndian(buffer.Slice(24, 8)),
                    FileOffset = BinaryPrimitives.ReadUInt64LittleEndian(buffer.Slice(32, 8)),
                    FileSize = BinaryPrimitives.ReadUInt64LittleEndian(buffer.Slice(40, 8)),
                    MaximalProtection = (VmProtection)BinaryPrimitives.ReadUInt32LittleEndian(buffer.Slice(48, 4)),
                    InitialProtection = (VmProtection)BinaryPrimitives.ReadUInt32LittleEndian(buffer.Slice(52, 4)),
                    NumberOfSections = BinaryPrimitives.ReadUInt32LittleEndian(buffer.Slice(56, 4)),
                    Flags = BinaryPrimitives.ReadUInt32LittleEndian(buffer.Slice(60, 4)),
                };
            }
            else
            {
                return new Segment64Header
                {
                    Name = Encoding.UTF8.GetString(buffer.SliceZeroTerminated(0, 16)),
                    Address = BinaryPrimitives.ReadUInt64BigEndian(buffer.Slice(16, 8)),
                    Size = BinaryPrimitives.ReadUInt64BigEndian(buffer.Slice(24, 8)),
                    FileOffset = BinaryPrimitives.ReadUInt64BigEndian(buffer.Slice(32, 8)),
                    FileSize = BinaryPrimitives.ReadUInt64BigEndian(buffer.Slice(40, 8)),
                    MaximalProtection = (VmProtection)BinaryPrimitives.ReadUInt32BigEndian(buffer.Slice(48, 4)),
                    InitialProtection = (VmProtection)BinaryPrimitives.ReadUInt32BigEndian(buffer.Slice(52, 4)),
                    NumberOfSections = BinaryPrimitives.ReadUInt32BigEndian(buffer.Slice(56, 4)),
                    Flags = BinaryPrimitives.ReadUInt32BigEndian(buffer.Slice(60, 4)),
                };
            }
        }

        // TODO: Write
    }
}