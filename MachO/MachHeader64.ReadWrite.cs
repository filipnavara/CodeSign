using System.Buffers.Binary;

namespace CodeSign.MachO
{
    public partial class MachHeader64
    {
        public const int BinarySize = 28;

        public static MachHeader64 Read(ReadOnlySpan<byte> buffer, bool isLittleEndian)
        {
            if (isLittleEndian)
            {
                return new MachHeader64
                {
                    CpuType = (MachCpuType)BinaryPrimitives.ReadUInt32LittleEndian(buffer.Slice(0, 4)),
                    CpuSubType = BinaryPrimitives.ReadUInt32LittleEndian(buffer.Slice(4, 4)),
                    FileType = (MachFileType)BinaryPrimitives.ReadUInt32LittleEndian(buffer.Slice(8, 4)),
                    NumberOfCommands = BinaryPrimitives.ReadUInt32LittleEndian(buffer.Slice(12, 4)),
                    SizeOfCommands = BinaryPrimitives.ReadUInt32LittleEndian(buffer.Slice(16, 4)),
                    Flags = (MachHeaderFlags)BinaryPrimitives.ReadUInt32LittleEndian(buffer.Slice(20, 4)),
                    Reserved = BinaryPrimitives.ReadUInt32LittleEndian(buffer.Slice(24, 4)),
                };
            }
            else
            {
                return new MachHeader64
                {
                    CpuType = (MachCpuType)BinaryPrimitives.ReadUInt32BigEndian(buffer.Slice(0, 4)),
                    CpuSubType = BinaryPrimitives.ReadUInt32BigEndian(buffer.Slice(4, 4)),
                    FileType = (MachFileType)BinaryPrimitives.ReadUInt32BigEndian(buffer.Slice(8, 4)),
                    NumberOfCommands = BinaryPrimitives.ReadUInt32BigEndian(buffer.Slice(12, 4)),
                    SizeOfCommands = BinaryPrimitives.ReadUInt32BigEndian(buffer.Slice(16, 4)),
                    Flags = (MachHeaderFlags)BinaryPrimitives.ReadUInt32BigEndian(buffer.Slice(20, 4)),
                    Reserved = BinaryPrimitives.ReadUInt32BigEndian(buffer.Slice(24, 4)),
                };
            }
        }

        // TODO: Write
    }
}