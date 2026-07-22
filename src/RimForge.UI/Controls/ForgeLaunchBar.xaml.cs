using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace RimForge.UI.Controls;

public partial class ForgeLaunchBar : UserControl
{
    public static readonly RoutedEvent RefreshRequestedEvent = Register(nameof(RefreshRequested));
    public static readonly RoutedEvent OpenProfileRequestedEvent = Register(nameof(OpenProfileRequested));
    public static readonly RoutedEvent LaunchRequestedEvent = Register(nameof(LaunchRequested));
    public static readonly RoutedEvent ForgeRequestedEvent = Register(nameof(ForgeRequested));
    public static readonly RoutedEvent ProfileManagementRequestedEvent = Register(nameof(ProfileManagementRequested));
    public static readonly RoutedEvent ProfileCreateRequestedEvent = Register(nameof(ProfileCreateRequested));
    public static readonly RoutedEvent ProfileRenameRequestedEvent = Register(nameof(ProfileRenameRequested));
    public static readonly RoutedEvent ProfileDuplicateRequestedEvent = Register(nameof(ProfileDuplicateRequested));
    public static readonly RoutedEvent ProfileFavoriteRequestedEvent = Register(nameof(ProfileFavoriteRequested));
    public static readonly RoutedEvent ProfileImportRequestedEvent = Register(nameof(ProfileImportRequested));
    public static readonly RoutedEvent ProfileExportRequestedEvent = Register(nameof(ProfileExportRequested));
    public static readonly RoutedEvent ProfileCompareRequestedEvent = Register(nameof(ProfileCompareRequested));
    public static readonly RoutedEvent ProfileLockRequestedEvent = Register(nameof(ProfileLockRequested));
    public static readonly RoutedEvent ProfileDeleteRequestedEvent = Register(nameof(ProfileDeleteRequested));
    public static readonly RoutedEvent ManualAutoSortRequestedEvent = Register(nameof(ManualAutoSortRequested));
    public static readonly RoutedEvent RevertChangesRequestedEvent = Register(nameof(RevertChangesRequested));
    public static readonly RoutedEvent SaveLoadOrderRequestedEvent = Register(nameof(SaveLoadOrderRequested));
    public static readonly RoutedEvent ShowIssuesRequestedEvent = Register(nameof(ShowIssuesRequested));
    public static readonly RoutedEvent FixIssuesRequestedEvent = Register(nameof(FixIssuesRequested));

    public ForgeLaunchBar() => InitializeComponent();

    public event RoutedEventHandler RefreshRequested { add => AddHandler(RefreshRequestedEvent, value); remove => RemoveHandler(RefreshRequestedEvent, value); }
    public event RoutedEventHandler OpenProfileRequested { add => AddHandler(OpenProfileRequestedEvent, value); remove => RemoveHandler(OpenProfileRequestedEvent, value); }
    public event RoutedEventHandler LaunchRequested { add => AddHandler(LaunchRequestedEvent, value); remove => RemoveHandler(LaunchRequestedEvent, value); }
    public event RoutedEventHandler ForgeRequested { add => AddHandler(ForgeRequestedEvent, value); remove => RemoveHandler(ForgeRequestedEvent, value); }
    public event RoutedEventHandler ProfileManagementRequested { add => AddHandler(ProfileManagementRequestedEvent, value); remove => RemoveHandler(ProfileManagementRequestedEvent, value); }
    public event RoutedEventHandler ProfileCreateRequested { add => AddHandler(ProfileCreateRequestedEvent, value); remove => RemoveHandler(ProfileCreateRequestedEvent, value); }
    public event RoutedEventHandler ProfileRenameRequested { add => AddHandler(ProfileRenameRequestedEvent, value); remove => RemoveHandler(ProfileRenameRequestedEvent, value); }
    public event RoutedEventHandler ProfileDuplicateRequested { add => AddHandler(ProfileDuplicateRequestedEvent, value); remove => RemoveHandler(ProfileDuplicateRequestedEvent, value); }
    public event RoutedEventHandler ProfileFavoriteRequested { add => AddHandler(ProfileFavoriteRequestedEvent, value); remove => RemoveHandler(ProfileFavoriteRequestedEvent, value); }
    public event RoutedEventHandler ProfileImportRequested { add => AddHandler(ProfileImportRequestedEvent, value); remove => RemoveHandler(ProfileImportRequestedEvent, value); }
    public event RoutedEventHandler ProfileExportRequested { add => AddHandler(ProfileExportRequestedEvent, value); remove => RemoveHandler(ProfileExportRequestedEvent, value); }
    public event RoutedEventHandler ProfileCompareRequested { add => AddHandler(ProfileCompareRequestedEvent, value); remove => RemoveHandler(ProfileCompareRequestedEvent, value); }
    public event RoutedEventHandler ProfileLockRequested { add => AddHandler(ProfileLockRequestedEvent, value); remove => RemoveHandler(ProfileLockRequestedEvent, value); }
    public event RoutedEventHandler ProfileDeleteRequested { add => AddHandler(ProfileDeleteRequestedEvent, value); remove => RemoveHandler(ProfileDeleteRequestedEvent, value); }
    public event RoutedEventHandler ManualAutoSortRequested { add => AddHandler(ManualAutoSortRequestedEvent, value); remove => RemoveHandler(ManualAutoSortRequestedEvent, value); }
    public event RoutedEventHandler RevertChangesRequested { add => AddHandler(RevertChangesRequestedEvent, value); remove => RemoveHandler(RevertChangesRequestedEvent, value); }
    public event RoutedEventHandler SaveLoadOrderRequested { add => AddHandler(SaveLoadOrderRequestedEvent, value); remove => RemoveHandler(SaveLoadOrderRequestedEvent, value); }
    public event RoutedEventHandler ShowIssuesRequested { add => AddHandler(ShowIssuesRequestedEvent, value); remove => RemoveHandler(ShowIssuesRequestedEvent, value); }
    public event RoutedEventHandler FixIssuesRequested { add => AddHandler(FixIssuesRequestedEvent, value); remove => RemoveHandler(FixIssuesRequestedEvent, value); }

    private static RoutedEvent Register(string name) => EventManager.RegisterRoutedEvent(name, RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(ForgeLaunchBar));
    private void Raise(RoutedEvent routedEvent) => RaiseEvent(new RoutedEventArgs(routedEvent, this));
    private void Refresh_Click(object sender, RoutedEventArgs e)
    {
        var rotation = new RotateTransform();
        RefreshIconHost.RenderTransform = rotation;
        rotation.BeginAnimation(RotateTransform.AngleProperty, new DoubleAnimation
        {
            From = 0,
            To = 360,
            Duration = TimeSpan.FromMilliseconds(500),
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseInOut }
        });

        Raise(RefreshRequestedEvent);
    }
    private void OpenProfile_Click(object sender, RoutedEventArgs e) => Raise(OpenProfileRequestedEvent);
    private void ProfileManagement_Click(object sender, RoutedEventArgs e) => Raise(ProfileManagementRequestedEvent);
    private void Launch_Click(object sender, RoutedEventArgs e) => Raise(LaunchRequestedEvent);
    private void Forge_Click(object sender, RoutedEventArgs e) => Raise(ForgeRequestedEvent);
    private void OnProfileCreateRequested(object sender, RoutedEventArgs e) => Raise(ProfileCreateRequestedEvent);
    private void OnProfileRenameRequested(object sender, RoutedEventArgs e) => Raise(ProfileRenameRequestedEvent);
    private void OnProfileOpenRequested(object sender, RoutedEventArgs e) => Raise(OpenProfileRequestedEvent);
    private void OnProfileDuplicateRequested(object sender, RoutedEventArgs e) => Raise(ProfileDuplicateRequestedEvent);
    private void OnProfileFavoriteRequested(object sender, RoutedEventArgs e) => Raise(ProfileFavoriteRequestedEvent);
    private void OnProfileImportRequested(object sender, RoutedEventArgs e) => Raise(ProfileImportRequestedEvent);
    private void OnProfileExportRequested(object sender, RoutedEventArgs e) => Raise(ProfileExportRequestedEvent);
    private void OnProfileCompareRequested(object sender, RoutedEventArgs e) => Raise(ProfileCompareRequestedEvent);
    private void OnProfileLockRequested(object sender, RoutedEventArgs e) => Raise(ProfileLockRequestedEvent);
    private void OnProfileDeleteRequested(object sender, RoutedEventArgs e) => Raise(ProfileDeleteRequestedEvent);
    private void ManualAutoSort_Click(object sender, RoutedEventArgs e) => Raise(ManualAutoSortRequestedEvent);
    private void RevertChanges_Click(object sender, RoutedEventArgs e) => Raise(RevertChangesRequestedEvent);
    private void SaveLoadOrder_Click(object sender, RoutedEventArgs e) => Raise(SaveLoadOrderRequestedEvent);
    private void ShowIssues_Click(object sender, RoutedEventArgs e) => Raise(ShowIssuesRequestedEvent);
    private void FixIssues_Click(object sender, RoutedEventArgs e) => Raise(FixIssuesRequestedEvent);
}
