﻿using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace MILLEC
{
    public unsafe struct MILLEC<T>
    {
        internal T[] _itemsArr;
        internal byte[] _bitVecsArr;
        internal int _count, _highestTouchedIndex;
        private FreeSlot _firstFreeSlot;

        public int Count => _count;
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

        public MILLEC(): this(0) { }
        
        public MILLEC(int size)
        {
            if (RuntimeHelpers.IsReferenceOrContainsReferences<T>() || sizeof(T) < sizeof(int))
            {
                throw new NotImplementedException("We need to add support for managed Ts");
            }
            
            _itemsArr = Allocate<T>(size, true);

            _bitVecsArr = AllocateBitArray(size);

            _count = 0;

            _highestTouchedIndex = DEFAULT_HIGHEST_TOUCHED_INDEX;
            
            _firstFreeSlot = new FreeSlot();
        }
        
        private const int BYTE_BIT_COUNT = 8;
        
        public static byte[] AllocateBitArray(int countOfT)
        {
            // TODO: This needs to be revisited if we ever impl SIMD
            
            // TODO: Use DivRem intrinsic
            var (quotient, remainder) = Math.DivRem(countOfT, BYTE_BIT_COUNT);
            
            var size = (remainder == 0) ? quotient : quotient + 1;
            
            // Byte is unmanaged, and therefore will always be pinned
            // We want it to be zero-initialized, since a set bit of 1 indicates that a slot is not empty.
            return Allocate<byte>(size, false);
        }
        
        internal readonly ref struct BitVectorsArrayInterfacer
        {
            public readonly ref byte FirstItem;

            public BitVectorsArrayInterfacer(byte[] bitVectorsArray)
            {
                FirstItem = ref MemoryMarshal.GetArrayDataReference(bitVectorsArray);
            }

            public ref byte this[int index]
            {
                get => ref Unsafe.Add(ref FirstItem, index);
            }
        }
        
        internal readonly ref struct BitInterfacer
        {
            private readonly ref byte Slot;

            private readonly int VectorIndex;
            
            public BitInterfacer(BitVectorsArrayInterfacer bitVectorsArrayInterfacer, int slotIndex)
            {
                // E.x. index 7 -> 7 / 8 -> Q:0 R:7, 8 -> 8 / 8 -> Q:1 R:0, 9 -> 9 / 8 ->  Q:1 R:1
                var index = Math.DivRem(slotIndex, BYTE_BIT_COUNT, out VectorIndex);

                Slot = ref Unsafe.Add(ref bitVectorsArrayInterfacer.FirstItem, index);
            }

            public bool IsWholeByteClear()
            {
                return Slot == 0;
            }
            
            public bool IsSet => (Slot & (1 << VectorIndex)) != 0;

            public void Set()
            {
                Slot |= unchecked((byte) (1 << VectorIndex));
            }
            
            public void Clear()
            {
                Slot &= unchecked((byte) ~(1 << VectorIndex));
            }
        }
        
        internal readonly ref struct ItemsArrayInterfacer
        {
            public readonly ref T FirstItem;

            public ItemsArrayInterfacer(T[] itemsArr)
            {
                FirstItem = ref MemoryMarshal.GetArrayDataReference(itemsArr);
            }

            public ref T this[int index]
            {
                get => ref Unsafe.Add(ref FirstItem, index);
            }

            public ref T GetLastSlotOffsetByOne(T[] itemsArr)
            {
                return ref this[itemsArr.Length];
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public ref T GetFirstFreeOrNewSlot(FreeSlot firstFreeSlotFieldValue, ref int newSlotWriteIndex, out bool isNewSlot)
            {
                var next = firstFreeSlotFieldValue.Next;

                isNewSlot = next == -1;
                
                newSlotWriteIndex = isNewSlot ? newSlotWriteIndex : next;

                return ref this[newSlotWriteIndex];
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool ItemExistsAtIndex(BitVectorsArrayInterfacer bitVectorsArrayInterfacer, int index, out BitInterfacer bitInterfacer)
        {
            bitInterfacer = new BitInterfacer(bitVectorsArrayInterfacer, index);

            // HighestTouchedIndex is guaranteed to be < Length
            return index <= _highestTouchedIndex && bitInterfacer.IsSet;
        }

        private void ValidateItemExistsAtIndex(BitVectorsArrayInterfacer bitVectorsArrayInterfacer, int index, out BitInterfacer bitInterfacer)
        {
            if (ItemExistsAtIndex(bitVectorsArrayInterfacer, index, out bitInterfacer))
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
                ValidateItemExistsAtIndex(new BitVectorsArrayInterfacer(_bitVecsArr), index, out _);
                    
                return ref new ItemsArrayInterfacer(_itemsArr)[index];
            }
        }

        internal struct FreeSlot
        {
            public int Next;

            public FreeSlot(): this(NO_NEXT_SLOT_VALUE) { }
            
            public FreeSlot(int next)
            {
                Next = next;
            }

            public ref FreeSlot GetNextFreeSlot(ItemsArrayInterfacer itemsArrayInterfacer)
            {
                var next = Next;
                
                if (next != NO_NEXT_SLOT_VALUE)
                {
                    return ref Unsafe.As<T, FreeSlot>(ref Unsafe.Add(ref itemsArrayInterfacer.FirstItem, next));
                }

                return ref Unsafe.NullRef<FreeSlot>();
            }

            [UnscopedRef]
            public ref T ReinterpretAsItem()
            {
                return ref Unsafe.As<FreeSlot, T>(ref this);
            }

            public static ref FreeSlot ReinterpretItemAsFreeSlot(ref T item)
            {
                return ref Unsafe.As<T, FreeSlot>(ref item);
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)] // Don't pollute hot path
        private void ResizeAdd(T item)
        {
            // We incremented Count beforehand
            var writeIndex = _count - 1;
            
            var oldArr = _itemsArr;

            var oldBitArray = _bitVecsArr;

            var oldSize = oldArr.Length;

            var newSize = (oldSize == 0) ? 1 : oldSize * 2;
            
            var newArr= _itemsArr = Allocate<T>(newSize, true);

            var newBitArray = _bitVecsArr = AllocateBitArray(newSize);
            
            oldArr.AsSpan().CopyTo(newArr);
            oldBitArray.AsSpan().CopyTo(newBitArray);
            
            // Write the item to writeIndex. Free slots are guaranteed to be exhausted if a resize is required.
            new ItemsArrayInterfacer(newArr)[writeIndex] = item;
            
            // Remember to set its corresponding bit.
            new BitInterfacer(new BitVectorsArrayInterfacer(newBitArray), writeIndex).Set();
        }

        public void Add(T item)
        {
            // We take the value of Count before the increment, which is also the writeIndex
            var writeIndex = _count++;
            
            var itemsArr = _itemsArr;

            var itemsInterfacer = new ItemsArrayInterfacer(itemsArr);

            var firstFreeSlot = _firstFreeSlot;

            // ref var currentFreeSlot = ref firstFreeSlot.GetNextFreeSlot(itemsInterfacer);
            //
            // ref var writeSlot = ref Unsafe.NullRef<T>();
            //
            // if (Unsafe.IsNullRef(ref currentFreeSlot))
            // {
            //     Debug.Assert(HighestTouchedIndex == writeIndex - 1);
            //     
            //     // Regardless of need to resize, set HighestTouchedIndex to be writeIndex
            //     HighestTouchedIndex = writeIndex;
            //     
            //     // This pattern elide bounds.
            //     if (writeIndex < itemsArr.Length)
            //     {
            //         writeSlot = ref itemsArr[writeIndex];
            //     }
            //
            //     else
            //     {
            //         ResizeAdd(item);
            //         return;
            //     }
            // }
            //
            // else
            // {
            //     // Write value of currentFreeSlot to the field
            //     FirstFreeSlot = currentFreeSlot;
            //     
            //     writeSlot = ref currentFreeSlot.ReinterpretAsItem();
            //
            //     writeIndex = firstFreeSlot.Next;
            // }
            //
            // writeSlot = item;
            //
            // var bitInterfacer = new BitInterfacer(new BitVectorsArrayInterfacer(BitVectorsArr), writeIndex);
            //
            // bitInterfacer.Set();

            ref var slot = ref itemsInterfacer.GetFirstFreeOrNewSlot(firstFreeSlot, ref writeIndex, out var isNewSlot);

            if (isNewSlot)
            {
                // Regardless of need to resize, set HighestTouchedIndex to be writeIndex
                Debug.Assert(writeIndex == _highestTouchedIndex + 1);
                _highestTouchedIndex = writeIndex;
                
                if (writeIndex < itemsArr.Length)
                {
                    goto WriteToSlotAndSetCorrespondingBit;
                }

                ResizeAdd(item);
                return;
            }

            _firstFreeSlot = FreeSlot.ReinterpretItemAsFreeSlot(ref slot);
            
            WriteToSlotAndSetCorrespondingBit:
            slot = item;
            new BitInterfacer(new BitVectorsArrayInterfacer(_bitVecsArr), writeIndex).Set();
        }

        public void RemoveAt(int index)
        {
            ValidateItemExistsAtIndex(new BitVectorsArrayInterfacer(_bitVecsArr), index, out var bitInterfacer);
            
            var newCount = _count - 1;
            
            Unsafe.SkipInit(out FreeSlot newFreeSlot);

            bitInterfacer.Clear();
            
            if (newCount <= 0)
            {
                goto Empty;
            }

            _count = newCount;
            
            if (index == _highestTouchedIndex)
            {
                goto DecrementHighestTouched;
            }

            var itemsArrayInterfacer = new ItemsArrayInterfacer(_itemsArr);

            ref var removedItem = ref itemsArrayInterfacer[index];

            ref var freeSlot = ref FreeSlot.ReinterpretItemAsFreeSlot(ref removedItem);

            // Deleted item's slot now houses previous free slot
            freeSlot = _firstFreeSlot;
            
            // We will set the FirstFreeSlot field to current index.
            newFreeSlot.Next = index;
            
            _firstFreeSlot = newFreeSlot;
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
                // We did not actually set @this.Count ( It is set after the goto statement that leads to this helper. )
                if (@this._count == 1)
                {
                    // We already clear set bit via bitInterfacer.Clear();
                    // At this point, the BitVectorArr is guaranteed to be all cleared.
                    #if DEBUG
                    foreach (var bitVector in @this._bitVecsArr)
                    {
                        if (bitVector != 0)
                        {
                            throw new Exception($"nameof{bitVector} not cleared!");
                        }
                    }
                
                    #endif
                    @this.Clear(false);
                } // Else, it was already empty, do nothing.
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Clear(bool clearBitVectors = true)
        {
            _firstFreeSlot = new FreeSlot();
            _count = 0;
            _highestTouchedIndex = DEFAULT_HIGHEST_TOUCHED_INDEX;
            _bitVecsArr.AsSpan().Clear();
            
            // We don't have to clear ItemsArr, as it is not possible for a slot to become "free" without
            // writing to it prior ( Via Add() )
        }
        
        public ref T UnsafeGetFirstItemReference()
        {
            return ref new ItemsArrayInterfacer(_itemsArr).FirstItem;
        }
        
        public ref T UnsafeGetItemReference(int index)
        {
            return ref new ItemsArrayInterfacer(_itemsArr)[index];
        }

        // ref T instead of ArrayItemsInterfacer, as Enumerator does not store ArrayItemsInterfacer.
        private static int IndexOfItemRef(ref T firstItem, ref T item)
        {
            return unchecked((int) (Unsafe.ByteOffset(ref firstItem, ref item) / sizeof(T)));
        }

        public ref struct Enumerator
        {
            private readonly ref T FirstItem, LastItem;
            
            private ref T CurrentItem;

            // private ref byte CurrentBitVector;

            private readonly BitVectorsArrayInterfacer BitVectorsArrayInterfacer;
            
            public ref T Current => ref CurrentItem;

            internal Enumerator(ItemsArrayInterfacer itemsArrayInterfacer, BitVectorsArrayInterfacer bitVectorsArrayInterfacer)
            {
                FirstItem = ref itemsArrayInterfacer.FirstItem; 
                // MoveNext() is always called before the first iteration
                CurrentItem = ref Unsafe.Subtract(ref FirstItem, 1);
                // CurrentBitVector = ref bitArrayInterfacer.FirstItem;
                BitVectorsArrayInterfacer = bitVectorsArrayInterfacer;
            }
            
            public bool MoveNext()
            {
                // TODO: Improve performance of this.
                MoveNext:
                CurrentItem = ref Unsafe.Add(ref CurrentItem, 1);
                
                if (!Unsafe.IsAddressGreaterThan(ref CurrentItem, ref LastItem))
                {
                    var currentIndex = IndexOfItemRef(ref FirstItem, ref CurrentItem);

                    if (new BitInterfacer(BitVectorsArrayInterfacer, currentIndex).IsSet)
                    {
                        return true;
                    }
                    
                    goto MoveNext;
                }
                
                return false;
            }
        }

        public Enumerator GetEnumerator()
        {
            return new Enumerator(new ItemsArrayInterfacer(_itemsArr), new BitVectorsArrayInterfacer(_bitVecsArr));
        }
    }
}