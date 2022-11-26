namespace Melanzana.MachO
{
    public class MachDyldInfo : MachLoadCommand
    {
        public MachDyldInfo(MachObjectFile objectFile)
        {
            ArgumentNullException.ThrowIfNull(objectFile);

            RebaseData = new MachLinkEditData();
            BindData = new MachLinkEditData();
            WeakBindData = new MachLinkEditData();
            LazyBindData = new MachLinkEditData();
            ExportData = new MachLinkEditData();
        }

        public MachDyldInfo(
            MachObjectFile objectFile,
            MachLinkEditData rebaseData,
            MachLinkEditData bindData,
            MachLinkEditData weakBindData,
            MachLinkEditData lazyBindData,
            MachLinkEditData exportData)
        {
            ArgumentNullException.ThrowIfNull(objectFile);
            ArgumentNullException.ThrowIfNull(rebaseData);
            ArgumentNullException.ThrowIfNull(bindData);
            ArgumentNullException.ThrowIfNull(weakBindData);
            ArgumentNullException.ThrowIfNull(lazyBindData);
            ArgumentNullException.ThrowIfNull(exportData);

            RebaseData = rebaseData;
            RebaseData.Parent = this;
            BindData = bindData;
            BindData.Parent = this;
            WeakBindData = weakBindData;
            WeakBindData.Parent = this;
            LazyBindData = lazyBindData;
            LazyBindData.Parent = this;
            ExportData = exportData;
            ExportData.Parent = this;
        }

        public MachLinkEditData RebaseData { get; private init; }
        public MachLinkEditData BindData { get; private init; }
        public MachLinkEditData WeakBindData { get; private init; }
        public MachLinkEditData LazyBindData { get; private init; }
        public MachLinkEditData ExportData { get; private init; }

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