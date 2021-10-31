using System.Buffers.Binary;

namespace CodeSign.MachO
{
    public partial class MachHeader
    {
        public const int BinarySize = 24;

        public static MachHeader Read(ReadOnlySpan<byte> buffer, bool isLittleEndian)
        {
            if (isLittleEndian)
            {
                return new MachHeader
                {
                    CpuType = (MachCpuType)BinaryPrimitives.ReadUInt32LittleEndian(buffer.Slice(0, 4)),
                    CpuSubType = BinaryPrimitives.ReadUInt32LittleEndian(buffer.Slice(4, 4)),
                    FileType = (MachFileType)BinaryPrimitives.ReadUInt32LittleEndian(buffer.Slice(8, 4)),
                    NumberOfCommands = BinaryPrimitives.ReadUInt32LittleEndian(buffer.Slice(12, 4)),
                    SizeOfCommands = BinaryPrimitives.ReadUInt32LittleEndian(buffer.Slice(16, 4)),
                    Flags = (MachHeaderFlags)BinaryPrimitives.ReadUInt32LittleEndian(buffer.Slice(20, 4)),
                };
            }
            else
            {
                return new MachHeader
                {
                    CpuType = (MachCpuType)BinaryPrimitives.ReadUInt32BigEndian(buffer.Slice(0, 4)),
                    CpuSubType = BinaryPrimitives.ReadUInt32BigEndian(buffer.Slice(4, 4)),
                    FileType = (MachFileType)BinaryPrimitives.ReadUInt32BigEndian(buffer.Slice(8, 4)),
                    NumberOfCommands = BinaryPrimitives.ReadUInt32BigEndian(buffer.Slice(12, 4)),
                    SizeOfCommands = BinaryPrimitives.ReadUInt32BigEndian(buffer.Slice(16, 4)),
                    Flags = (MachHeaderFlags)BinaryPrimitives.ReadUInt32BigEndian(buffer.Slice(20, 4)),
                };
            }
        }

        // TODO: Write
    }
}