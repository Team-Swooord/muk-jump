using System.Reflection;
using NUnit.Framework;
using MukJump.Core;

public class GameplayHudViewTests
{
    [TestCase(0, "0m")]
    [TestCase(9999, "9999m")]
    [TestCase(10000, "10km")]
    [TestCase(10500, "10.5km")]
    [TestCase(99999, "100km")]
    public void LargeHeightUsesCompactReadableUnit(int meters, string expected)
    {
        var method = typeof(GameplayHudView).GetMethod(
            "FormatHeight", BindingFlags.Static | BindingFlags.NonPublic);

        Assert.IsNotNull(method);
        Assert.AreEqual(expected, method.Invoke(null, new object[] { meters }));
    }
}
