using RimForge.Core.Models;

namespace RimForge.Core.Services;

public sealed class SelectionService : ISelectionService
{
    private readonly IApplicationEventBus? _eventBus;
    private ModRecord? _selectedMod;

    public SelectionService(IApplicationEventBus? eventBus = null) => _eventBus = eventBus;

    public ModRecord? SelectedMod => _selectedMod;
    public event EventHandler<ModRecord?>? SelectionChanged;

    public void Select(ModRecord? mod)
    {
        if (ReferenceEquals(_selectedMod, mod)) return;
        if (_selectedMod?.Id == mod?.Id) return;

        _selectedMod = mod;
        SelectionChanged?.Invoke(this, mod);
        _eventBus?.Publish(new ModSelectionChangedEvent(mod));
    }
}
