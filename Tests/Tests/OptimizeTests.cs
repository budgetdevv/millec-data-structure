using FluentAssertions;
using Tests.Data;

namespace Tests.Tests;

public class OptimizeTests
{
    [Test]
    public void ItemCountShouldNotChange()
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
            millec.RemoveAt(i); 
        }

        millec.Count.Should().Be(NEW_COUNT);
        
        millec.Optimize();
        
        millec.Count.Should().Be(NEW_COUNT);
    }
}