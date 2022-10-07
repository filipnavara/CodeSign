using System.Buffers.Binary;

namespace Melanzana.MachO
{
    public class MachRelocationWriter : IDisposable
    {
        private MachObjectFile objectFile;
        private MachSection section;
        private Stream relocationStream;

        internal MachRelocationWriter(MachObjectFile objectFile, MachSection section, MachLinkEditData relocationData)
        {
            this.objectFile = objectFile;
            this.section = section;
            this.relocationStream = relocationData.GetWriteStream();
        }

        public void Dispose()
        {
            this.relocationStream.Dispose();
        }

        public void AddRelocation(MachRelocation relocation)
        {
            Span<byte> relocationBuffer = stackalloc byte[8];
            uint info;

            info = relocation.SymbolOrSectionIndex;
            info |= relocation.IsPCRelative ? 0x1_00_00_00u : 0u;
            info |= relocation.Length switch { 1 => 0u << 25, 2 => 1u << 25, 4 => 2u << 25, _ => 3u << 25 };
            info |= relocation.IsExternal ? 0x8_00_00_00u : 0u;
            info |= (uint)relocation.RelocationType << 28;

            if (objectFile.IsLittleEndian)
            {
                BinaryPrimitives.WriteInt32LittleEndian(relocationBuffer, relocation.Address);
                BinaryPrimitives.WriteUInt32LittleEndian(relocationBuffer, info);
            }
            else
            {
                BinaryPrimitives.WriteInt32BigEndian(relocationBuffer, relocation.Address);
                BinaryPrimitives.WriteUInt32BigEndian(relocationBuffer, info);
            }

            this.relocationStream.Write(relocationBuffer);
        }
    }
}