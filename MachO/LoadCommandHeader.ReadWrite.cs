using System.Buffers.Binary;

namespace CodeSign.MachO
{
    public partial class LoadCommandHeader
    {
        public const int BinarySize = 8;

        public static LoadCommandHeader Read(ReadOnlySpan<byte> buffer, bool isLittleEndian)
        {
            if (isLittleEndian)
            {
                return new LoadCommandHeader
                {
                    CommandType = (LoadCommandType)BinaryPrimitives.ReadUInt32LittleEndian(buffer.Slice(0, 4)),
                    CommandSize = BinaryPrimitives.ReadUInt32LittleEndian(buffer.Slice(4, 4)),
                };
            }
            else
            {
                return new LoadCommandHeader
                {
                    CommandType = (LoadCommandType)BinaryPrimitives.ReadUInt32BigEndian(buffer.Slice(0, 4)),
                    CommandSize = BinaryPrimitives.ReadUInt32BigEndian(buffer.Slice(4, 4)),
                };
            }
        }

        public void Write(Span<byte> buffer, bool isLittleEndian)
        {
            if (isLittleEndian)
            {
                BinaryPrimitives.WriteUInt32LittleEndian(buffer.Slice(0, 4), (uint)this.CommandType);
                BinaryPrimitives.WriteUInt32LittleEndian(buffer.Slice(4, 4), this.CommandSize);
            }
            else
            {
                BinaryPrimitives.WriteUInt32BigEndian(buffer.Slice(0, 4), (uint)this.CommandType);
                BinaryPrimitives.WriteUInt32BigEndian(buffer.Slice(4, 4), this.CommandSize);
            }
        }
    }
}