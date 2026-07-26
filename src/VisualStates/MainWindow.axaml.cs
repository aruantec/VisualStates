using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using VisualStates.Controls;
using VisualStates.Services;
using VisualStates.ViewModels;

namespace VisualStates;

/// <summary>
/// Primary editor window: hosts the graph canvas and forwards view-area pointer input for pan/zoom.
/// </summary>
public partial class MainWindow : Window
{
    /// <summary>
    /// Initializes the window from XAML. Used by the DI container and design-time tooling.
    /// </summary>
    public MainWindow()
    {
        InitializeComponent();
    }

    /// <summary>
    /// Creates the window with view model and window context wired for data binding and dialogs.
    /// </summary>
    /// <param name="viewModel">Root view model for the editor.</param>
    /// <param name="windowContext">Shared context so services can obtain this window as owner.</param>
    public MainWindow(MainViewModel viewModel, IWindowContext windowContext) : this()
    {
        DataContext = viewModel;
        windowContext.MainWindow = this;
        Title = viewModel.Title;
        viewModel.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(MainViewModel.Title))
                Title = viewModel.Title;
        };
    }

    /// <summary>Closes the application window when Exit is chosen from the menu.</summary>
    /// <param name="sender">The menu item that raised the event.</param>
    /// <param name="e">Routed event arguments.</param>
    private void OnExitClick(object? sender, RoutedEventArgs e) => Close();

    /// <summary>
    /// Forwards wheel events on the canvas to zoom the graph at the pointer position.
    /// </summary>
    /// <param name="sender">The control that received the wheel event.</param>
    /// <param name="e">Pointer wheel event arguments.</param>
    private void OnCanvasPointerWheelChanged(object? sender, PointerWheelEventArgs e)
    {
        if (e.Handled || GraphCanvas.ViewModel is null)
            return;

        var position = e.GetPosition(GraphCanvas);
        GraphCanvas.ApplyZoomAt(position, GraphViewport.GetWheelZoomFactor(e.Delta));
        e.Handled = true;
    }

    /// <summary>
    /// Starts view panning when the user presses outside the graph canvas but inside the view area.
    /// </summary>
    /// <param name="sender">The view-area control that received the press.</param>
    /// <param name="e">Pointer pressed event arguments.</param>
    private void OnViewAreaPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.Handled || e.Source is GraphCanvas)
            return;

        GraphCanvas.HandlePointerPressed(e);
        if (GraphCanvas.IsViewPanning)
            e.Handled = true;
    }

    /// <summary>
    /// Continues view panning while the pointer moves over the view area (not over the canvas itself).
    /// </summary>
    /// <param name="sender">The view-area control that received the move.</param>
    /// <param name="e">Pointer move event arguments.</param>
    private void OnViewAreaPointerMoved(object? sender, PointerEventArgs e)
    {
        if (e.Handled || e.Source is GraphCanvas)
            return;

        if (GraphCanvas.IsViewPanning)
        {
            GraphCanvas.HandlePointerMoved(e);
            e.Handled = true;
        }
    }

    /// <summary>
    /// Ends view panning when the pointer is released over the view area.
    /// </summary>
    /// <param name="sender">The view-area control that received the release.</param>
    /// <param name="e">Pointer released event arguments.</param>
    private void OnViewAreaPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (e.Handled || e.Source is GraphCanvas)
            return;

        if (GraphCanvas.IsViewPanning)
        {
            GraphCanvas.HandlePointerReleased(e);
            e.Handled = true;
        }
    }
}
