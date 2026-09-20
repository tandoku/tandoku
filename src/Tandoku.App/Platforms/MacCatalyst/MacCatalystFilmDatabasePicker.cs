namespace Tandoku.App;

using Foundation;
using UIKit;
using UniformTypeIdentifiers;

internal static class MacCatalystFilmDatabasePicker
{
    internal static async Task<FileResult?> PickAsync(Action<string> reportStatus)
    {
        var yamlType = UTType.CreateFromExtension("yaml") ?? UTTypes.PlainText;
        var ymlType = UTType.CreateFromExtension("yml") ?? UTTypes.PlainText;
        // Copying into the sandbox avoids MAUI's intermittent NSFileCoordinator completion failure.
        using var picker = new UIDocumentPickerViewController(
            [yamlType, ymlType, UTTypes.PlainText],
            asCopy: true)
        {
            AllowsMultipleSelection = false,
        };

        var completion = new TaskCompletionSource<FileResult?>(
            TaskCreationOptions.RunContinuationsAsynchronously);

        EventHandler<UIDocumentPickedAtUrlsEventArgs>? pickedHandler = null;
        EventHandler? cancelledHandler = null;

        pickedHandler = (_, eventArgs) =>
        {
            reportStatus($"Picker returned {eventArgs.Urls.Length} file.");
            if (eventArgs.Urls.FirstOrDefault()?.Path is not string path)
            {
                completion.TrySetException(
                    new InvalidDataException("The selected file did not have a readable local copy."));
                return;
            }

            completion.TrySetResult(new FileResult(path));
        };
        cancelledHandler = (_, _) =>
        {
            reportStatus("File selection canceled.");
            completion.TrySetResult(null);
        };

        picker.DidPickDocumentAtUrls += pickedHandler;
        picker.WasCancelled += cancelledHandler;

        try
        {
            var controller = Microsoft.Maui.ApplicationModel.Platform.GetCurrentUIViewController();
            ArgumentNullException.ThrowIfNull(controller);

            reportStatus("Presenting the Mac document picker.");
            await controller.PresentViewControllerAsync(picker, true);
            reportStatus("Waiting for the Mac document picker result.");
            return await completion.Task;
        }
        finally
        {
            picker.DidPickDocumentAtUrls -= pickedHandler;
            picker.WasCancelled -= cancelledHandler;
        }
    }
}
