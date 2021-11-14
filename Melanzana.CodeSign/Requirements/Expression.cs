using System.Buffers.Binary;
using System.Text;
using System.Formats.Asn1;

namespace Melanzana.CodeSign.Requirements
{
    public abstract class Expression
    {
        public abstract int Size { get; }

        public abstract void Write(Span<byte> buffer, out int bytesWritten);

        public static Expression False { get; } = new SimpleExpression(ExpressionOperation.False);
        public static Expression True { get; } = new SimpleExpression(ExpressionOperation.True);
        public static Expression Ident(string identifier) => new StringExpression(ExpressionOperation.Ident, identifier);
        public static Expression AppleAnchor { get; } = new SimpleExpression(ExpressionOperation.AppleAnchor);
        public static Expression AnchorHash(int certificateIndex, byte[] anchorHash) => new AnchorHashExpression(certificateIndex, anchorHash);
        // InfoKeyValue
        public static Expression And(Expression left, Expression right) => new BinaryOperatorExpression(ExpressionOperation.And, left, right);
        public static Expression Or(Expression left, Expression right) => new BinaryOperatorExpression(ExpressionOperation.Or, left, right);
        public static Expression CDHash(byte[] codeDirectoryHash) => new CDHashExpression(codeDirectoryHash);
        public static Expression Not(Expression inner) => new UnaryOperatorExpression(ExpressionOperation.Not, inner);
        public static Expression InfoKeyField(string field, ExpressionMatchType matchType, string? matchValue = null)
            => new FieldMatchExpression(ExpressionOperation.InfoKeyField, field, matchType, matchValue != null ? Encoding.ASCII.GetBytes(matchValue) : null);
        public static Expression CertField(int certificateIndex, string certificateField, ExpressionMatchType matchType, string? matchValue = null)
            => new CertExpression(ExpressionOperation.CertField, certificateIndex, certificateField, matchType, matchValue != null ? Encoding.ASCII.GetBytes(matchValue) : null);
        // TrustedCert,
        public static Expression TrustedCerts { get; } = new SimpleExpression(ExpressionOperation.TrustedCerts);
        public static Expression CertGeneric(int certificateIndex, string certificateFieldOid, ExpressionMatchType matchType, string? matchValue = null)
            => new CertExpression(ExpressionOperation.CertGeneric, certificateIndex, GetOidBytes(certificateFieldOid), matchType, matchValue != null ? Encoding.ASCII.GetBytes(matchValue) : null);
        public static Expression AppleGenericAnchor { get; } = new SimpleExpression(ExpressionOperation.AppleGenericAnchor);
        public static Expression EntitlementField(string field, ExpressionMatchType matchType, string? matchValue = null)
            => new FieldMatchExpression(ExpressionOperation.EntitlementField, field, matchType, matchValue != null ? Encoding.ASCII.GetBytes(matchValue) : null);
        public static Expression CertPolicy(int certificateIndex, string certificateFieldOid, ExpressionMatchType matchType, string? matchValue = null)
            => new CertExpression(ExpressionOperation.CertPolicy, certificateIndex, GetOidBytes(certificateFieldOid), matchType, matchValue != null ? Encoding.ASCII.GetBytes(matchValue) : null);
        // NamedAnchor,
        // NamedCode,
        // Platform,
        public static Expression Notarized { get; } = new SimpleExpression(ExpressionOperation.Notarized);
        // CertFieldDate,
        public static Expression LegacyDevID { get; } = new SimpleExpression(ExpressionOperation.LegacyDevID);

        private static int Align(int size) => (size + 3) & ~3;

        private static byte[] GetOidBytes(string oid)
        {
            var asnWriter = new AsnWriter(AsnEncodingRules.DER);
            asnWriter.WriteObjectIdentifier(oid);
            return asnWriter.Encode().AsSpan(2).ToArray();
        }

        private static string GetOidString(byte[] oid)
        {
            var oidBytes = new byte[oid.Length + 2];
            oidBytes[0] = 6;
            oidBytes[1] = (byte)oid.Length;
            oid.CopyTo(oidBytes.AsSpan(2));
            return AsnDecoder.ReadObjectIdentifier(oidBytes, AsnEncodingRules.DER, out _);
        }

        private static string BinaryValueToString(byte[] bytes)
        {
            return $"0x{Convert.ToHexString(bytes)}";
        }

        private static string ValueToString(byte[] bytes)
        {
            bool isPrintable = bytes.All(c => !char.IsControl((char)c) && char.IsAscii((char)c));
            if (!isPrintable)
            {
                return BinaryValueToString(bytes);
            }

            bool needQuoting =
                bytes.Length == 0 ||
                char.IsDigit((char)bytes[0]) ||
                bytes.Any(c => !char.IsLetterOrDigit((char)c));
            if (needQuoting)
            {
                var sb = new StringBuilder();
                sb.Append('"');
                foreach (var c in bytes)
                {
                    if (c == (byte)'\\' || c == (byte)'"')
                    {
                        sb.Append('\\');
                    }
                    sb.Append((char)c);
                }
                sb.Append('"');
                return sb.ToString();
            }
            else
            {
                return Encoding.ASCII.GetString(bytes);
            }
        }

        private static string CertificateSlotToString(int slot)
        {
            return slot switch {
                0 => "leaf",
                -1 => "root",
                _ => slot.ToString(),
            };
        }

        class SimpleExpression : Expression
        {
            ExpressionOperation op;

            public SimpleExpression(ExpressionOperation op)
            {
                this.op = op;
            }

            public override int Size => 4;

            public override void Write(Span<byte> buffer, out int bytesWritten)
            {
                BinaryPrimitives.WriteUInt32BigEndian(buffer, (uint)op);
                bytesWritten = 4;
            }

            public override string ToString()
            {
                return op switch {
                    ExpressionOperation.False => "never",
                    ExpressionOperation.True => "always",
                    ExpressionOperation.AppleAnchor => "apple anchor",
                    ExpressionOperation.AppleGenericAnchor => "apple generic anchor",
                    ExpressionOperation.TrustedCerts => "anchor trusted",
                    ExpressionOperation.Notarized => "notarized",
                    ExpressionOperation.LegacyDevID => "legacy",
                    _ => "unknown",
                };
            }
        }

        class BinaryOperatorExpression : Expression
        {
            ExpressionOperation op;
            Expression left;
            Expression right;

            public BinaryOperatorExpression(ExpressionOperation op, Expression left, Expression right)
            {
                this.op = op;
                this.left = left;
                this.right = right;
            }

            public override int Size => 4 + left.Size + right.Size;

            public override void Write(Span<byte> buffer, out int bytesWritten)
            {
                BinaryPrimitives.WriteUInt32BigEndian(buffer, (uint)op);
                left.Write(buffer.Slice(4), out var bytesWrittenLeft);
                right.Write(buffer.Slice(4 + bytesWrittenLeft), out var bytesWrittenRight);
                bytesWritten = 4 + bytesWrittenLeft + bytesWrittenRight;
            }

            private string? WrapInnerExpression(Expression innerExpression)
            {
                if (innerExpression is BinaryOperatorExpression boe &&
                    boe.op != op)
                {
                    return $"({boe})";
                }

                return innerExpression.ToString();
            }

            public override string ToString()
            {
                return op switch {
                    ExpressionOperation.And => $"{WrapInnerExpression(left)} and {WrapInnerExpression(right)}",
                    ExpressionOperation.Or => $"{WrapInnerExpression(left)} or {WrapInnerExpression(right)}",
                    _ => "unknown",
                };
            }
        }

        class UnaryOperatorExpression : Expression
        {
            ExpressionOperation op;
            Expression inner;

            public UnaryOperatorExpression(ExpressionOperation op, Expression inner)
            {
                this.op = op;
                this.inner = inner;
            }

            public override int Size => 4 + inner.Size;

            public override void Write(Span<byte> buffer, out int bytesWritten)
            {
                BinaryPrimitives.WriteUInt32BigEndian(buffer, (uint)op);
                inner.Write(buffer.Slice(4), out var bytesWrittenInner);
                bytesWritten = 4 + bytesWrittenInner;
            }

            public override string ToString()
            {
                return op switch {
                    ExpressionOperation.Not => $"! {inner}",
                    _ => "unknown",
                };
            }
        }

        class StringExpression : Expression
        {
            ExpressionOperation op;
            string opString;

            public StringExpression(ExpressionOperation op, string opString)
            {
                this.op = op;
                this.opString = opString;
            }

            public override int Size => 8 + Align(Encoding.UTF8.GetByteCount(opString));

            public override void Write(Span<byte> buffer, out int bytesWritten)
            {
                byte[] opStringBytes = Encoding.UTF8.GetBytes(opString);
                BinaryPrimitives.WriteUInt32BigEndian(buffer, (uint)op);
                BinaryPrimitives.WriteInt32BigEndian(buffer.Slice(4), opStringBytes.Length);
                opStringBytes.CopyTo(buffer.Slice(8, opStringBytes.Length));
                buffer.Slice(8 + opStringBytes.Length, Align(opStringBytes.Length) - opStringBytes.Length).Fill((byte)0);
                bytesWritten = 8 + Align(opStringBytes.Length);
            }

            public override string ToString()
            {
                return op switch {
                    ExpressionOperation.Ident => $"identifier \"{opString}\"", // FIXME: Escaping
                    _ => "unknown",
                };
            }
        }

        class CDHashExpression : Expression
        {
            private readonly byte[] codeDirectoryHash;

            public CDHashExpression(byte[] codeDirectoryHash)
            {
                this.codeDirectoryHash = codeDirectoryHash;
            }

            public override int Size => 4 + codeDirectoryHash.Length;

            public override void Write(Span<byte> buffer, out int bytesWritten)
            {
                BinaryPrimitives.WriteUInt32BigEndian(buffer, (uint)ExpressionOperation.CDHash);
                codeDirectoryHash.CopyTo(buffer.Slice(4, codeDirectoryHash.Length));
                bytesWritten = 4 + codeDirectoryHash.Length;
            }

            public override string ToString()
            {
                return $"cdhash H\"{Convert.ToHexString(codeDirectoryHash)}\"";
            }
        }

        class AnchorHashExpression : Expression
        {
            private readonly int certificateIndex;
            private readonly byte[] anchorHash;

            public AnchorHashExpression(int certificateIndex, byte[] anchorHash)
            {
                this.certificateIndex = certificateIndex;
                this.anchorHash = anchorHash;
            }

            public override int Size => 8 + anchorHash.Length;

            public override void Write(Span<byte> buffer, out int bytesWritten)
            {
                BinaryPrimitives.WriteUInt32BigEndian(buffer.Slice(0, 4), (uint)ExpressionOperation.AnchorHash);
                BinaryPrimitives.WriteInt32BigEndian(buffer.Slice(4, 4), anchorHash.Length);
                anchorHash.CopyTo(buffer.Slice(8, anchorHash.Length));
                bytesWritten = 8 + anchorHash.Length;
            }

            public override string ToString()
            {
                return $"certificate {CertificateSlotToString(certificateIndex)} = H\"{Convert.ToHexString(anchorHash)}\"";
            }
        }

        abstract class MatchExpression : Expression
        {
            ExpressionMatchType matchType;
            byte[]? matchValue;

            public MatchExpression(ExpressionMatchType matchType, byte[]? matchValue)
            {
                this.matchType = matchType;
                this.matchValue = matchValue;
            }

            public override int Size =>
                4 +
                (matchValue != null ? 4 + Align(matchValue.Length) : 0);

            public override void Write(Span<byte> buffer, out int bytesWritten)
            {
                BinaryPrimitives.WriteUInt32BigEndian(buffer, (uint)matchType);
                if (matchValue != null)
                {
                    BinaryPrimitives.WriteInt32BigEndian(buffer.Slice(4), matchValue.Length);
                    matchValue.CopyTo(buffer.Slice(4, matchValue.Length));
                    buffer.Slice(4 + matchValue.Length, Align(matchValue.Length) - matchValue.Length).Fill((byte)0);
                    bytesWritten = 8 + Align(matchValue.Length);
                }
                else
                {
                    bytesWritten = 4;
                }
            }

            public override string ToString()
            {
                return matchType switch {
                    ExpressionMatchType.Exists => "/* exists */",
                    ExpressionMatchType.Absent => "absent",
                    ExpressionMatchType.Equal => $"= {ValueToString(matchValue!)}",
                    ExpressionMatchType.Contains => $"~ {ValueToString(matchValue!)}",
                    ExpressionMatchType.BeginsWith => $"= {ValueToString(matchValue!)}*",
                    ExpressionMatchType.EndsWith => $"= *{ValueToString(matchValue!)}",
                    ExpressionMatchType.LessThan => $"< {ValueToString(matchValue!)}",
                    ExpressionMatchType.GreaterEqual => $">= {ValueToString(matchValue!)}",
                    ExpressionMatchType.LessEqual => $"<= {ValueToString(matchValue!)}",
                    ExpressionMatchType.GreaterThan => $">= {ValueToString(matchValue!)}",
                    //ExpressionMatchType.On => $"= {TimestampToString(matchValue!)}",
                    _ => "unknown",
                };
            }
        }

        class FieldMatchExpression : MatchExpression
        {
            private readonly ExpressionOperation op;
            private readonly object field;
            private readonly byte[] fieldBytes;

            public FieldMatchExpression(
                ExpressionOperation op,
                string field,
                ExpressionMatchType matchType,
                byte[]? matchValue)
                : base(matchType, matchValue) 
            {
                this.op = op;
                this.field = field;
                fieldBytes = Encoding.ASCII.GetBytes(field);
            }

            public override int Size =>
                8 +
                Align(fieldBytes.Length) +
                base.Size;

            public override void Write(Span<byte> buffer, out int bytesWritten)
            {
                BinaryPrimitives.WriteUInt32BigEndian(buffer.Slice(0, 4), (uint)op);
                BinaryPrimitives.WriteInt32BigEndian(buffer.Slice(4, 4), fieldBytes.Length);
                fieldBytes.CopyTo(buffer.Slice(8, fieldBytes.Length));
                buffer.Slice(8 + fieldBytes.Length, Align(fieldBytes.Length) - fieldBytes.Length).Fill((byte)0);
                bytesWritten = 8 + Align(fieldBytes.Length);
                base.Write(buffer.Slice(bytesWritten), out var matchExpressionSize);
                bytesWritten += matchExpressionSize;
            }


            public override string ToString()
            {
                return op switch {
                    ExpressionOperation.InfoKeyField => $"info[{field}] {base.ToString()}",
                    ExpressionOperation.EntitlementField => $"entitlement[{field}] {base.ToString()}",
                    _ => "unknown",
                };
            }
        }

        class CertExpression : MatchExpression
        {
            private readonly ExpressionOperation op;
            private readonly int certificateIndex;
            private readonly object certificateField;
            private readonly byte[] certificateFieldBytes;

            public CertExpression(
                ExpressionOperation op,
                int certificateIndex,
                object certificateField, // string or byte[]
                ExpressionMatchType matchType,
                byte[]? matchValue)
                : base(matchType, matchValue) 
            {
                this.op = op;
                this.certificateIndex = certificateIndex;
                this.certificateField = certificateField;

                certificateFieldBytes = certificateField is string certificateFieldString ? 
                    Encoding.UTF8.GetBytes(certificateFieldString) :
                    (byte[])certificateField;
            }

            public override int Size =>
                16 +
                Align(certificateFieldBytes.Length) +
                base.Size;

            public override void Write(Span<byte> buffer, out int bytesWritten)
            {
                BinaryPrimitives.WriteUInt32BigEndian(buffer, (uint)op);
                BinaryPrimitives.WriteInt32BigEndian(buffer.Slice(4), certificateIndex);
                BinaryPrimitives.WriteInt32BigEndian(buffer.Slice(8), certificateFieldBytes.Length);
                certificateFieldBytes.CopyTo(buffer.Slice(12, certificateFieldBytes.Length));
                buffer.Slice(12 + certificateFieldBytes.Length, Align(certificateFieldBytes.Length) - certificateFieldBytes.Length).Fill((byte)0);
                bytesWritten = 12 + Align(certificateFieldBytes.Length);
                base.Write(buffer.Slice(bytesWritten), out var matchExpressionSize);
                bytesWritten += matchExpressionSize;
            }


            public override string ToString()
            {
                return op switch {
                    ExpressionOperation.CertField => $"certificate {CertificateSlotToString(certificateIndex)} [{certificateField}] {base.ToString()}",
                    ExpressionOperation.CertGeneric => $"certificate {CertificateSlotToString(certificateIndex)} [field.{GetOidString(certificateFieldBytes)}] {base.ToString()}",
                    ExpressionOperation.CertPolicy => $"certificate {CertificateSlotToString(certificateIndex)} [policy.{GetOidString(certificateFieldBytes)}] {base.ToString()}",
                    ExpressionOperation.CertFieldDate => $"certificate {CertificateSlotToString(certificateIndex)} [timestamp.{GetOidString(certificateFieldBytes)}] {base.ToString()}",
                    _ => "unknown",
                };
            }
        }
    }
}
