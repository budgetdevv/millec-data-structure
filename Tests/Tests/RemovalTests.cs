using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using FluentAssertions;
using MILLEC;
using Tests.Data;

namespace Tests.Tests;

public class RemovalTests
{
    [Test]
    [TestCase(0)]
    [TestCase(1)]
    [TestCase(8)]
    public void ItemCountIsValidDuringIterativeRemoval(int capacity)
    {
        var millec = MILLECTestHelpers.New(capacity, capacity: 8);

        for (int i = 0; i < capacity; i++)
        {
            millec.RemoveAt(i);
            millec.Count.Should().Be(capacity - i - 1);
        }
    }

    [Test]
    [TestCase(3, new int[] { 0 })]
    [TestCase(3, new int[] { 0, 1 })]
    [TestCase(3, new int[] { 0, 2 })]
    [TestCase(3, new int[] { 0, 1, 2 })]
    [TestCase(3, new int[] { 1 })]
    [TestCase(3, new int[] { 1, 0 })]
    [TestCase(3, new int[] { 1, 2 })]
    [TestCase(3, new int[] { 1, 0, 2 })]
    [TestCase(3, new int[] { 2 })]
    [TestCase(3, new int[] { 2, 1 })]
    [TestCase(3, new int[] { 2, 0 })]
    [TestCase(3, new int[] { 2, 1, 0 })]

    [TestCase(4, new int[] { 0 })]
    [TestCase(4, new int[] { 0, 1 })]
    [TestCase(4, new int[] { 0, 2 })]
    [TestCase(4, new int[] { 0, 1, 2 })]
    [TestCase(4, new int[] { 1 })]
    [TestCase(4, new int[] { 1, 0 })]
    [TestCase(4, new int[] { 1, 2 })]
    [TestCase(4, new int[] { 1, 0, 2 })]
    [TestCase(4, new int[] { 2 })]
    [TestCase(4, new int[] { 2, 1 })]
    [TestCase(4, new int[] { 2, 0 })]
    [TestCase(4, new int[] { 2, 1, 0 })]
    public void AfterRemovingItems_IndexedAccessibilityMatchesAvailabilityOfSlot(int itemCount, int[] removeTheseIndices)
    {
        var millec = MILLECTestHelpers.New(itemCount, capacity: 8);
        List<int> removedPositions = new List<int>();
        for (int i = 0; i < removeTheseIndices.Length; i++)
        {
            millec.RemoveAt(i);
            removedPositions.Add(i);
            MILLECTestHelpers.AssertThrows<Exception>(() => { int x = millec[i]; });

            for (int j = 0; j < itemCount; j++)
            {
                if (removedPositions.Contains(j))
                    MILLECTestHelpers.AssertThrows<Exception>(() => { int x = millec[j]; });
                else
                    Assert.DoesNotThrow(() => { int x = millec[j]; });
            }
        }
    }

    [Test]
    [TestCase(3, new int[] { 0 })]
    [TestCase(3, new int[] { 0, 1 })]
    [TestCase(3, new int[] { 0, 2 })]
    [TestCase(3, new int[] { 0, 1, 2 })]
    [TestCase(3, new int[] { 1 })]
    [TestCase(3, new int[] { 1, 0 })]
    [TestCase(3, new int[] { 1, 2 })]
    [TestCase(3, new int[] { 1, 0, 2 })]
    [TestCase(3, new int[] { 2 })]
    [TestCase(3, new int[] { 2, 1 })]
    [TestCase(3, new int[] { 2, 0 })]
    [TestCase(3, new int[] { 2, 1, 0 })]

    [TestCase(4, new int[] { 0 })]
    [TestCase(4, new int[] { 0, 1 })]
    [TestCase(4, new int[] { 0, 2 })]
    [TestCase(4, new int[] { 0, 1, 2 })]
    [TestCase(4, new int[] { 1 })]
    [TestCase(4, new int[] { 1, 0 })]
    [TestCase(4, new int[] { 1, 2 })]
    [TestCase(4, new int[] { 1, 0, 2 })]
    [TestCase(4, new int[] { 2 })]
    [TestCase(4, new int[] { 2, 1 })]
    [TestCase(4, new int[] { 2, 0 })]
    [TestCase(4, new int[] { 2, 1, 0 })]
    public void ItemCountIsValidDuringRandomRemovals(int itemCount, int[] removeTheseIndices)
    {
        var millec = MILLECTestHelpers.New(itemCount, capacity: 8);
        for (int i = 0; i < removeTheseIndices.Length; i++)
        {
            millec.RemoveAt(i);
            millec.Count.Should().Be(itemCount - i - 1);
        }
    }

    [Test]
    [TestCase(3, new int[] { 0 })]
    [TestCase(3, new int[] { 0, 1 })]
    [TestCase(3, new int[] { 0, 2 })]
    [TestCase(3, new int[] { 0, 1, 2 })]
    [TestCase(3, new int[] { 1 })]
    [TestCase(3, new int[] { 1, 0 })]
    [TestCase(3, new int[] { 1, 2 })]
    [TestCase(3, new int[] { 1, 0, 2 })]
    [TestCase(3, new int[] { 2 })]
    [TestCase(3, new int[] { 2, 1 })]
    [TestCase(3, new int[] { 2, 0 })]
    [TestCase(3, new int[] { 2, 1, 0 })]

    [TestCase(4, new int[] { 0 })]
    [TestCase(4, new int[] { 0, 1 })]
    [TestCase(4, new int[] { 0, 2 })]
    [TestCase(4, new int[] { 0, 1, 2 })]
    [TestCase(4, new int[] { 1 })]
    [TestCase(4, new int[] { 1, 0 })]
    [TestCase(4, new int[] { 1, 2 })]
    [TestCase(4, new int[] { 1, 0, 2 })]
    [TestCase(4, new int[] { 2 })]
    [TestCase(4, new int[] { 2, 1 })]
    [TestCase(4, new int[] { 2, 0 })]
    [TestCase(4, new int[] { 2, 1, 0 })]
    public void AfterRemovingItems_EnumerationByRefReturnsCorrectValueForEachRemainingItem(int itemCount, int[] removeTheseIndices)
    {
        const int SIZE = 8;
        
        var millec = new MILLEC<int>(size: SIZE);
        var random = new Random();

        var indexValueMap = new Dictionary<int, int>(SIZE);

        for (int index = 0; index < itemCount; index++)
        {
            var val = random.Next();
            millec.Add(val);
            indexValueMap[index] = val;
        }

        var currentRemovedIndices = new HashSet<int>(removeTheseIndices.Length);

        var itemIndexCalculator = millec.GetItemIndexCalculator();
        
        foreach (var removedIndex in removeTheseIndices)
        {
            millec.RemoveAt(removedIndex);
            currentRemovedIndices.Add(removedIndex).Should().BeTrue();
            
            foreach (ref var item in millec)
            {
                var itemIndex = itemIndexCalculator.GetIndexOfItemRef(ref item);
                
                if (!currentRemovedIndices.Contains(itemIndex))
                {
                    indexValueMap[itemIndex].Should().Be(item);
                }
            }
        }
    }

    [Test]
    [TestCase(3, new int[] { 0 })]
    [TestCase(3, new int[] { 0, 1 })]
    [TestCase(3, new int[] { 0, 2 })]
    [TestCase(3, new int[] { 0, 1, 2 })]
    [TestCase(3, new int[] { 1 })]
    [TestCase(3, new int[] { 1, 0 })]
    [TestCase(3, new int[] { 1, 2 })]
    [TestCase(3, new int[] { 1, 0, 2 })]
    [TestCase(3, new int[] { 2 })]
    [TestCase(3, new int[] { 2, 1 })]
    [TestCase(3, new int[] { 2, 0 })]
    [TestCase(3, new int[] { 2, 1, 0 })]

    [TestCase(4, new int[] { 0 })]
    [TestCase(4, new int[] { 0, 1 })]
    [TestCase(4, new int[] { 0, 2 })]
    [TestCase(4, new int[] { 0, 1, 2 })]
    [TestCase(4, new int[] { 1 })]
    [TestCase(4, new int[] { 1, 0 })]
    [TestCase(4, new int[] { 1, 2 })]
    [TestCase(4, new int[] { 1, 0, 2 })]
    [TestCase(4, new int[] { 2 })]
    [TestCase(4, new int[] { 2, 1 })]
    [TestCase(4, new int[] { 2, 0 })]
    [TestCase(4, new int[] { 2, 1, 0 })]
    public void AfterRemovingItems_EnumerationOfIndicesReturnsCorrectValueForEachRemainingItemIndex(int itemCount, int[] removeTheseIndices)
    {
        const int SIZE = 8;
        var millec = new MILLEC<int>(size: SIZE);
        var random = new Random();
        var currentRemovedIndices = new HashSet<int>(removeTheseIndices.Length);
        var indexValueMap = new Dictionary<int, int>(SIZE);

        for (int index = 0; index < itemCount; index++)
        {
            var val = random.Next();
            millec.Add(val);
            indexValueMap.Add(index, val);
        }

        foreach (var removedIndex in removeTheseIndices)
        {
            millec.RemoveAt(removedIndex);
            currentRemovedIndices.Add(removedIndex).Should().BeTrue();
            
            foreach (var presentIndex in millec.GetTestIndicesEnumerator())
            {
                currentRemovedIndices.Contains(presentIndex).Should().BeFalse();
                millec[presentIndex].Should().Be(indexValueMap[presentIndex]);
            }
        }
    }
}