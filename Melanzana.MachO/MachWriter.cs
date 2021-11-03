/*using System.Buffers.Binary;

namespace CodeSign.MachO
{
    public static class MachWriter
    {
        public static void Write(MachO machO, Stream stream)
        {
            var machMagic = new byte[4];
            var machHeaderBuffer = new byte[MachHeader64.BinarySize];
            // TODO: Allow explicit endianness switch?
            BinaryPrimitives.WriteUInt64BigEndian(machMagic, machO.Header is MachHeader64 ? (uint)MachMagic.MachHeader64BigEndian : (uint)MachMagic.MachHeaderBigEndian);

            // TODO: Update command sizes here or...?
            uint numberOfCommands = 0;
            uint sizeOfCommands = 0;
            foreach (var loadCommand in machO.LoadCommands)
            {
                numberOfCommands++;
                sizeOfCommands += loadCommand.Header.CommandSize;
            }
            machO.Header.NumberOfCommands = numberOfCommands;
            machO.Header.SizeOfCommands = sizeOfCommands;

            // Write updated header
            stream.Write(machMagic);
            machO.Header.Write(machHeaderBuffer, out var bytesWritten);
            stream.Write(machHeaderBuffer.AsSpan(0, bytesWritten));

            // Write load commands

            // Write header padding

            // Write segments
        }
    }
}
*/