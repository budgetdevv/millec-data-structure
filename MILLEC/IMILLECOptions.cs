using System.Runtime.CompilerServices;

namespace MILLEC
{
    public interface IMILLECOptions<ItemT>
    {
        // Inline so that it can be constant-folded, making branches free.
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static virtual bool ZeroInitialize()
        {
            return false;
        }
        
        // Inline so that it can be constant-folded, making branches free.
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static virtual bool SkipValidation()
        {
            return false;
        }
        
        // Inline so that it can be constant-folded, making branches free.
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static virtual bool ZeroItemsOnRemoval()
        {
            return RuntimeHelpers.IsReferenceOrContainsReferences<ItemT>();
        }
        
        // Inline so that it can be constant-folded, making branches free.
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static virtual bool UseInlineFreeList()
        {
            // This switch will be ignored if RuntimeHelpers.IsReferenceOrContainsReferences<ItemT>() == true
            // or sizeof(ItemT) < sizeof(int)
            return true;
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static virtual bool ItemEquals(ref ItemT left, ref ItemT right)
        {
            return EqualityComparer<ItemT>.Default.Equals(left, right);
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static virtual int GetHashCode(ref ItemT item)
        {
            return EqualityComparer<ItemT>.Default.GetHashCode(item);
        }
    }
}