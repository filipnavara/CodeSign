using System;
using System.Buffers.Binary;
using System.Formats.Asn1;
using System.Security;
using System.Security.Cryptography;
using System.Security.Cryptography.Pkcs;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using Claunia.PropertyList;

namespace CodeSign.Signing
{
    class CmsWrapperBlob
    {
        //private readonly X509Certificate2Collection certificatesList;

        //public X509Certificate2 DeveloperCertificate { get; private set; }

        private byte[] content;

        private readonly static string incRootCertificatePath = "CodeSign.Data.RootCertificate.cer";
        private readonly static string g1IntermediateCertificatePath = "CodeSign.Data.IntermediateG1Certificate.cer";
        private readonly static string g3IntermediateCertificatePath = "CodeSign.Data.IntermediateG3Certificate.cer";

        private static X509Certificate2 GetManifestCertificate(string name)
        {
            var memoryStream = new MemoryStream();
            using (var manifestStream = typeof(CmsWrapperBlob).Assembly.GetManifestResourceStream(name))
                manifestStream.CopyTo(memoryStream);
            return new X509Certificate2(memoryStream.ToArray());
        }

        public static CmsWrapperBlob Create(
            X509Certificate2 developerCertificate,
            byte[] dataToSign,
            byte[] sha1CodeDirectoryHash,
            byte[] sha256CodeDirectoryHash)
        {
            if (developerCertificate == null)
                throw new ArgumentNullException(nameof(developerCertificate));
            if (dataToSign == null)
                throw new ArgumentNullException(nameof(dataToSign));
            if (sha1CodeDirectoryHash == null)
                throw new ArgumentNullException(nameof(sha1CodeDirectoryHash));
            if (sha256CodeDirectoryHash == null)
                throw new ArgumentNullException(nameof(sha256CodeDirectoryHash));

            X509Certificate2Collection certificatesList;

            // TODO: Add full chain
            certificatesList = new X509Certificate2Collection();
            certificatesList.Add(developerCertificate);
            if (developerCertificate.Issuer == "C=US, O=Apple Inc., OU=G3, CN=Apple Worldwide Developer Relations Certification Authority")
                certificatesList.Add(GetManifestCertificate(g3IntermediateCertificatePath));
            else if (developerCertificate.Issuer == "C=US, O=Apple Inc., OU=Apple Worldwide Developer Relations, CN=Apple Worldwide Developer Relations Certification Authority")
                certificatesList.Add(GetManifestCertificate(g1IntermediateCertificatePath));
            else
                throw new NotImplementedException();
            certificatesList.Add(GetManifestCertificate(incRootCertificatePath));

            var cmsSigner = new CmsSigner(developerCertificate);
            cmsSigner.Certificates.AddRange(certificatesList);
            cmsSigner.IncludeOption = X509IncludeOption.None;

            cmsSigner.SignedAttributes.Add(new Pkcs9SigningTime());

            // DER version of the hash attribute
            var codeDirectorySha256AttrWriter = new AsnWriter(AsnEncodingRules.DER);
            using (codeDirectorySha256AttrWriter.PushSequence())
            {
                codeDirectorySha256AttrWriter.WriteObjectIdentifier("2.16.840.1.101.3.4.2.1"); // SHA-256
                codeDirectorySha256AttrWriter.WriteOctetString(sha256CodeDirectoryHash);
            }
            var codeDirectorySha1AttrWriter = new AsnWriter(AsnEncodingRules.DER);
            using (codeDirectorySha1AttrWriter.PushSequence())
            {
                codeDirectorySha1AttrWriter.WriteObjectIdentifier("1.3.14.3.2.26"); // SHA-1
                codeDirectorySha1AttrWriter.WriteOctetString(sha1CodeDirectoryHash);
            }
            var values = new AsnEncodedDataCollection();
            var oid = new Oid("1.2.840.113635.100.9.2", null);
            values.Add(new AsnEncodedData(oid, codeDirectorySha256AttrWriter.Encode()));
            values.Add(new AsnEncodedData(oid, codeDirectorySha1AttrWriter.Encode()));
            cmsSigner.SignedAttributes.Add(new CryptographicAttributeObject(oid, values));

            // PList version of the hash attribute
            var plistBytes = Encoding.UTF8.GetBytes(
                new NSDictionary()
                {
                    ["cdhashes"] = new NSArray()
                    {
                        new NSData(sha1CodeDirectoryHash),
                        new NSData(sha256CodeDirectoryHash.AsSpan(0, /*HashSize.Sha1*/20).ToArray())
                    }
                }.ToXmlPropertyList());
            var codeDirectoryPListAttrWriter = new AsnWriter(AsnEncodingRules.DER);
            codeDirectoryPListAttrWriter.WriteOctetString(plistBytes);
            cmsSigner.SignedAttributes.Add(new AsnEncodedData("1.2.840.113635.100.9.1", codeDirectoryPListAttrWriter.Encode()));

            ///cmsSigner.SignedAttributes.Add(new System.Security.Cryptography.CryptographicAttributeObject()

            var signedCms = new SignedCms(new ContentInfo(dataToSign), true);
            signedCms.ComputeSignature(cmsSigner);

            var encodedCms = signedCms.Encode();

            var blobBuffer = new byte[8 + encodedCms.Length];
            BinaryPrimitives.WriteUInt32BigEndian(blobBuffer.AsSpan(0, 4), (uint)CodeSigningBlobMagic.CmsWrapper);
            BinaryPrimitives.WriteUInt32BigEndian(blobBuffer.AsSpan(4, 4), (uint)blobBuffer.Length);
            encodedCms.CopyTo(blobBuffer.AsSpan(8));

            return new CmsWrapperBlob { content = blobBuffer };
        }

        public byte[] Emit()
        {
            return content;
        }
    }
}
