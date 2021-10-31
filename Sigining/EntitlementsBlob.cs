using System.Buffers.Binary;
using System.Text;
using Claunia.PropertyList;

namespace CodeSign.Signing
{
    class EntitlementsBlob
    {
        /*public EntitlementsBlob(byte[])
        {
        }*/

        byte[] content;

        public static EntitlementsBlob Create(NSDictionary entitlements)
        {
            var plistBytes = Encoding.UTF8.GetBytes(entitlements.ToXmlPropertyList());
            var blobBuffer = new byte[8 + plistBytes.Length];

            BinaryPrimitives.WriteUInt32BigEndian(blobBuffer.AsSpan(0, 4), (uint)CodeSigningBlobMagic.Entitlements);
            BinaryPrimitives.WriteUInt32BigEndian(blobBuffer.AsSpan(4, 4), (uint)blobBuffer.Length);
            plistBytes.CopyTo(blobBuffer.AsSpan(8, plistBytes.Length));

            return new EntitlementsBlob { content = blobBuffer };
        }

        public byte[] Emit()
        {
            return content;
        }

        public int Size => content.Length;
    }
}