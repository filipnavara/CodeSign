using System.Buffers.Binary;
using System.Text;

namespace CodeSign.MachO.Commands
{
    public partial class LinkEditHeader
    {
        public const int BinarySize = 8;

        public static LinkEditHeader Read(ReadOnlySpan<byte> buffer, bool isLittleEndian)
        {
            if (isLittleEndian)
            {
                return new LinkEditHeader
                {
                    FileOffset = BinaryPrimitives.ReadUInt32LittleEndian(buffer.Slice(0, 4)),
                    FileSize = BinaryPrimitives.ReadUInt32LittleEndian(buffer.Slice(4, 4)),
                };
            }
            else
            {
                return new LinkEditHeader
                {
                    FileOffset = BinaryPrimitives.ReadUInt32BigEndian(buffer.Slice(0, 4)),
                    FileSize = BinaryPrimitives.ReadUInt32BigEndian(buffer.Slice(4, 4)),
                };
            }
        }

        // TODO: Write
    }
}