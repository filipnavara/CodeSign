namespace Melanzana.MachO
{
    public abstract class MachLinkEdit : MachLoadCommand
    {
        protected MachLinkEdit()
        {
            Data = new MachLinkEditData();
        }

        protected MachLinkEdit(MachLinkEditData data)
        {
            ArgumentNullException.ThrowIfNull(data);

            Data = data;
        }

        public uint FileOffset => Data.FileOffset;

        public uint FileSize => (uint)Data.Size;

        public MachLinkEditData Data { get; private init; }

        internal override IEnumerable<MachLinkEditData> LinkEditData
        {
            get
            {
                yield return Data;
            }
        }
    }
}