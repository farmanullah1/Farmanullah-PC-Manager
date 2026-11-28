using PCManager.Core.Models;

namespace PCManager.Services.Abstractions;

/// <summary>Small Windows utilities exposed by the Toolbox.</summary>
public interface IToolboxService
{
    IReadOnlyList<ToolboxTool> GetTools();

    /// <summary>Runs the tool and returns a message for the UI.</summary>
    Task<OperationResult> LaunchAsync(string toolId, CancellationToken cancellationToken = default);
}

public interface IToolboxLayoutService
{
    IReadOnlyList<ToolboxLayoutEntry> Layout { get; }
    event EventHandler? Changed;
    void SetEnabled(string id, bool enabled);
    bool Move(string id, int offset);
    void Reset();
    Task ExportAsync(string path, CancellationToken cancellationToken = default);
    Task<bool> ImportAsync(string path, CancellationToken cancellationToken = default);
}
