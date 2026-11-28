using PCManager.Core.Android;

namespace PCManager.Services.Android;

internal static class PackageEngineRules
{
    public static HashSet<string> ToSet(string output) =>
        new(AdbParsers.ParsePackageList(output), StringComparer.Ordinal);

    public static PackageScanResult Failure(AdbProcessResult result, string prefix) =>
        new(
            AdbFailures.IsDeviceDisconnect(result)
                ? PackageScanStatus.Disconnected
                : PackageScanStatus.Failed,
            prefix + ": " + Message(result),
            null);

    public static string Message(AdbProcessResult result) =>
        result.Cancelled
            ? "cancelled"
            : result.TimedOut
                ? "timed out"
                : result.Combined.Length > 0
                    ? result.Combined
                    : $"exit code {result.ExitCode}";

    public static bool AlreadyInState(
        AndroidPackage package,
        PackageAction action,
        out string message)
    {
        switch (action)
        {
            case PackageAction.Uninstall when package.State == PackageState.Uninstalled:
                message = "The package is already uninstalled for this user.";
                return true;
            case PackageAction.Disable when package.State == PackageState.Disabled:
                message = "The package is already disabled.";
                return true;
            case PackageAction.Restore when package.State != PackageState.Uninstalled:
                message = "The package is already installed.";
                return true;
            case PackageAction.Enable when package.State == PackageState.Enabled:
                message = "The package is already enabled.";
                return true;
            default:
                message = string.Empty;
                return false;
        }
    }

    public static bool CanApply(
        AndroidPackage package,
        PackageAction action,
        out string message)
    {
        if (action == PackageAction.Disable && package.State == PackageState.Uninstalled)
        {
            message = "The package is uninstalled; restore it before disabling it.";
            return false;
        }

        if (action == PackageAction.Enable && package.State == PackageState.Uninstalled)
        {
            message = "The package is uninstalled; restore it before enabling it.";
            return false;
        }

        message = string.Empty;
        return true;
    }

    public static AdbCommand CommandFor(
        AdbDevice device,
        string packageId,
        PackageAction action,
        int userId) => action switch
        {
            PackageAction.Uninstall => AdbCommand.UninstallPackage(device.Serial, packageId, userId),
            PackageAction.Disable => AdbCommand.DisablePackage(device.Serial, packageId, userId),
            PackageAction.Restore => AdbCommand.RestorePackage(device.Serial, packageId, userId),
            PackageAction.Enable => AdbCommand.EnablePackage(device.Serial, packageId, userId),
            _ => throw new ArgumentOutOfRangeException(nameof(action), "Unsupported package action."),
        };

    public static PackageActionResult Result(
        PackageActionRequest request,
        PackageAction action,
        PackageState before,
        PackageState? after,
        PackageResultStatus status,
        string message) =>
        new(request.PackageId, request.Action, action, before, after, status, message);

    public static void ApplyVerifiedStates(
        List<PackageActionResult> results,
        PackageScan verifiedScan)
    {
        for (int index = 0; index < results.Count; index++)
        {
            PackageActionResult result = results[index];

            if (result.Status == PackageResultStatus.Ok &&
                verifiedScan.ById.TryGetValue(result.PackageId, out AndroidPackage? package))
            {
                results[index] = result with { After = package.State };
            }
        }
    }

    public static string BuildSummary(IReadOnlyList<PackageActionResult> results)
    {
        int ok = results.Count(result => result.Status == PackageResultStatus.Ok);
        int already = results.Count(result => result.Status == PackageResultStatus.Already);
        int failed = results.Count(result => result.Status == PackageResultStatus.Failed);
        int blocked = results.Count(result => result.Status == PackageResultStatus.BlockedDenylist);

        return $"{ok} succeeded, {already} already, {failed} failed, {blocked} blocked.";
    }
}
