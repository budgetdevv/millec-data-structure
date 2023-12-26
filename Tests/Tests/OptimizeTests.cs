using FluentAssertions;
using Tests.Data;

namespace Tests.Tests;

public class OptimizeTests
{
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
            // [ 0, 1, 2 ] ( Length: 3 ), 3 - 0 - 1 = 2, which is the index of last element.
            var index = !removeTrailing ? i : ADD_COUNT - i - 1;
            
            millec.RemoveAt(index); 
        }

        millec.Count.Should().Be(NEW_COUNT);
        
        millec.Optimize();
        
        millec.Count.Should().Be(NEW_COUNT);
    }
}