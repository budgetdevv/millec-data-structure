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
}
