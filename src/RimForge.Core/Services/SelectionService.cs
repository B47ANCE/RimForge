using RimForge.Core.Models;

namespace RimForge.Core.Services;

public sealed class SelectionService : ISelectionService
{
    private readonly IApplicationEventBus? _eventBus;
    private ModRecord? _selectedMod;

    public SelectionService(IApplicationEventBus? eventBus = null) => _eventBus = eventBus;

    public ModRecord? SelectedMod => _selectedMod;
    public ForgeGraphQueryOrigin Origin { get; private set; } = ForgeGraphQueryOrigin.Inspector;
    public event EventHandler<ModRecord?>? SelectionChanged;

    public void Select(ModRecord? mod, ForgeGraphQueryOrigin origin = ForgeGraphQueryOrigin.Inspector)
    {
        var sameMod = ReferenceEquals(_selectedMod, mod) || _selectedMod?.Id == mod?.Id;
        if (sameMod && Origin == origin) return;

        if (sameMod)
        {
            Origin = origin;
            return;
        }

        _selectedMod = mod;
        Origin = origin;
        SelectionChanged?.Invoke(this, mod);
        _eventBus?.Publish(new ModSelectionChangedEvent(mod));
    }
}
