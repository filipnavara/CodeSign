using CodeSign.Streams;

namespace CodeSign.MachO
{
    public class MachO
    {
        private readonly Stream stream;

        public MachO(FatArchHeader? fatArchHeader, IMachHeader machHeader, Stream stream)
        {
            FatArchHeader = fatArchHeader;
            MachHeader = machHeader;
            this.stream = stream;
        }

        public FatArchHeader? FatArchHeader { get; private set; }

        public IMachHeader MachHeader { get; private set; }

        public Stream GetStream()
        {
            return stream.Slice(0, stream.Length);
        }
    }
}