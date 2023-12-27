using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MILLEC;

public static class MILLEC_DebuggingExtensions
{
    public static T[] GetItemsArray<T>(ref this MILLEC<T> instance)
    {
        return instance._itemsArr;
    }

    public static byte[] GetBitVectorsArr<T>(ref this MILLEC<T> instance)
    {
        return instance._bitVectorsArr;
    }

    public static int GetHighestTouchedIndex<T>(ref this MILLEC<T> instance)
    {
        return instance._highestTouchedIndex;
    }
    
    public ref struct TestIndicesEnumerator<T>
    {
        private readonly ref MILLEC<T> List;

        private readonly MILLEC<T>.BitVectorsArrayInterfacer BitArrayInterfacer;

        private readonly int HighestTouchedIndex;

        private int CurrentItemIndex;
        
        public int Current => CurrentItemIndex;
          
        public TestIndicesEnumerator(ref MILLEC<T> list)
        {
            List = ref list;
            BitArrayInterfacer = new MILLEC<T>.BitVectorsArrayInterfacer(list._bitVectorsArr);
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

                if (!new MILLEC<T>.BitInterfacer(BitArrayInterfacer, CurrentItemIndex).IsSet)
                {
                    continue;
                }

                return true;
            }
        }

        public TestIndicesEnumerator<T> GetEnumerator()
        {
            return this;
        }
    }
    
    public static TestIndicesEnumerator<T> GetTestIndicesEnumerator<T>(ref this MILLEC<T> instance)
    {
        return new TestIndicesEnumerator<T>(ref instance);
    }
}
