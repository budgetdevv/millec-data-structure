using FluentAssertions;
using Tests.Data;

namespace Tests.Tests;

public class OptimizeTests
{
    private static int ReverseIndex(int index, int count)
    {
        // [ 0, 1, 2 ] ( Length: 3 ), 3 - 0 - 1 = 2, which is the index of last element.
        return count - index - 1;
    }
    
    [Test]
    [TestCase(true)]
    [TestCase(false)]
    public void ItemCountShouldNotChange(bool removeTrailing)
    {
        var millec = TestMillec.New(0, 0);

        const int ADD_COUNT = 5, REMOVE_COUNT = 4, NEW_COUNT = ADD_COUNT - REMOVE_COUNT;

        NEW_COUNT.Should().BeGreaterThanOrEqualTo(0);
        
        for (int i = 0; i < ADD_COUNT; i++)
        {
            millec.Add(i); 
        }
        
        for (int i = 0; i < REMOVE_COUNT; i++)
        {
            millec.RemoveAt(!removeTrailing ? i : ReverseIndex(i, ADD_COUNT)); 
        }

        millec.Count.Should().Be(NEW_COUNT);
        
        millec.Optimize();
        
        millec.Count.Should().Be(NEW_COUNT);
    }

    [Test]
    public void TrailingRemovesShouldBeOptimized()
    {
        var millec = TestMillec.New(0, 0);
        
        const int ADD_COUNT = 5, REMOVE_COUNT = 4, NEW_COUNT = ADD_COUNT - REMOVE_COUNT;

        NEW_COUNT.Should().BeGreaterThanOrEqualTo(0);
        
        for (int i = 0; i < ADD_COUNT; i++)
        {
            millec.Add(i); 
        }
        
        for (int i = 0; i < REMOVE_COUNT; i++)
        {
            millec.RemoveAt(ReverseIndex(i, ADD_COUNT)); 
        }

        millec.FreeSlotCount.Should().Be(0);

        millec.GetFreeSlotIndicesAllocating().Length.Should().Be(0);
    }
}