using System.Buffers;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace MILLEC
{
    [StructLayout(LayoutKind.Auto)]
    public unsafe partial struct MILLEC<T>
    {
        internal T[] _itemsArr;
        internal byte[] _bitVectorsArr;
        internal int _count, _highestTouchedIndex;
        private FreeSlot _firstFreeSlot;

        public int Count => _count;
        public int TouchedSlotsCount => _highestTouchedIndex + 1;
        public int Capacity => _itemsArr.Length;

        // Works for initial values too! -1 + 1 - 0 = 0
        public int FreeSlotCount => _highestTouchedIndex + 1 - _count;

        private const int ALIGNMENT = 64, NO_NEXT_SLOT_VALUE = -1, DEFAULT_HIGHEST_TOUCHED_INDEX = -1;

        // Allow skipInit to be constant-folded
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static AllocateT[] Allocate<AllocateT>(int size, bool skipInit)
        {
            // We will allocate the BitVectorsArr on POH. In the future, this will enable us to use aligned SIMD instructions
            // We also try to allocate ItemsArr on POH, for better memory locality

            var shouldAllocateOnPOH = !RuntimeHelpers.IsReferenceOrContainsReferences<T>();
            
            if (skipInit)
            {
                return GC.AllocateUninitializedArray<AllocateT>(size, shouldAllocateOnPOH);
            }

            else
            {
                return GC.AllocateArray<AllocateT>(size, shouldAllocateOnPOH);
            }
        }

        public MILLEC(): this(size: BYTE_BIT_COUNT) { }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int RoundToNextMultiple(int num, int multiple)
        {
            var rem = num % multiple;

            return rem == 0 ? num : (num - rem + multiple);
        }
        
        private const int BYTE_BIT_COUNT = 8;
        
        public MILLEC(int size)
        {
            if (RuntimeHelpers.IsReferenceOrContainsReferences<T>() || sizeof(T) < sizeof(int))
            {
                throw new NotImplementedException("We need to add support for managed Ts");
            }

            // To simplify and optimize ItemsEnumerator, we make _itemsArr.Length a multiple of BYTE_BIT_COUNT
            size = RoundToNextMultiple(size, BYTE_BIT_COUNT);
            
            // It should never be less than, since we round up.
            Debug.Assert(size >= BYTE_BIT_COUNT);
            
            Debug.Assert(size % BYTE_BIT_COUNT == 0);
            
            _itemsArr = Allocate<T>(size, true);

            _bitVectorsArr = AllocateBitArray(size);

            _count = 0;

            _highestTouchedIndex = DEFAULT_HIGHEST_TOUCHED_INDEX;
            
            _firstFreeSlot = new FreeSlot();
        }
        
        public static byte[] AllocateBitArray(int countOfT)
        {
            // TODO: This needs to be revisited if we ever impl SIMD
            int size;
            
            #if DEBUG
            (size, var remainder) = Math.DivRem(countOfT, BYTE_BIT_COUNT);
            // countOfT is guaranteed to be a multiple of BYTE_BIT_COUNT
            Debug.Assert(remainder == 0);
            #else
            size = countOfT / BYTE_BIT_COUNT;
            #endif
            
            // Byte is unmanaged, and therefore will always be pinned
            // We want it to be zero-initialized, since a set bit of 1 indicates that a slot is not empty.
            return Allocate<byte>(size, false);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool ItemExistsAtIndex(BitVectorsArrayInterfacer bitVectorsArrayInterfacer, int index, int itemsArrLength, out BitInterfacer bitInterfacer)
        {
            bitInterfacer = new BitInterfacer(bitVectorsArrayInterfacer, index);

            // HighestTouchedIndex is guaranteed to be < Length
            return unchecked((uint) index) <= itemsArrLength && bitInterfacer.IsSet;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)] // Allow skipValidation to be constant-folded
        private static void ValidateItemExistsAtIndex(BitVectorsArrayInterfacer bitVectorsArrayInterfacer, int index, int itemsArrLength, bool skipValidation, out BitInterfacer bitInterfacer)
        {
            // TODO: Does JIT optimize branches out?
            if (skipValidation)
            {
                bitInterfacer = new BitInterfacer(bitVectorsArrayInterfacer, index);
                
                return;
            }
            
            if (ItemExistsAtIndex(bitVectorsArrayInterfacer, index, itemsArrLength, out bitInterfacer))
            {
                return;
            }

            Throw();

            return;

            [MethodImpl(MethodImplOptions.NoInlining)]
            void Throw()
            {
                throw new Exception($"Item does not exist at index: {index}");
            }
        }
        
        
        public ref T this[int index]
        {
            get
            {
                var itemsArr = _itemsArr;
                
                ValidateItemExistsAtIndex(new BitVectorsArrayInterfacer(_bitVectorsArr), index, itemsArr.Length, skipValidation: false, out _);
                    
                return ref new ItemsArrayInterfacer(itemsArr)[index];
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)] // Don't pollute hot path
        private ref T ResizeAdd()
        {
            // We incremented Count beforehand
            var writeIndex = _count - 1;
            
            var oldArr = _itemsArr;

            var oldBitArray = _bitVectorsArr;

            var oldSize = oldArr.Length;

            // oldSize will never be < BYTE_BIT_COUNT. A multiple of BYTE_BIT_COUNT multiplied by 2 is still a multiple of BYTE_BIT_COUNT
            var newSize = oldSize * 2;
            
            var newArr= _itemsArr = Allocate<T>(newSize, true);

            var newBitArray = _bitVectorsArr = AllocateBitArray(newSize);
            
            oldArr.AsSpan().CopyTo(newArr);
            oldBitArray.AsSpan().CopyTo(newBitArray);
            
            // Caller is responsible for setting bit after revamp of this method.
            // // Remember to set its corresponding bit.
            // new BitInterfacer(new BitVectorsArrayInterfacer(newBitArray), writeIndex).Set();
            
            // Write the item to writeIndex. Free slots are guaranteed to be exhausted if a resize is required.
            return ref new ItemsArrayInterfacer(newArr)[writeIndex];
        }

        public void Add(T item)
        {
            // We take the value of Count before the increment, which is also the writeIndex
            var writeIndex = _count++;
            
            var itemsArr = _itemsArr;

            var itemsInterfacer = new ItemsArrayInterfacer(itemsArr);

            var firstFreeSlot = _firstFreeSlot;

            ref var slot = ref itemsInterfacer.GetFirstFreeOrNewSlot(firstFreeSlot, ref writeIndex, out var isNewSlot);

            if (isNewSlot)
            {
                // Regardless of need to resize, set HighestTouchedIndex to be writeIndex
                Debug.Assert(writeIndex == _highestTouchedIndex + 1);
                _highestTouchedIndex = writeIndex;
                
                if (writeIndex >= itemsArr.Length)
                {
                    goto Resize;
                }
                
                goto WriteToSlotAndSetCorrespondingBit;
            }

            _firstFreeSlot = FreeSlot.ReinterpretItemAsFreeSlot(ref slot);
            
            WriteToSlotAndSetCorrespondingBit:
            slot = item;
            new BitInterfacer(new BitVectorsArrayInterfacer(_bitVectorsArr), writeIndex).Set();
            return;
            
            Resize:
            slot = ref ResizeAdd();
            goto WriteToSlotAndSetCorrespondingBit;
        }

        public ref T UnsafeAddUninitializedSlot()
        {
            // We take the value of Count before the increment, which is also the writeIndex
            var writeIndex = _count++;
            
            var itemsArr = _itemsArr;

            var itemsInterfacer = new ItemsArrayInterfacer(itemsArr);

            var firstFreeSlot = _firstFreeSlot;

            ref var slot = ref itemsInterfacer.GetFirstFreeOrNewSlot(firstFreeSlot, ref writeIndex, out var isNewSlot);

            if (isNewSlot)
            {
                // Regardless of need to resize, set HighestTouchedIndex to be writeIndex
                Debug.Assert(writeIndex == _highestTouchedIndex + 1);
                _highestTouchedIndex = writeIndex;
                
                if (writeIndex >= itemsArr.Length)
                {
                    goto Resize;
                }
                
                goto SetCorrespondingBitAndReturnSlotRef;
            }

            _firstFreeSlot = FreeSlot.ReinterpretItemAsFreeSlot(ref slot);
            
            SetCorrespondingBitAndReturnSlotRef: ;
            new BitInterfacer(new BitVectorsArrayInterfacer(_bitVectorsArr), writeIndex).Set();
            return ref slot;
            
            Resize:
            slot = ref ResizeAdd();
            goto SetCorrespondingBitAndReturnSlotRef;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)] // Allow performDecrementHighestTouchedOptimization and skipValidation to be constant-folded.
        public void RemoveAt(int index, bool performDecrementHighestTouchedOptimization = true, bool skipValidation = false)
        {
            var itemsArr = _itemsArr;

            ValidateItemExistsAtIndex(new BitVectorsArrayInterfacer(_bitVectorsArr), index, itemsArr.Length, skipValidation, out var bitInterfacer);

            bitInterfacer.Clear();

            var newCount = --_count;
            
            // Count will never be negative, as ValidateItemExistsAtIndex() will throw.
            if (newCount == 0)
            {
                goto Empty;
            }
            
            if (performDecrementHighestTouchedOptimization && index == _highestTouchedIndex)
            {
                goto DecrementHighestTouched;
            }

            var itemsArrayInterfacer = new ItemsArrayInterfacer(itemsArr);

            ref var removedItem = ref itemsArrayInterfacer[index];

            ref var freeSlot = ref FreeSlot.ReinterpretItemAsFreeSlot(ref removedItem);

            // Deleted item's slot now houses previous free slot
            freeSlot = _firstFreeSlot;
            // We will set the _firstFreeSlot's Next field to current index.
            _firstFreeSlot.Next = index;
            return;
            
            DecrementHighestTouched: // If we are deleting the last item, just decrement the HighestTouchedIndex.
            _highestTouchedIndex = index - 1;
            return;
            
            Empty:
            Empty(ref this);
            return;
            
            [MethodImpl(MethodImplOptions.NoInlining)]
            void Empty(ref MILLEC<T> @this)
            {
                Debug.Assert(@this._count == 0);
                
                // We already clear set bit via bitInterfacer.Clear();
                // At this point, the BitVectorArr is guaranteed to be all cleared.
                #if DEBUG
                foreach (var bitVector in @this._bitVectorsArr)
                {
                    if (bitVector != 0)
                    {
                        throw new Exception($"nameof{bitVector} not cleared!");
                    }
                }
                
                #endif
                @this.Clear(false);
            }
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)] // Allow performDecrementHighestTouchedOptimization and skipValidation to be constant-folded.
        public void RemoveAt(int index, out T removedItem, bool performDecrementHighestTouchedOptimization = true, bool skipValidation = false)
        {
            var itemsArr = _itemsArr;

            ValidateItemExistsAtIndex(new BitVectorsArrayInterfacer(_bitVectorsArr), index, itemsArr.Length, skipValidation, out var bitInterfacer);

            bitInterfacer.Clear();

            var newCount = --_count;
            
            var itemsArrayInterfacer = new ItemsArrayInterfacer(itemsArr);

            ref var removedItemRef = ref itemsArrayInterfacer[index];

            // Copy its value before we overwrite it with FreeSlot data.
            // Do not move this line of code down.
            removedItem = removedItemRef;
            
            // Count will never be negative, as ValidateItemExistsAtIndex() will throw.
            if (newCount == 0)
            {
                goto Empty;
            }
            
            if (performDecrementHighestTouchedOptimization && index == _highestTouchedIndex)
            {
                goto DecrementHighestTouched;
            }
            
            ref var freeSlot = ref FreeSlot.ReinterpretItemAsFreeSlot(ref removedItemRef);

            // Deleted item's slot now houses previous free slot
            freeSlot = _firstFreeSlot;
            
            // We will set the _firstFreeSlot's Next field to current index.
            _firstFreeSlot.Next = index;
            return;
            
            DecrementHighestTouched: // If we are deleting the last item, just decrement the HighestTouchedIndex.
            _highestTouchedIndex = index - 1;
            return;
            
            Empty:
            Empty(ref this);
            return;
            
            [MethodImpl(MethodImplOptions.NoInlining)]
            void Empty(ref MILLEC<T> @this)
            {
                Debug.Assert(@this._count == 0);
                
                // We already clear set bit via bitInterfacer.Clear();
                // At this point, the BitVectorArr is guaranteed to be all cleared.
                #if DEBUG
                foreach (var bitVector in @this._bitVectorsArr)
                {
                    if (bitVector != 0)
                    {
                        throw new Exception($"nameof{bitVector} not cleared!");
                    }
                }
                #endif
                @this.Clear(false);
            }
        }

        private struct EquatableItem: IEquatable<EquatableItem>
        {
            private static readonly EqualityComparer<EquatableItem> Comparer;
            
            public T Item;
            
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public bool Equals(EquatableItem other)
            {
                return Comparer.Equals(this, other);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public override int GetHashCode()
            {
                return Comparer.GetHashCode(this);
            }
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)] // Allow performDecrementHighestTouchedOptimization to be constant-folded.
        public bool TryRemoveItem(T item, bool performDecrementHighestTouchedOptimization = true)
        {
            var itemReinterpreted = Unsafe.As<T, EquatableItem>(ref item);

            var itemsArr = _itemsArr;
            
            var span = MemoryMarshal.CreateSpan(ref Unsafe.As<T, EquatableItem>(ref new ItemsArrayInterfacer(itemsArr).FirstItem), TouchedSlotsCount);

            var indexOfItem = span.IndexOf(itemReinterpreted);
            
            var success = indexOfItem != -1 && ItemExistsAtIndex(new BitVectorsArrayInterfacer(_bitVectorsArr), indexOfItem, itemsArr.Length, out _);
            
            if (success)
            {
                RemoveAt(indexOfItem, performDecrementHighestTouchedOptimization, skipValidation: true);
            }

            return success;
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Clear(bool clearBitVectors = true)
        {
            _firstFreeSlot = new FreeSlot();
            _count = 0;
            _highestTouchedIndex = DEFAULT_HIGHEST_TOUCHED_INDEX;
            _bitVectorsArr.AsSpan().Clear();
            
            // We don't have to clear ItemsArr, as it is not possible for a slot to become "free" without
            // writing to it prior ( Via Add() )
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ref T UnsafeGetFirstItemReference()
        {
            return ref new ItemsArrayInterfacer(_itemsArr).FirstItem;
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ref T UnsafeGetItemReference(int index)
        {
            return ref new ItemsArrayInterfacer(_itemsArr)[index];
        }

        // ref T instead of ArrayItemsInterfacer, as Enumerator does not store ArrayItemsInterfacer.
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int IndexOfItemRef(ref T firstItem, ref T item)
        {
            return IndexOfRef<T>(ref firstItem, ref item);
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int IndexOfRef<F>(ref F firstRef, ref F currentRef)
        {
            return unchecked((int) (Unsafe.ByteOffset(ref firstRef, ref currentRef) / sizeof(F)));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Span<int> GetFreeSlotIndices(Span<int> buffer)
        {
            var freeSlotCount = FreeSlotCount;

            if (freeSlotCount <= buffer.Length)
            {
                return UnsafeGetFreeSlotIndices(buffer, freeSlotCount);
            }

            return Throw();
            
            [MethodImpl(MethodImplOptions.NoInlining)]
            Span<int> Throw()
            {
                throw new OverflowException($"{nameof(buffer)} is too small!");
            }
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Span<int> UnsafeGetFreeSlotIndices(Span<int> buffer, int freeSlotCount)
        {
            ref var first = ref MemoryMarshal.GetReference(buffer);
            
            ref var lastOffsetByOne = ref Unsafe.Add(ref first, freeSlotCount);
            
            return UnsafeGetFreeSlotIndices(ref first, ref lastOffsetByOne, freeSlotCount, new ItemsArrayInterfacer(_itemsArr));
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private Span<int> UnsafeGetFreeSlotIndices(ref int first, ref int lastOffsetByOne, int freeSlotCount, ItemsArrayInterfacer itemsArrInterfacer)
        {
            Debug.Assert(IndexOfRef<int>(ref first, ref lastOffsetByOne) == freeSlotCount);
            
            ref var current = ref first;
            
            var span = MemoryMarshal.CreateSpan(ref current, freeSlotCount);
            
            var currentFreeSlot = _firstFreeSlot;
            
            for (; !Unsafe.AreSame(ref current, ref lastOffsetByOne)
                 ; current = ref Unsafe.Add(ref current, 1)
                 // It is indeed possible for it to return a null-ref. However, we will never
                 // dereference it due to the loop being skipped ( freeSlotCount will be 0 )
                 , currentFreeSlot = currentFreeSlot.GetNextFreeSlot(itemsArrInterfacer))
            {
                current = currentFreeSlot.Next;
            }

            return span;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Span<int> GetFreeSlotIndicesAllocating()
        {
            return GetFreeSlotIndices(SharedArrayPool.Rent(FreeSlotCount));
        }
        
        // Avoid extra indirection
        private static readonly ArrayPool<int> SharedArrayPool = ArrayPool<int>.Shared;
        
        public void Optimize()
        {
            var freeSlotCount = FreeSlotCount;

            if (freeSlotCount == 0)
            {
                goto Ret; // Forward jump to favor slow path.
            }
            
            var ap = SharedArrayPool;
                
            var buffer = ap.Rent(freeSlotCount);

            ref var first = ref MemoryMarshal.GetArrayDataReference(buffer);

            ref var lastOffsetByOne = ref Unsafe.Add(ref first, freeSlotCount);
            
            var itemsArrInterfacer = new ItemsArrayInterfacer(_itemsArr);
            
            var span = UnsafeGetFreeSlotIndices(ref first, ref lastOffsetByOne, freeSlotCount, itemsArrInterfacer);
            
            span.Sort();

            var highestTouched = _highestTouchedIndex;

            if (true) // I'd like to reuse currentFreeSlotIndex as a variable in foreach 
            {
                ref var currentFreeSlotIndex = ref lastOffsetByOne;

                while (true) // Check for adjacent free slots at the end, and reclaim them.
                {
                    currentFreeSlotIndex = ref Unsafe.Subtract(ref currentFreeSlotIndex, 1);

                    if (!Unsafe.IsAddressLessThan(ref currentFreeSlotIndex, ref first))
                    {
                        if (highestTouched == currentFreeSlotIndex)
                        {
                            // We can actually convert current free slot into non-live slot.
                            // Hence, the highestTouchedIndex becomes that of the previous slot's
                            // ( Previous as in, the one adjacent to its left )
                            highestTouched = currentFreeSlotIndex - 1;
                            continue;
                        }
                    }
                    
                    break;
                }
                
                // Unconditionally write highestTouched, regardless of whether it has changed or not
                _highestTouchedIndex = highestTouched;
            
                // Assume original highestTouched is 8
                // 7 and 8 will be adjacent. 0, 1 2 are not.
                // The loop will terminate at 2, we want the length from 0 to 2, which is 3.
                // To calculate it, we take 2 ( Which is the index ) - 0 + 1 = 3.
                //
                //         ---- Non-adjacent.
                //         |
                //         v
                // [ 0, 1, 2, 7, 8 ]

                var newBufferLength = IndexOfRef<int>(ref first, ref currentFreeSlotIndex) + 1;
                
                span = MemoryMarshal.CreateSpan(ref first, newBufferLength);

                // Ensure we sliced the new span correctly. Do not move this check up, as FreeSlotCount has a dependency
                // on the updated _highestTouchedIndex.
                Debug.Assert(span.Length == FreeSlotCount);
            }

            ref var previousSlot = ref _firstFreeSlot;

            foreach (var currentFreeSlotIndex in span)
            {
                previousSlot.Next = currentFreeSlotIndex;
                previousSlot = ref previousSlot.GetNextFreeSlot(itemsArrInterfacer, skipValidate: true);
            }

            // Taking a ref to _firstFreeSlot is a great idea, as it ensures that
            // _firstFreeSlot's next is -1 even if we end up optimizing away all of
            // the free slots!
            // Set the last free slot's next to be -1.
            previousSlot.Next = -1;

            ap.Return(buffer);
            
            #if DEBUG
            var newFreeSlotCount = FreeSlotCount;
            var testBuffer = new int[newFreeSlotCount];
            var newFreeSlotIndices = GetFreeSlotIndices(testBuffer);
            Debug.Assert(newFreeSlotIndices.SequenceEqual(span));
            #endif
            
            Ret:
            return;
        }
    }
}