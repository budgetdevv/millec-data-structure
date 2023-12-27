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

        // We will allocate the BitVectorsArr on POH. In the future, this will enable us to use aligned SIMD instructions
        // We also try to allocate ItemsArr on POH, for better memory locality
        public static virtual bool AllocateBitVectorOnPinnedObjectHeap()
        {
            return true;
        }
        
        public static virtual bool AllocateItemsOnPinnedObjectHeap()
        {
            // The allocator will throw if the overridden version returns true
            // for RuntimeHelpers.IsReferenceOrContainsReferences<ItemT>() == true.
            return !RuntimeHelpers.IsReferenceOrContainsReferences<ItemT>();
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