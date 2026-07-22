# RimForge UI Controls

## ForgeCheckBox

`ForgeCheckBox` is RimForge's branded replacement for the stock WPF checkbox.
It inherits from `System.Windows.Controls.CheckBox`, so existing bindings,
commands, keyboard input, three-state behavior, automation, and event handlers
continue to work.

Presentation states:

- Unchecked: raised hammer over a steel anvil.
- Hover: hammer lifts and the anvil edge gains the RimForge accent.
- Checked: hammer strikes, the anvil glows, and sparks briefly appear.
- Pressed: immediate impact feedback.
- Indeterminate: three orange work-in-progress dots.
- Disabled: muted steel with no interaction animation.

The entire control surface, including its content, is clickable. The artwork is
implemented with WPF vector geometry so it remains crisp at different DPI and
can inherit future semantic theme variants.
