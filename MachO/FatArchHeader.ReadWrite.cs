using System.Buffers.Binary;

namespace CodeSign.MachO
{
    public partial class FatArchHeader
    {
        public const int BinarySize = 20;

        public static FatArchHeader Read(Span<byte> buffer, bool isLittleEndian)
        {
            if (isLittleEndian)
            {
                return new FatArchHeader
                {
                    CpuType = (MachCpuType)BinaryPrimitives.ReadUInt32LittleEndian(buffer.Slice(0, 4)),
                    CpuSubType = BinaryPrimitives.ReadUInt32LittleEndian(buffer.Slice(4, 4)),
                    Offset = BinaryPrimitives.ReadUInt32LittleEndian(buffer.Slice(8, 4)),
                    Size = BinaryPrimitives.ReadUInt32LittleEndian(buffer.Slice(12, 4)),
                    Alignment = BinaryPrimitives.ReadUInt32LittleEndian(buffer.Slice(16, 4)),
                };
            }
            else
            {
                return new FatArchHeader
                {
                    CpuType = (MachCpuType)BinaryPrimitives.ReadUInt32BigEndian(buffer.Slice(0, 4)),
                    CpuSubType = BinaryPrimitives.ReadUInt32BigEndian(buffer.Slice(4, 4)),
                    Offset = BinaryPrimitives.ReadUInt32BigEndian(buffer.Slice(8, 4)),
                    Size = BinaryPrimitives.ReadUInt32BigEndian(buffer.Slice(12, 4)),
                    Alignment = BinaryPrimitives.ReadUInt32BigEndian(buffer.Slice(16, 4)),
                };
            }
        }

        public void Write(Span<byte> buffer, bool isLittleEndian)
        {
            if (isLittleEndian)
            {
                BinaryPrimitives.WriteUInt32LittleEndian(buffer.Slice(0, 4), (uint)this.CpuType);
                BinaryPrimitives.WriteUInt32LittleEndian(buffer.Slice(4, 4), this.CpuSubType);
                BinaryPrimitives.WriteUInt32LittleEndian(buffer.Slice(8, 4), this.Offset);
                BinaryPrimitives.WriteUInt32LittleEndian(buffer.Slice(12, 4), this.Size);
                BinaryPrimitives.WriteUInt32LittleEndian(buffer.Slice(16, 4), this.Alignment);
            }
            else
            {
                BinaryPrimitives.WriteUInt32BigEndian(buffer.Slice(0, 4), (uint)this.CpuType);
                BinaryPrimitives.WriteUInt32BigEndian(buffer.Slice(4, 4), this.CpuSubType);
                BinaryPrimitives.WriteUInt32BigEndian(buffer.Slice(8, 4), this.Offset);
                BinaryPrimitives.WriteUInt32BigEndian(buffer.Slice(12, 4), this.Size);
                BinaryPrimitives.WriteUInt32BigEndian(buffer.Slice(16, 4), this.Alignment);
            }
        }
    }
}