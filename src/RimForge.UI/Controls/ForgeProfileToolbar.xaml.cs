using System.Windows;
using System.Windows.Controls;

namespace RimForge.UI.Controls;

public partial class ForgeProfileToolbar : UserControl
{
    public static readonly DependencyProperty CanRenameProperty = DependencyProperty.Register(
        nameof(CanRename), typeof(bool), typeof(ForgeProfileToolbar), new PropertyMetadata(true));
    public static readonly DependencyProperty CanLockProperty = DependencyProperty.Register(
        nameof(CanLock), typeof(bool), typeof(ForgeProfileToolbar), new PropertyMetadata(true));
    public static readonly DependencyProperty CanDeleteProperty = DependencyProperty.Register(
        nameof(CanDelete), typeof(bool), typeof(ForgeProfileToolbar), new PropertyMetadata(true));
    public static readonly DependencyProperty IsLockedProperty = DependencyProperty.Register(
        nameof(IsLocked), typeof(bool), typeof(ForgeProfileToolbar), new PropertyMetadata(false));
    public static readonly DependencyProperty IsFavoriteProperty = DependencyProperty.Register(
        nameof(IsFavorite), typeof(bool), typeof(ForgeProfileToolbar), new PropertyMetadata(false));

    public static readonly RoutedEvent CreateRequestedEvent = Register(nameof(CreateRequested));
    public static readonly RoutedEvent RenameRequestedEvent = Register(nameof(RenameRequested));
    public static readonly RoutedEvent OpenRequestedEvent = Register(nameof(OpenRequested));
    public static readonly RoutedEvent DuplicateRequestedEvent = Register(nameof(DuplicateRequested));
    public static readonly RoutedEvent FavoriteRequestedEvent = Register(nameof(FavoriteRequested));
    public static readonly RoutedEvent ImportRequestedEvent = Register(nameof(ImportRequested));
    public static readonly RoutedEvent ExportRequestedEvent = Register(nameof(ExportRequested));
    public static readonly RoutedEvent CompareRequestedEvent = Register(nameof(CompareRequested));
    public static readonly RoutedEvent LockRequestedEvent = Register(nameof(LockRequested));
    public static readonly RoutedEvent DeleteRequestedEvent = Register(nameof(DeleteRequested));

    public ForgeProfileToolbar() => InitializeComponent();

    public bool CanRename { get => (bool)GetValue(CanRenameProperty); set => SetValue(CanRenameProperty, value); }
    public bool CanLock { get => (bool)GetValue(CanLockProperty); set => SetValue(CanLockProperty, value); }
    public bool CanDelete { get => (bool)GetValue(CanDeleteProperty); set => SetValue(CanDeleteProperty, value); }
    public bool IsLocked { get => (bool)GetValue(IsLockedProperty); set => SetValue(IsLockedProperty, value); }
    public bool IsFavorite { get => (bool)GetValue(IsFavoriteProperty); set => SetValue(IsFavoriteProperty, value); }

    public event RoutedEventHandler CreateRequested { add => AddHandler(CreateRequestedEvent, value); remove => RemoveHandler(CreateRequestedEvent, value); }
    public event RoutedEventHandler RenameRequested { add => AddHandler(RenameRequestedEvent, value); remove => RemoveHandler(RenameRequestedEvent, value); }
    public event RoutedEventHandler OpenRequested { add => AddHandler(OpenRequestedEvent, value); remove => RemoveHandler(OpenRequestedEvent, value); }
    public event RoutedEventHandler DuplicateRequested { add => AddHandler(DuplicateRequestedEvent, value); remove => RemoveHandler(DuplicateRequestedEvent, value); }
    public event RoutedEventHandler FavoriteRequested { add => AddHandler(FavoriteRequestedEvent, value); remove => RemoveHandler(FavoriteRequestedEvent, value); }
    public event RoutedEventHandler ImportRequested { add => AddHandler(ImportRequestedEvent, value); remove => RemoveHandler(ImportRequestedEvent, value); }
    public event RoutedEventHandler ExportRequested { add => AddHandler(ExportRequestedEvent, value); remove => RemoveHandler(ExportRequestedEvent, value); }
    public event RoutedEventHandler CompareRequested { add => AddHandler(CompareRequestedEvent, value); remove => RemoveHandler(CompareRequestedEvent, value); }
    public event RoutedEventHandler LockRequested { add => AddHandler(LockRequestedEvent, value); remove => RemoveHandler(LockRequestedEvent, value); }
    public event RoutedEventHandler DeleteRequested { add => AddHandler(DeleteRequestedEvent, value); remove => RemoveHandler(DeleteRequestedEvent, value); }

    private static RoutedEvent Register(string name) => EventManager.RegisterRoutedEvent(name, RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(ForgeProfileToolbar));
    private void Raise(RoutedEvent routedEvent) => RaiseEvent(new RoutedEventArgs(routedEvent, this));
    private void Create_Click(object sender, RoutedEventArgs e) => Raise(CreateRequestedEvent);
    private void Rename_Click(object sender, RoutedEventArgs e) => Raise(RenameRequestedEvent);
    private void Open_Click(object sender, RoutedEventArgs e) => Raise(OpenRequestedEvent);
    private void Duplicate_Click(object sender, RoutedEventArgs e) => Raise(DuplicateRequestedEvent);
    private void Favorite_Click(object sender, RoutedEventArgs e) => Raise(FavoriteRequestedEvent);
    private void Import_Click(object sender, RoutedEventArgs e) => Raise(ImportRequestedEvent);
    private void Export_Click(object sender, RoutedEventArgs e) => Raise(ExportRequestedEvent);
    private void Compare_Click(object sender, RoutedEventArgs e) => Raise(CompareRequestedEvent);
    private void Lock_Click(object sender, RoutedEventArgs e) => Raise(LockRequestedEvent);
    private void Delete_Click(object sender, RoutedEventArgs e) => Raise(DeleteRequestedEvent);
}
