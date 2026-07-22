using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Animation;
using RimForge.UI.Controls;

namespace RimForge.UI.Dialogs;

public partial class ForgeDialogWindow : Window
{
    public static readonly DependencyProperty TitleTextProperty = DependencyProperty.Register(
        nameof(TitleText), typeof(string), typeof(ForgeDialogWindow), new PropertyMetadata("RimForge"));

    public static readonly DependencyProperty HeadingProperty = DependencyProperty.Register(
        nameof(Heading), typeof(string), typeof(ForgeDialogWindow), new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty MessageProperty = DependencyProperty.Register(
        nameof(Message), typeof(string), typeof(ForgeDialogWindow), new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty DetailContentProperty = DependencyProperty.Register(
        nameof(DetailContent), typeof(object), typeof(ForgeDialogWindow), new PropertyMetadata(null));

    public static readonly DependencyProperty InputTextProperty = DependencyProperty.Register(
        nameof(InputText), typeof(string), typeof(ForgeDialogWindow), new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty IsInputVisibleProperty = DependencyProperty.Register(
        nameof(IsInputVisible), typeof(bool), typeof(ForgeDialogWindow), new PropertyMetadata(true));

    public static readonly DependencyProperty IsCancelVisibleProperty = DependencyProperty.Register(
        nameof(IsCancelVisible), typeof(bool), typeof(ForgeDialogWindow), new PropertyMetadata(true));

    public static readonly DependencyProperty DialogIconKindProperty = DependencyProperty.Register(
        nameof(DialogIconKind), typeof(ForgeIconKind), typeof(ForgeDialogWindow), new PropertyMetadata(ForgeIconKind.Profile));

    public static readonly DependencyProperty PrimaryIconKindProperty = DependencyProperty.Register(
        nameof(PrimaryIconKind), typeof(ForgeIconKind), typeof(ForgeDialogWindow), new PropertyMetadata(ForgeIconKind.Export));

    private bool _isClosing;

    public bool Accepted { get; private set; }

    public ForgeDialogWindow()
    {
        InitializeComponent();
        Loaded += (_, _) =>
        {
            if (IsInputVisible)
            {
                InputBox.Focus();
                InputBox.SelectAll();
            }
            else
            {
                PrimaryButton.Focus();
            }
        };
    }

    public string TitleText
    {
        get => (string)GetValue(TitleTextProperty);
        set => SetValue(TitleTextProperty, value);
    }

    public string Heading
    {
        get => (string)GetValue(HeadingProperty);
        set => SetValue(HeadingProperty, value);
    }

    public string Message
    {
        get => (string)GetValue(MessageProperty);
        set => SetValue(MessageProperty, value);
    }

    public object? DetailContent
    {
        get => GetValue(DetailContentProperty);
        set => SetValue(DetailContentProperty, value);
    }

    public string InputText
    {
        get => (string)GetValue(InputTextProperty);
        set => SetValue(InputTextProperty, value);
    }

    public bool IsInputVisible
    {
        get => (bool)GetValue(IsInputVisibleProperty);
        set => SetValue(IsInputVisibleProperty, value);
    }

    public bool IsCancelVisible
    {
        get => (bool)GetValue(IsCancelVisibleProperty);
        set => SetValue(IsCancelVisibleProperty, value);
    }

    public ForgeIconKind DialogIconKind
    {
        get => (ForgeIconKind)GetValue(DialogIconKindProperty);
        set => SetValue(DialogIconKindProperty, value);
    }

    public ForgeIconKind PrimaryIconKind
    {
        get => (ForgeIconKind)GetValue(PrimaryIconKindProperty);
        set => SetValue(PrimaryIconKindProperty, value);
    }

    public string PrimaryText
    {
        set => PrimaryButtonLabel.Text = value;
    }

    public bool IsDanger
    {
        set
        {
            if (value)
            {
                PrimaryButton.SetResourceReference(StyleProperty, "DialogDangerButton");
                PrimaryIconKind = ForgeIconKind.Delete;
            }
        }
    }

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 1)
        {
            DragMove();
        }
    }

    private async void Primary_Click(object sender, RoutedEventArgs e)
    {
        if (IsInputVisible && string.IsNullOrWhiteSpace(InputText))
        {
            InputBox.Focus();
            return;
        }

        await CloseWithResultAsync(true);
    }

    private async void Cancel_Click(object sender, RoutedEventArgs e) => await CloseWithResultAsync(false);
    private async void Close_Click(object sender, RoutedEventArgs e) => await CloseWithResultAsync(false);

    private async void Window_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            e.Handled = true;
            await CloseWithResultAsync(false);
        }
    }

    private async Task CloseWithResultAsync(bool result)
    {
        if (_isClosing)
        {
            return;
        }

        _isClosing = true;
        PrimaryButton.IsEnabled = false;

        var duration = TimeSpan.FromMilliseconds(110);
        var easing = new CubicEase { EasingMode = EasingMode.EaseIn };
        RootChrome.BeginAnimation(OpacityProperty, new DoubleAnimation(0, duration) { EasingFunction = easing });

        if (RootChrome.RenderTransform is System.Windows.Media.ScaleTransform scale)
        {
            scale.BeginAnimation(System.Windows.Media.ScaleTransform.ScaleXProperty,
                new DoubleAnimation(0.985, duration) { EasingFunction = easing });
            scale.BeginAnimation(System.Windows.Media.ScaleTransform.ScaleYProperty,
                new DoubleAnimation(0.985, duration) { EasingFunction = easing });
        }

        await Task.Delay(duration);
        Accepted = result;
        Close();
    }
}
