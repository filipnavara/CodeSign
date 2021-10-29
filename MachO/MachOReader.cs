using System.Buffers.Binary;
using CodeSign.Streams;

namespace CodeSign.MachO
{
    public static class MachOReader
    {
        private static MachO ReadSingle(FatArchHeader? fatArchHeader, MachMagic magic, Stream stream)
        {
            Span<byte> headerBuffer = stackalloc byte[Math.Max(MachHeader.BinarySize, MachHeader64.BinarySize)];

            switch (magic)
            {
                case MachMagic.MachHeaderLitteEndian:
                case MachMagic.MachHeaderBigEndian:
                    stream.Read(headerBuffer.Slice(0, MachHeader.BinarySize));
                    return new MachO(null, MachHeader.Read(headerBuffer, isLittleEndian: magic == MachMagic.MachHeaderLitteEndian), stream);

                case MachMagic.MachHeader64LitteEndian:
                case MachMagic.MachHeader64BigEndian:
                    stream.Read(headerBuffer.Slice(0, MachHeader64.BinarySize));
                    return new MachO(null, MachHeader64.Read(headerBuffer, isLittleEndian: magic == MachMagic.MachHeaderLitteEndian), stream);
            }

            throw new NotSupportedException();
        }

        public static IEnumerable<MachO> Read(Stream stream)
        {
            var magicBuffer = new byte[4];
            stream.ReadFully(magicBuffer);

            var magic = (MachMagic)BinaryPrimitives.ReadUInt32BigEndian(magicBuffer);
            if (magic == MachMagic.FatMagicLittleEndian || magic == MachMagic.FatMagicBigEndian)
            {
                var headerBuffer = new byte[Math.Max(FatHeader.BinarySize, FatArchHeader.BinarySize)];
                stream.ReadFully(headerBuffer.AsSpan(0, FatHeader.BinarySize));
                var fatHeader = FatHeader.Read(headerBuffer, isLittleEndian: magic == MachMagic.FatMagicLittleEndian);
                for (int i = 0; i < fatHeader.NumberOfFatArchitectures; i++)
                {
                    stream.ReadFully(headerBuffer.AsSpan(0, FatArchHeader.BinarySize));
                    var fatArchHeader = FatArchHeader.Read(headerBuffer, isLittleEndian: magic == MachMagic.FatMagicLittleEndian);

                    var machOSlice = stream.Slice(fatArchHeader.Offset, fatArchHeader.Size);
                    machOSlice.ReadFully(magicBuffer);
                    magic = (MachMagic)BinaryPrimitives.ReadUInt32BigEndian(magicBuffer);
                    yield return ReadSingle(fatArchHeader, magic, machOSlice);
                }
            }
            else
            {
                yield return ReadSingle(null, magic, stream);
            }
        }
    }
}