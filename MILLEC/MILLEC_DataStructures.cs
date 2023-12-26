using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace MILLEC
{
    public unsafe partial struct MILLEC<T>
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
            public readonly ref T FirstItem;

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public ItemsArrayInterfacer(T[] itemsArr)
            {
                FirstItem = ref MemoryMarshal.GetArrayDataReference(itemsArr);
            }

            public ref T this[int index]
            {
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                get => ref Unsafe.Add(ref FirstItem, index);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
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
        
        public ref struct ItemsEnumerator
        {
            private readonly ref T FirstItem, LastItem;
            
            private ref T CurrentItem;

            // private ref byte CurrentBitVector;

            private readonly BitVectorsArrayInterfacer BitVectorsArrayInterfacer;
            
            public ref T Current => ref CurrentItem;

            internal ItemsEnumerator(ItemsArrayInterfacer itemsArrayInterfacer, BitVectorsArrayInterfacer bitVectorsArrayInterfacer)
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

        public ItemsEnumerator GetEnumerator()
        {
            return new ItemsEnumerator(new ItemsArrayInterfacer(_itemsArr), new BitVectorsArrayInterfacer(_bitVectorsArr));
        }
    }
}