using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace MILLEC
{
    public unsafe partial struct MILLEC<ItemT, OptsT> where OptsT: IMILLECOptions<ItemT>
    {
        internal readonly ref struct BitVectorsArrayInterfacer
        {
            public readonly ref byte FirstItem;

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public BitVectorsArrayInterfacer(byte[] bitVectorsArray)
            {
                FirstItem = ref MemoryMarshal.GetArrayDataReference(bitVectorsArray);
            }

            public ref byte this[int index]
            {
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                get => ref Unsafe.Add(ref FirstItem, index);
            }
        }
        
        internal readonly ref struct BitInterfacer
        {
            private readonly ref byte Slot;

            private readonly int VectorIndex;
            
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public BitInterfacer(BitVectorsArrayInterfacer bitVectorsArrayInterfacer, int slotIndex)
            {
                // E.x. index 7 -> 7 / 8 -> Q:0 R:7, 8 -> 8 / 8 -> Q:1 R:0, 9 -> 9 / 8 ->  Q:1 R:1
                var index = Math.DivRem(slotIndex, BYTE_BIT_COUNT, out VectorIndex);

                Slot = ref Unsafe.Add(ref bitVectorsArrayInterfacer.FirstItem, index);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public bool IsWholeByteClear()
            {
                return Slot == 0;
            }
            
            public bool IsSet
            {
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                get => (Slot & (1 << VectorIndex)) != 0;
            }

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
            public readonly ref ItemT FirstItem;

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public ItemsArrayInterfacer(ItemT[] itemsArr)
            {
                FirstItem = ref MemoryMarshal.GetArrayDataReference(itemsArr);
            }

            public ref ItemT this[int index]
            {
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                get => ref Unsafe.Add(ref FirstItem, index);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public ref ItemT GetLastSlotOffsetByOne(ItemT[] itemsArr)
            {
                return ref this[itemsArr.Length];
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public ref ItemT GetFirstFreeOrNewSlot(FreeSlot firstFreeSlotFieldValue, ref int newSlotWriteIndex, out bool isNewSlot)
            {
                var next = firstFreeSlotFieldValue.Next;

                isNewSlot = next == -1;
                
                newSlotWriteIndex = isNewSlot ? newSlotWriteIndex : next;

                return ref this[newSlotWriteIndex];
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public int IndexOfSlot(ref ItemT slot)
            {
                return IndexOfItemRef(ref FirstItem, ref slot);
            }
        }

        public readonly ref struct ItemIndexCalculator
        {
            private readonly ItemsArrayInterfacer ItemsArrInterfacer;
            
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            internal ItemIndexCalculator(ItemsArrayInterfacer itemsArrInterfacer)
            {
                ItemsArrInterfacer = itemsArrInterfacer;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public int GetIndexOfItemRef(ref ItemT item)
            {
                return IndexOfItemRef(ref ItemsArrInterfacer.FirstItem, ref item);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ItemIndexCalculator GetItemIndexCalculator()
        {
            return new ItemIndexCalculator(new ItemsArrayInterfacer(_itemsArr));
        }
        
        internal struct FreeSlot
        {
            public int Next;

            public FreeSlot(): this(NO_NEXT_SLOT_VALUE) { }
            
            public FreeSlot(int next)
            {
                Next = next;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)] // Allow skipValidate to be constant-folded
            public ref FreeSlot GetNextFreeSlot(ItemsArrayInterfacer itemsArrayInterfacer, bool skipValidate = false)
            {
                var next = Next;
                
                if (next != NO_NEXT_SLOT_VALUE || skipValidate)
                {
                    return ref Unsafe.As<ItemT, FreeSlot>(ref Unsafe.Add(ref itemsArrayInterfacer.FirstItem, next));
                }

                return ref Unsafe.NullRef<FreeSlot>();
            }

            [UnscopedRef]
            public ref ItemT ReinterpretAsItem()
            {
                return ref Unsafe.As<FreeSlot, ItemT>(ref this);
            }

            public static ref FreeSlot ReinterpretItemAsFreeSlot(ref ItemT item)
            {
                return ref Unsafe.As<ItemT, FreeSlot>(ref item);
            }
        }
        
        public ref struct ItemsEnumerator
        {
            private readonly ref ItemT LastItemOffsetByOne;
            
            private ref ItemT CurrentItemBoundaryStart, CurrentItem;

            private ref byte CurrentBitVector;
            
            // This field is used for two things:
            // 1) Representation of whether we should use tzcnt.
            // 2) Representation of remaining set bits if we are using tzcnt.
            private byte CurrentBitVectorValue;
            
            public ref ItemT Current => ref CurrentItem;

            internal ItemsEnumerator(ref MILLEC<ItemT, OptsT> list)
            {
                var itemsArrInterfacer = new ItemsArrayInterfacer(list._itemsArr);
                // MoveNext() is always called before the first iteration
                CurrentItemBoundaryStart = ref itemsArrInterfacer.FirstItem;
       
                // CurrentItem = ref Unsafe.NullRef<T>();
                
                // We round up here, but indexOfLastOffsetByOne is guaranteed to never exceed list.Capacity, since capacity is a multiple of BYTE_BIT_COUNT
                var indexOfLastOffsetByOne = RoundToNextMultiple(list.TouchedSlotsCount, BYTE_BIT_COUNT);
                Debug.Assert(indexOfLastOffsetByOne <= list.Capacity);
                Debug.Assert(indexOfLastOffsetByOne >= 8);
                LastItemOffsetByOne = ref itemsArrInterfacer[indexOfLastOffsetByOne];
                
                CurrentBitVector = ref new BitVectorsArrayInterfacer(list._bitVectorsArr).FirstItem;

                Prep();
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private bool UseTZCNT()
            {
                return CurrentBitVectorValue != byte.MaxValue;
            }
            
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private void AdvanceBoundary()
            {
                CurrentItemBoundaryStart = ref Unsafe.Add(ref CurrentItemBoundaryStart, BYTE_BIT_COUNT);
                CurrentBitVector = ref Unsafe.Add(ref CurrentBitVector, 1);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private void Prep()
            {
                // Read out CurrentBitVector's value
                CurrentBitVectorValue = CurrentBitVector;
                CurrentItem = ref Unsafe.Subtract(ref CurrentItemBoundaryStart, 1);
            }
            
            public bool MoveNext()
            {
                Start:
                var useTZCNT = UseTZCNT();
                
                if (useTZCNT)
                {
                    if (CurrentBitVectorValue == 0)
                    {
                        // Forward jump to favor hot path.
                        goto NextBoundary;
                    }

                    var index = BitOperations.TrailingZeroCount(CurrentBitVectorValue);
                    
                    CurrentItem = ref Unsafe.Add(ref CurrentItemBoundaryStart, index);
                    
                    // Mask off current bit.
                    CurrentBitVectorValue = unchecked((byte) (CurrentBitVectorValue & ~(1 << index)));
                    
                    return true; // No reason to merge return paths for both, it is an additional jump.
                }

                else
                {
                    // Prep() sets CurrentItem to be CurrentItemBoundaryStart - 1
                    CurrentItem = ref Unsafe.Add(ref CurrentItem, 1);
                    
                    var index = IndexOfItemRef(ref CurrentItemBoundaryStart, ref CurrentItem);
                    
                    if (index == BYTE_BIT_COUNT)
                    {
                        // Forward jump to favor hot path.
                        goto NextBoundary;
                    }
                    
                    return true; // No reason to merge return paths for both, it is an additional jump.
                }
                
                NextBoundary:
                AdvanceBoundary();
                
                var shouldMoveNext = !Unsafe.AreSame(ref CurrentItemBoundaryStart, ref LastItemOffsetByOne);
                
                // This branch will be predicted taken, as the forward jump to return shouldMoveNext is predicted NOT taken.
                // This is only relevant for static branch prediction, when there's no branch data.
                // Even then, goto Start should be happening for most part, so branch prediction data will favor it.
                if (shouldMoveNext)
                {
                    Prep(); // Do NOT move this before the check, as we might read invalid memory.
                    goto Start;
                }

                // False.
                return shouldMoveNext;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ItemsEnumerator GetEnumerator()
        {
            return new ItemsEnumerator(ref this);
        }
        
        public ref struct FreeSlotInterfacer
        {
            internal ref FreeSlot CurrentFreeSlot;

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public ref ItemT UnsafeGetItem()
            {
                return ref CurrentFreeSlot.ReinterpretAsItem();
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            internal FreeSlotInterfacer(ref FreeSlot currentFreeSlot)
            {
                CurrentFreeSlot = ref currentFreeSlot;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void MarkAsUsed(ref FreeSlotEnumerator enumerator)
            {
                if (Unsafe.AreSame(ref CurrentFreeSlot, ref enumerator.PreviousFreeSlot))
                {
                    MarkAsUsedUnsafe(ref enumerator);
                }

                return;
                
                [MethodImpl(MethodImplOptions.NoInlining)]
                void Throw()
                {
                    throw new Exception($"{nameof(MarkAsUsed)} called on {nameof(FreeSlotInterfacer)} not belonging to current iteration!");
                }
            }
            
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void MarkAsUsedUnsafe(ref FreeSlotEnumerator enumerator)
            {
                // Patch the previous slot to point to current's next
                enumerator.PreviousFreeSlot.Next = CurrentFreeSlot.Next;
                var slotIndex = enumerator.ItemsArrInterfacer.IndexOfSlot(ref CurrentFreeSlot.ReinterpretAsItem());
                // Mark the current free slot as used.
                new BitInterfacer(enumerator.BitVectorsArrInterfacer, slotIndex).Set();
            }
        }
        
        public ref struct FreeSlotEnumerator
        {
            internal ref FreeSlot PreviousFreeSlot;
            
            internal FreeSlotInterfacer FreeSlotInterfacer;
            
            internal readonly ItemsArrayInterfacer ItemsArrInterfacer;

            internal readonly BitVectorsArrayInterfacer BitVectorsArrInterfacer;
            
            public FreeSlotInterfacer Current => FreeSlotInterfacer;
            
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            internal FreeSlotEnumerator(ref MILLEC<ItemT, OptsT> list)
            {
                FreeSlotInterfacer.CurrentFreeSlot = ref list._firstFreeSlot;
                ItemsArrInterfacer = new ItemsArrayInterfacer(list._itemsArr);
                BitVectorsArrInterfacer = new BitVectorsArrayInterfacer(list._bitVectorsArr);
            }
            
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public bool MoveNext()
            {
                PreviousFreeSlot = ref FreeSlotInterfacer.CurrentFreeSlot;
                FreeSlotInterfacer.CurrentFreeSlot = ref PreviousFreeSlot.GetNextFreeSlot(ItemsArrInterfacer);
                
                return !Unsafe.IsNullRef(ref FreeSlotInterfacer.CurrentFreeSlot);
            }
            
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public FreeSlotEnumerator GetEnumerator()
            {
                return this;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public FreeSlotEnumerator GetFreeSlotEnumerator()
        {
            return new FreeSlotEnumerator(ref this);
        }
    }
}