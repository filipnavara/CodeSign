namespace Melanzana.MachO
{
    public class MachDyldInfoOnly : MachDyldInfo
    {
        public MachDyldInfoOnly()
        {
        }

        public MachDyldInfoOnly(
            MachLinkEditData rebaseData,
            MachLinkEditData bindData,
            MachLinkEditData weakBindData,
            MachLinkEditData lazyBindData,
            MachLinkEditData exportData)
            : base(rebaseData, bindData, weakBindData, lazyBindData, exportData)
        {
        }
    }
}