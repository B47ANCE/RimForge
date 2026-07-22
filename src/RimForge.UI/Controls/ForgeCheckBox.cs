using System.Windows;
using System.Windows.Controls;

namespace RimForge.UI.Controls;

/// <summary>
/// RimForge's branded checkbox. The control preserves normal WPF CheckBox
/// semantics while presenting a hammer-and-anvil interaction surface.
/// </summary>
public sealed class ForgeCheckBox : CheckBox
{
    static ForgeCheckBox()
    {
        DefaultStyleKeyProperty.OverrideMetadata(
            typeof(ForgeCheckBox),
            new FrameworkPropertyMetadata(typeof(ForgeCheckBox)));
    }
}
