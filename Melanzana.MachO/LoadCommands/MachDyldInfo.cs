namespace Melanzana.MachO
{
    public class MachDyldInfo : MachLoadCommand
    {
        public MachLinkEditData RebaseData { get; init; }
        public MachLinkEditData BindData { get; init; }
        public MachLinkEditData WeakBindData { get; init; }
        public MachLinkEditData LazyBindData { get; init; }
        public MachLinkEditData ExportData { get; init; }
    }
}