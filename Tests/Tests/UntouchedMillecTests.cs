using System;
using System.Runtime.CompilerServices;
using FluentAssertions;
using MILLEC;
using Tests.Data;

namespace Tests.Tests;

public class UntouchedMillecTests
{
    [Test]
    public void NewMillecHasZeroItemCount()
    {
        var millec = MILLECTestHelpers.New(itemCount: 0, capacity: 8);
        millec.Count.Should().Be(0);
    }

    [Test]
    [TestCase(0)]
    [TestCase(1)]
    [TestCase(8)]
    public void IndexedAccessToUntouchedSlotsShouldError(int capacity)
    {
        var millec = MILLECTestHelpers.New(itemCount: 0, capacity: capacity);

        for (int i = 0; i < capacity; i++)
            MILLECTestHelpers.AssertThrows<Exception>(() => { int x = millec[i]; });
    }

    [Test]
    [TestCase(0)]
    [TestCase(1)]
    [TestCase(8)]
    public void IndexedAccessOutOfBoundsShouldError(int capacity)
    {
        var millec = MILLECTestHelpers.New(itemCount: 0, capacity: capacity);
        MILLECTestHelpers.AssertThrows<Exception>(() => { int x = millec[-1]; });
        MILLECTestHelpers.AssertThrows<Exception>(() => { int x = millec[capacity]; });
    }

    [Test]
    [TestCase(0)]
    [TestCase(1)]
    [TestCase(8)]
    public void ByRefEnumerationReturnsZeroItems(int capacity)
    {
        var millec = MILLECTestHelpers.New(itemCount: 0, capacity: capacity);
        foreach (ref var x in millec)
            throw new Exception("This exception should not occur because there are no items to enumerator.");
    }

    [Test]
    [TestCase(0)]
    [TestCase(1)]
    [TestCase(8)]
    public void IndexEnumerationReturnsZeroItems(int capacity)
    {
        var millec = MILLECTestHelpers.New(itemCount: 0, capacity: capacity);
        foreach (int idx in millec)
            throw new Exception("This exception should not occur because there are no items to enumerator.");
    }
}