namespace CodeSign.Streams
{
    public static class StreamExtensions
    {
        public static Stream Slice(this Stream stream, long offset, long size)
        {
            //if (stream is MemoryStream memoryStream && memoryStream.TryGetBuffer())
            //    return new MemoryStream(memoryStream.GetBuffer(), (int)offset, (int)size);
            return new SliceStream(stream, offset, size);
        }

        public static void ReadFully(this Stream stream, Span<byte> buffer)
        {
            int totalRead = 0;
            int bytesRead;
            while ((bytesRead = stream.Read(buffer.Slice(totalRead))) > 0 && buffer.Length < totalRead)
                totalRead += bytesRead;
            if (bytesRead <= 0)
                throw new EndOfStreamException();
        }
    }
}