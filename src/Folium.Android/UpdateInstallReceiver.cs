using System.Collections.Concurrent;
using Android.Content;
using Android.Content.PM;
using Folium.Updates;

namespace Folium.Android;

/// <summary>Receives success and failure events from Android's package installer.</summary>
[BroadcastReceiver(Enabled = true, Exported = false)]
public sealed class UpdateInstallReceiver : BroadcastReceiver
{
    private const int STATUS_PENDING_USER_ACTION = -1;
    private const int STATUS_SUCCESS = 0;
    private const int STATUS_FAILURE_INVALID = 4;
    private const int STATUS_FAILURE_CONFLICT = 5;
    private const int STATUS_FAILURE_INCOMPATIBLE = 7;
    private static readonly ConcurrentDictionary<int, TaskCompletionSource<bool>> Pending = new();
    private static int _waitingForPromptActivity;
    private static int _promptActivityWasShown;

    /// <summary>Registers the completion source for an install session.</summary>
    /// <param name="sessionId">The package installer session identifier.</param>
    /// <param name="completion">The operation completion source.</param>
    public static void Register(int sessionId, TaskCompletionSource<bool> completion)
    {
        Pending[sessionId] = completion;
    }

    /// <summary>Removes the callback for an abandoned package installer session.</summary>
    /// <param name="sessionId">The package installer session identifier.</param>
    public static void Remove(int sessionId)
    {
        _ = Pending.TryRemove(sessionId, out _);
        _ = Interlocked.Exchange(ref _waitingForPromptActivity, 0);
    }

    /// <summary>Marks the install prompt as visible when Android pauses Folium for it.</summary>
    public static void MarkConfirmationPromptShown()
    {
        if (Interlocked.Exchange(ref _waitingForPromptActivity, 0) == 1)
        {
            _ = Interlocked.Exchange(ref _promptActivityWasShown, 1);
        }
    }

    /// <summary>Returns whether the activity resumes after Android showed the install prompt.</summary>
    public static bool ConsumeConfirmationPromptReturn()
    {
        return Interlocked.Exchange(ref _promptActivityWasShown, 0) == 1;
    }

    /// <inheritdoc />
    public override void OnReceive(Context? context, Intent? intent)
    {
        if (intent is null)
        {
            return;
        }

        int sessionId = intent.GetIntExtra(PackageInstaller.ExtraSessionId, -1);
        int status = intent.GetIntExtra(PackageInstaller.ExtraStatus, 1);
        if (context is null)
        {
            FailPending(sessionId, "Android could not open the install confirmation.");
            return;
        }

        if (status == STATUS_PENDING_USER_ACTION)
        {
            Intent? confirm = GetConfirmIntent(intent);
            if (confirm is null)
            {
                FailPending(sessionId, "Android did not provide an install confirmation.");
                return;
            }

            _ = confirm.AddFlags(ActivityFlags.NewTask);
            _ = Interlocked.Exchange(ref _waitingForPromptActivity, 1);
            try
            {
                context.StartActivity(confirm);
            }
            catch (Java.Lang.Exception exception)
            {
                FailPending(sessionId, "Android could not open the install confirmation.", exception);
            }
            return;
        }

        if (Pending.TryRemove(sessionId, out TaskCompletionSource<bool>? completion))
        {
            _ = Interlocked.Exchange(ref _waitingForPromptActivity, 0);
            if (status == STATUS_SUCCESS)
            {
                _ = completion.TrySetResult(true);
            }
            else
            {
                string message = intent.GetStringExtra(PackageInstaller.ExtraStatusMessage) ?? "Android could not install the update.";
                PackageInstallFailureKind failure = status switch
                {
                    STATUS_FAILURE_INVALID => PackageInstallFailureKind.InvalidApk,
                    STATUS_FAILURE_CONFLICT => PackageInstallFailureKind.PackageConflict,
                    STATUS_FAILURE_INCOMPATIBLE => PackageInstallFailureKind.DeviceIncompatible,
                    _ => PackageInstallFailureKind.Other,
                };
                UpdateFailureReason reason = UpdateFailureReasons.FromPackageInstallFailure(failure);
                _ = completion.TrySetException(new UpdateFailureException(reason, message));
            }
        }
    }

    private static void FailPending(int sessionId, string message, Exception? innerException = null)
    {
        _ = Interlocked.Exchange(ref _waitingForPromptActivity, 0);
        _ = Interlocked.Exchange(ref _promptActivityWasShown, 0);
        if (Pending.TryRemove(sessionId, out TaskCompletionSource<bool>? completion))
        {
            _ = completion.TrySetException(new UpdateFailureException(UpdateFailureReason.Install, message, innerException));
        }
    }

    private static Intent? GetConfirmIntent(Intent intent)
    {
        return OperatingSystem.IsAndroidVersionAtLeast(33)
            ? intent.GetParcelableExtra(Intent.ExtraIntent, Java.Lang.Class.FromType(typeof(Intent))) as Intent
            : intent.GetParcelableExtra(Intent.ExtraIntent) as Intent;
    }
}
