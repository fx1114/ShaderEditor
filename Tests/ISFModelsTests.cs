using Microsoft.VisualStudio.TestTools.UnitTesting;
using ShaderEditor.Isf;

namespace Tests;

[TestClass]
public sealed class FloatParameterVmTests
{
    [TestMethod]
    public void Constructor_UsesExpectedDefaultValues()
    {
        var parameter = new FloatParameterVm();

        Assert.AreEqual(0f, parameter.Value);
        Assert.AreEqual(0f, parameter.Min);
        Assert.AreEqual(1f, parameter.Max);
    }

    [TestMethod]
    public void Value_Setter_UpdatesValueAndRaisesPropertyChanged()
    {
        var parameter = new FloatParameterVm();
        string? changedProperty = null;

        parameter.PropertyChanged += (_, args) => changedProperty = args.PropertyName;

        parameter.Value = 0.75f;

        Assert.AreEqual(0.75f, parameter.Value);
        Assert.AreEqual(nameof(FloatParameterVm.Value), changedProperty);
    }

    [TestMethod]
    public void ToString_FormatsValueWithInvariantCultureAndUpToThreeDecimals()
    {
        var parameter = new FloatParameterVm
        {
            Value = 1.23456f
        };

        Assert.AreEqual("1.235", parameter.ToString());
    }

    [TestMethod]
    public void ToString_DoesNotIncludeUnnecessaryTrailingZeros()
    {
        var parameter = new FloatParameterVm
        {
            Value = 0.5f
        };

        Assert.AreEqual("0.5", parameter.ToString());
    }

    [TestMethod]
    public void MinAndMax_CanBeConfiguredDuringInitialization()
    {
        var parameter = new FloatParameterVm
        {
            Min = -1f,
            Max = 2.5f
        };

        Assert.AreEqual(-1f, parameter.Min);
        Assert.AreEqual(2.5f, parameter.Max);
    }
}