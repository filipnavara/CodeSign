using static System.Collections.Specialized.BitVector32;

namespace Melanzana.MachO
{
    public class MachDyldInfo : MachLoadCommand
    {
        public MachLinkEditData RebaseData { get; init; }
        public MachLinkEditData BindData { get; init; }
        public MachLinkEditData WeakBindData { get; init; }
        public MachLinkEditData LazyBindData { get; init; }
        public MachLinkEditData ExportData { get; init; }

        internal override IEnumerable<MachLinkEditData> LinkEditData
        {
            get
            {
                yield return RebaseData;
                yield return BindData;
                yield return WeakBindData;
                yield return LazyBindData;
                yield return ExportData;
            }
        }
    }
}