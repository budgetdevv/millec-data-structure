namespace MILLEC
{
    public static class MILLEC_DebuggingExtensions
    {
        public static ItemT[] GetItemsArray<ItemT, OptsT>(ref this MILLEC<ItemT, OptsT> instance) where OptsT : IMILLECOptions<ItemT>
        {
            return instance._itemsArr;
        }

        public static byte[] GetBitVectorsArr<ItemT, OptsT>(ref this MILLEC<ItemT, OptsT> instance) where OptsT : IMILLECOptions<ItemT>
        {
            return instance._bitVectorsArr;
        }

        public static int GetHighestTouchedIndex<ItemT, OptsT>(ref this MILLEC<ItemT, OptsT> instance) where OptsT : IMILLECOptions<ItemT>
        {
            return instance._highestTouchedIndex;
        }
    
        public ref struct TestIndicesEnumerator<ItemT, OptsT> where OptsT : IMILLECOptions<ItemT>
        {
            // This field is used for debugging.
            // ReSharper disable once NotAccessedField.Local
            private readonly ref MILLEC<ItemT, OptsT> List;

            private readonly MILLEC<ItemT, OptsT>.BitVectorsArrayInterfacer BitArrayInterfacer;

            private readonly int HighestTouchedIndex;

            private int CurrentItemIndex;
        
            // ReSharper disable once ConvertToAutoPropertyWhenPossible
            public int Current => CurrentItemIndex;
          
            public TestIndicesEnumerator(ref MILLEC<ItemT, OptsT> list)
            {
                List = ref list;
                BitArrayInterfacer = new MILLEC<ItemT, OptsT>.BitVectorsArrayInterfacer(list._bitVectorsArr);
                CurrentItemIndex = -1;
                HighestTouchedIndex = list._highestTouchedIndex;
            }

            public bool MoveNext()
            {
                while (true)
                {
                    CurrentItemIndex++;
                    var shouldMoveNext = CurrentItemIndex <= HighestTouchedIndex;

                    if (!shouldMoveNext)
                    {
                        return false;
                    }

                    if (!new MILLEC<ItemT, OptsT>.BitInterfacer(BitArrayInterfacer, CurrentItemIndex).IsSet)
                    {
                        continue;
                    }

                    return true;
                }
            }

            public TestIndicesEnumerator<ItemT, OptsT> GetEnumerator()
            {
                return this;
            }
        }
    
        public static TestIndicesEnumerator<ItemT, OptsT> GetTestIndicesEnumerator<ItemT, OptsT>(ref this MILLEC<ItemT, OptsT> instance) where OptsT : IMILLECOptions<ItemT>
        {
            return new TestIndicesEnumerator<ItemT, OptsT>(ref instance);
        }
    }
}
