using System.Text;

namespace Melanzana.MachO.Commands
{
    /// <summary>
    /// Represents 16-byte null-terminated UTF-8 string
    /// </summary>
    public class MachFixedName
    {
        public const int BinarySize = 16;

        public static MachFixedName Empty = new MachFixedName("");

        public MachFixedName(string name)
        {
            Name = name;
        }

        public static MachFixedName Read(ReadOnlySpan<byte> buffer, out int bytesRead)
        {
            bytesRead = BinarySize;
            var slice = buffer.Slice(0, BinarySize);
            var zeroIndex = slice.IndexOf((byte)0);
            if (zeroIndex >= 0)
                return new MachFixedName(Encoding.UTF8.GetString(slice.Slice(0, zeroIndex)));
            return new MachFixedName(Encoding.UTF8.GetString(slice));
        }

        public void Write(Span<byte> buffer, out int bytesWritten)
        {
            // FIXME: Write this correctly
            byte[] utf8Name = new byte[16];
            Encoding.UTF8.GetBytes(Name, utf8Name);
            utf8Name.CopyTo(buffer.Slice(0, 16));
            bytesWritten = 16;
        }

        public string Name { get; init; }

        public static implicit operator string(MachFixedName n) => n.Name;
        public static implicit operator MachFixedName(string n) => new MachFixedName(n);

        public override string ToString() => Name;
    }
}