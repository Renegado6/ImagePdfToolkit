using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace ImagePdfToolkit.Controls;

public partial class NumericStepper : UserControl
{
    public static readonly DependencyProperty ValueProperty = DependencyProperty.Register(
        nameof(Value),
        typeof(int),
        typeof(NumericStepper),
        new FrameworkPropertyMetadata(
            0,
            FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
            null,
            CoerceValue));

    public static readonly DependencyProperty MinimumProperty = DependencyProperty.Register(
        nameof(Minimum),
        typeof(int),
        typeof(NumericStepper),
        new PropertyMetadata(0, OnBoundsChanged));

    public static readonly DependencyProperty MaximumProperty = DependencyProperty.Register(
        nameof(Maximum),
        typeof(int),
        typeof(NumericStepper),
        new PropertyMetadata(100, OnBoundsChanged));

    public static readonly DependencyProperty StepProperty = DependencyProperty.Register(
        nameof(Step),
        typeof(int),
        typeof(NumericStepper),
        new PropertyMetadata(1));

    public NumericStepper()
    {
        InitializeComponent();
    }

    public int Value
    {
        get => (int)GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    public int Minimum
    {
        get => (int)GetValue(MinimumProperty);
        set => SetValue(MinimumProperty, value);
    }

    public int Maximum
    {
        get => (int)GetValue(MaximumProperty);
        set => SetValue(MaximumProperty, value);
    }

    public int Step
    {
        get => (int)GetValue(StepProperty);
        set => SetValue(StepProperty, value);
    }

    private static object CoerceValue(DependencyObject dependencyObject, object baseValue)
    {
        var control = (NumericStepper)dependencyObject;
        return Math.Clamp((int)baseValue, control.Minimum, control.Maximum);
    }

    private static void OnBoundsChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs e)
    {
        dependencyObject.CoerceValue(ValueProperty);
    }

    private void IncreaseButton_OnClick(object sender, RoutedEventArgs e)
    {
        Value = Math.Clamp(Value + Math.Max(1, Step), Minimum, Maximum);
    }

    private void DecreaseButton_OnClick(object sender, RoutedEventArgs e)
    {
        Value = Math.Clamp(Value - Math.Max(1, Step), Minimum, Maximum);
    }

    private void ValueTextBox_OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            CommitText();
            Keyboard.ClearFocus();
            e.Handled = true;
        }
        else if (e.Key == Key.Up)
        {
            IncreaseButton_OnClick(sender, e);
            e.Handled = true;
        }
        else if (e.Key == Key.Down)
        {
            DecreaseButton_OnClick(sender, e);
            e.Handled = true;
        }
    }

    private void ValueTextBox_OnLostKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        CommitText();
    }

    private void CommitText()
    {
        if (int.TryParse(ValueTextBox.Text, out var parsed))
        {
            Value = Math.Clamp(parsed, Minimum, Maximum);
        }

        ValueTextBox.Text = Value.ToString();
    }
}
