using System.Buffers.Binary;
using System.Text;
using Claunia.PropertyList;

namespace CodeSign.Signing
{
    class EntitlementsDerBlob
    {
        /*public EntitlementsDerBlob(byte[])
        {
        }*/

        byte[] content;

        public static EntitlementsDerBlob Create(NSDictionary entitlements)
        {
            var plistBytes = DerPropertyListWriter.Write(entitlements);
            var blobBuffer = new byte[8 + plistBytes.Length];

            BinaryPrimitives.WriteUInt32BigEndian(blobBuffer.AsSpan(0, 4), (uint)CodeSigningBlobMagic.EntitlementsDer);
            BinaryPrimitives.WriteUInt32BigEndian(blobBuffer.AsSpan(4, 4), (uint)blobBuffer.Length);
            plistBytes.CopyTo(blobBuffer.AsSpan(8, plistBytes.Length));

            return new EntitlementsDerBlob { content = blobBuffer };
        }

        public byte[] Emit()
        {
            return content;
        }

        public int Size => content.Length;
    }
}